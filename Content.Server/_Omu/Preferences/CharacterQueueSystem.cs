// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Server.Station.Events;
using Content.Shared._Omu.Preferences;
using Content.Shared.Antag;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Omu.Preferences;

public sealed class CharacterQueueSystem : SharedCharacterQueueSystem
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserDbDataManager _userDb = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly PlayTimeTrackingSystem _playTime = default!;

    private readonly Dictionary<NetUserId, CharacterQueueState> _queues = new();
    private readonly Dictionary<NetUserId, uint> _sequences = new();
    private readonly Dictionary<NetUserId, HumanoidCharacterProfile> _spawned = new();
    private readonly Dictionary<NetUserId, List<List<ProtoId<AntagPrototype>>>> _reservations = new();

    public override void Initialize()
    {
        base.Initialize();

        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(OnPlayerDisconnect);
        _prefs.CharacterDeleted += OnCharacterDeleted;

        SubscribeNetworkEvent<SetCharacterQueueEvent>(OnSetCharacterQueue);

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning, after: [typeof(AntagSelectionSystem)]);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnRulePlayerJobsAssigned);
        SubscribeLocalEvent<StationJobsGetCandidatesEvent>(OnStationJobsGetCandidates);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, before: [typeof(AntagSelectionSystem)]);
        SubscribeLocalEvent<NoJobsAvailableSpawningEvent>(OnNoJobsAvailable);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _prefs.CharacterDeleted -= OnCharacterDeleted;
    }

    public HumanoidCharacterProfile GetAssignmentProfile(NetUserId userId, PlayerPreferences prefs)
    {
        var queue = GetQueue(userId, prefs);
        var characters = GetActiveCharacters(prefs, queue).ToList();
        var priorities = queue.JobPriorities
            .Where(pair => characters.Any(c => c.JobPriorities.ContainsKey(pair.Key)))
            .ToDictionary();
        var overflow = characters.Any(c => c.PreferenceUnavailable == PreferenceUnavailableMode.SpawnAsOverflow);

        return (GetPrimaryCharacter(prefs, queue) ?? HumanoidCharacterProfile.Random())
            .WithJobPriorities(priorities)
            .WithPreferenceUnavailable(overflow
                ? PreferenceUnavailableMode.SpawnAsOverflow
                : PreferenceUnavailableMode.StayInLobby);
    }

    public IReadOnlyDictionary<ProtoId<JobPrototype>, JobPriority> GetJobPriorities(NetUserId userId)
    {
        return _prefs.TryGetCachedPreferences(userId, out var prefs)
            ? GetAssignmentProfile(userId, prefs).JobPriorities
            : new Dictionary<ProtoId<JobPrototype>, JobPriority>();
    }

    public bool TryPickCharacter(
        ICommonSession session,
        string? jobId,
        [NotNullWhen(true)] out HumanoidCharacterProfile? character)
    {
        if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
        {
            character = HumanoidCharacterProfile.Random();
            return true;
        }

        var queue = GetQueue(session.UserId, prefs);
        character = jobId == null
            ? GetPrimaryCharacter(prefs, queue)
            : PickCharacter(session, GetCandidates(prefs, queue, jobId), jobId, false);

        if (character != null)
            return true;

        RaiseLocalEvent(new NoJobsAvailableSpawningEvent(session));
        _chat.DispatchServerMessage(session, Loc.GetString("character-queue-no-character-for-job"));
        return false;
    }

    public HumanoidCharacterProfile? GetCurrentCharacter(NetUserId userId)
    {
        if (_spawned.TryGetValue(userId, out var spawned))
            return spawned;

        var prefs = _prefs.GetPreferences(userId);
        return GetPrimaryCharacter(prefs, GetQueue(userId, prefs));
    }

    public HumanoidCharacterProfile? GetAntagCharacter(NetUserId userId, List<ProtoId<AntagPrototype>> antags)
    {
        var prefs = _prefs.GetPreferences(userId);
        var wanting = GetActiveCharacters(prefs, GetQueue(userId, prefs))
            .Where(c => c.AntagPreferences.Overlaps(antags))
            .ToList();

        var character = wanting.Count > 0 ? _random.Pick(wanting) : GetCurrentCharacter(userId);
        if (character != null)
            _spawned[userId] = character;

        return character;
    }

    public async Task SetQueue(ICommonSession session, CharacterQueueState state)
    {
        if (!_queues.ContainsKey(session.UserId))
            return;

        var maxSlots = _cfg.GetCVar(CCVars.GameMaxCharacterSlots);
        var queue = new CharacterQueueState(
            SanitizePriorities(state.JobPriorities),
            state.ActiveSlots.Where(slot => slot >= 0 && slot < maxSlots).ToHashSet(),
            state.Order.Where(slot => slot >= 0 && slot < maxSlots).Distinct().ToList());

        _queues[session.UserId] = queue;
        SendQueue(session);

        if (ServerPreferencesManager.ShouldStorePrefs(session.Channel.AuthType))
            await _db.SaveCharacterQueueAsync(session.UserId, queue);
    }

    public bool WantsAntag(ICommonSession session, ProtoId<AntagPrototype> antag)
    {
        if (_spawned.TryGetValue(session.UserId, out var spawned))
            return CanBeAntag(session, spawned, antag);

        return _prefs.TryGetCachedPreferences(session.UserId, out var prefs)
            && GetActiveCharacters(prefs, GetQueue(session.UserId, prefs)).Any(c => CanBeAntag(session, c, antag));
    }

    private CharacterQueueState GetQueue(NetUserId userId, PlayerPreferences prefs)
    {
        if (_queues.TryGetValue(userId, out var queue))
            return queue;

        var selected = prefs.Characters.GetValueOrDefault(prefs.SelectedCharacterIndex) as HumanoidCharacterProfile;
        return new CharacterQueueState(
            selected?.JobPriorities.ToDictionary() ?? [],
            [prefs.SelectedCharacterIndex],
            []);
    }

    private HumanoidCharacterProfile? PickCharacter(
        ICommonSession session,
        IEnumerable<HumanoidCharacterProfile> candidates,
        ProtoId<JobPrototype> job,
        bool strict)
    {
        var allowed = candidates.Where(c => _playTime.IsAllowed(session, job, c)).ToList();
        if (_reservations.TryGetValue(session.UserId, out var reserved))
        {
            var fitting = allowed
                .Where(c => reserved.Any(antags => antags.Any(a => CanBeAntag(session, c, a))))
                .ToList();

            if (fitting.Count > 0 || strict)
                allowed = fitting;
        }

        if (allowed.Count == 0)
            return null;

        var roll = _random.Next(allowed.Sum(c => Weight(c, job)));
        foreach (var character in allowed)
        {
            roll -= Weight(character, job);
            if (roll < 0)
                return character;
        }

        return allowed[^1];
    }

    private Dictionary<ProtoId<JobPrototype>, JobPriority> SanitizePriorities(
        Dictionary<ProtoId<JobPrototype>, JobPriority> priorities)
    {
        var sanitized = new Dictionary<ProtoId<JobPrototype>, JobPriority>();
        var hasHigh = false;

        foreach (var (job, priority) in priorities)
        {
            if (priority is < JobPriority.Low or > JobPriority.High
                || !_prototypes.TryIndex(job, out var jobPrototype)
                || !jobPrototype.SetPreference)
                continue;

            sanitized[job] = priority == JobPriority.High && hasHigh ? JobPriority.Medium : priority;
            hasHigh |= priority == JobPriority.High;
        }

        return sanitized;
    }

    private bool CanBeAntag(ICommonSession session, HumanoidCharacterProfile character, ProtoId<AntagPrototype> antag)
    {
        return character.AntagPreferences.Contains(antag) && _playTime.IsAllowed(session, antag, character);
    }

    private void SendQueue(ICommonSession session)
    {
        if (_queues.TryGetValue(session.UserId, out var queue))
            RaiseNetworkEvent(new CharacterQueueStateEvent(queue), session.Channel);
    }

    private List<AntagSelectionComponent> GetPreselectingRules()
    {
        var rules = new List<AntagSelectionComponent>();
        var query = EntityQueryEnumerator<AntagSelectionComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            if (rule.SelectionTime == AntagSelectionTime.IntraPlayerSpawn && !HasComp<EndedGameRuleComponent>(uid))
                rules.Add(rule);
        }

        return rules;
    }

    private void ReserveAntags(List<AntagSelectionComponent> rules)
    {
        _reservations.Clear();
        foreach (var rule in rules)
        {
            foreach (var (definition, sessions) in rule.PreSelectedSessions)
            {
                foreach (var session in sessions)
                {
                    _reservations.GetOrNew(session.UserId).Add(definition.PrefRoles);
                }
            }
        }
    }

    private bool HasReservedJob(ICommonSession session, List<ProtoId<JobPrototype>>? blacklist)
    {
        if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
            return true;

        var jobs = GetAssignmentProfile(session.UserId, prefs).JobPriorities.Keys.ToList();
        var ev = new StationJobsGetCandidatesEvent(session.UserId, jobs);
        RaiseLocalEvent(ref ev);

        var bans = _banManager.GetJobBans(session.UserId);
        return jobs.Any(job => blacklist?.Contains(job) != true
            && bans?.Contains(job) != true
            && _prototypes.TryIndex(job, out var jobPrototype)
            && jobPrototype.CanBeAntag);
    }

    private async Task LoadData(ICommonSession session, CancellationToken cancel)
    {
        if (!ServerPreferencesManager.ShouldStorePrefs(session.Channel.AuthType))
            return;

        if (await _db.GetCharacterQueueAsync(session.UserId, cancel) is { } queue)
            _queues[session.UserId] = queue;
    }

    private void FinishLoad(ICommonSession session)
    {
        if (_queues.TryGetValue(session.UserId, out var queue))
            _queues[session.UserId] = new(SanitizePriorities(queue.JobPriorities), queue.ActiveSlots, queue.Order);
        else if (_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
            _queues[session.UserId] = GetQueue(session.UserId, prefs);

        SendQueue(session);
    }

    private void OnPlayerDisconnect(ICommonSession session)
    {
        _queues.Remove(session.UserId);
        _sequences.Remove(session.UserId);
    }

    private void OnCharacterDeleted(NetUserId userId, int slot)
    {
        if (!_queues.TryGetValue(userId, out var queue)
            || !queue.ActiveSlots.Contains(slot) && !queue.Order.Contains(slot))
            return;

        _queues[userId] = new CharacterQueueState(
            queue.JobPriorities,
            queue.ActiveSlots.Where(s => s != slot).ToHashSet(),
            queue.Order.Where(s => s != slot).ToList());

        if (_player.TryGetSessionById(userId, out var session))
            SendQueue(session);
    }

    private async void OnSetCharacterQueue(SetCharacterQueueEvent msg, EntitySessionEventArgs args)
    {
        if (msg.Sequence <= _sequences.GetValueOrDefault(args.SenderSession.UserId))
            return;

        _sequences[args.SenderSession.UserId] = msg.Sequence;
        await SetQueue(args.SenderSession, msg.State);
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        var rules = GetPreselectingRules();
        ReserveAntags(rules);

        var blacklists = _antag.GetPreSelectedAntagSessionsWithBlacklist();
        foreach (var session in ev.PlayerPool)
        {
            if (!_reservations.ContainsKey(session.UserId)
                || HasReservedJob(session, blacklists.GetValueOrDefault(session)))
                continue;

            foreach (var rule in rules)
            {
                if (!rule.RemoveUponFailedSpawn)
                    continue;

                var released = false;
                foreach (var sessions in rule.PreSelectedSessions.Values)
                {
                    released |= sessions.Remove(session);
                }

                if (released)
                    Log.Info($"Released {session} from antag preselection, no active character fits a compatible job.");
            }
        }

        ReserveAntags(rules);
    }

    private void OnRulePlayerJobsAssigned(RulePlayerJobsAssignedEvent ev)
    {
        _reservations.Clear();
    }

    private void OnStationJobsGetCandidates(ref StationJobsGetCandidatesEvent ev)
    {
        if (!_player.TryGetSessionById(ev.Player, out var session)
            || !_prefs.TryGetCachedPreferences(ev.Player, out var prefs))
            return;

        var active = GetActiveCharacters(prefs, GetQueue(ev.Player, prefs)).ToList();
        ev.Jobs.RemoveAll(job =>
            PickCharacter(session, active.Where(c => c.JobPriorities.ContainsKey(job)), job, true) == null);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        _spawned[ev.Player.UserId] = ev.Profile;

        foreach (var rule in GetPreselectingRules())
        {
            if (!rule.RemoveUponFailedSpawn)
                continue;

            foreach (var (definition, sessions) in rule.PreSelectedSessions)
            {
                if (!sessions.Contains(ev.Player)
                    || definition.PrefRoles.Any(antag => CanBeAntag(ev.Player, ev.Profile, antag)))
                    continue;

                sessions.Remove(ev.Player);
                Log.Info($"Released {ev.Player} from antag preselection, {ev.Profile.Name} does not fit it.");
            }
        }
    }

    private void OnNoJobsAvailable(NoJobsAvailableSpawningEvent ev)
    {
        if (!_prefs.TryGetCachedPreferences(ev.Player.UserId, out var prefs)
            || GetActiveCharacters(prefs, GetQueue(ev.Player.UserId, prefs)).Any())
            return;

        _chat.DispatchServerMessage(ev.Player, Loc.GetString("character-queue-no-active-characters"));
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _spawned.Clear();
        _reservations.Clear();
    }
}
