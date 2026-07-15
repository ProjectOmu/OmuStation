using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Actions;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Dataset;
using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking;
using Content.Shared._Impstation.Thaven;
using Content.Shared._Impstation.Thaven.Components;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Shared.Administration;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server._Impstation.CCVar;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Impstation.Thaven;

public sealed partial class ThavenMoodsSystem : SharedThavenMoodSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly UserInterfaceSystem _bui = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public IReadOnlyList<ThavenMood> SharedMoods => _sharedMoods.AsReadOnly();
    private readonly List<ThavenMood> _sharedMoods = new();


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThavenMoodsComponent, ComponentStartup>(OnThavenMoodInit);
        SubscribeLocalEvent<ThavenMoodsComponent, ComponentShutdown>(OnThavenMoodShutdown);
        SubscribeLocalEvent<ThavenMoodsComponent, ToggleMoodsScreenEvent>(OnToggleMoodsScreen);
        SubscribeLocalEvent<ThavenMoodsComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<ThavenMoodsComponent, ThavenEmagDoAfterEvent>(OnEmagDoAfter);
        SubscribeLocalEvent<ThavenMoodsComponent, GetVerbsEvent<Verb>>(AddThavenAdminVerb);
        SubscribeLocalEvent<GameRuleStartedEvent>(OnGameRuleStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>((_) => _sharedMoods.Clear());
    }

    private ThavenMoodConfigPrototype GetMoodConfig(ThavenMoodsComponent comp)
    {
        return _proto.Index(comp.MoodConfig);
    }

    private void EnsureSharedMoods(ThavenMoodsComponent comp)
    {
        if (_sharedMoods.Count > 0)
            return;

        NewSharedMoods(GetMoodConfig(comp));
    }

    private void NewSharedMoods(ThavenMoodConfigPrototype config)
    {
        _sharedMoods.Clear();
        for (int i = 0; i < _config.GetCVar(ImpCCVars.ThavenSharedMoodCount); i++)
            TryAddSharedMood(config, notify: false);
    }

    public bool TryAddSharedMood(ThavenMoodConfigPrototype config, ThavenMood? mood = null, bool checkConflicts = true, bool notify = true)
    {
        if (mood == null)
        {
            if (TryPick(config.SharedDataset, out var moodProto, _sharedMoods))
            {
                mood = RollMood(moodProto);
                checkConflicts = false; // TryPick has cleared this mood already
            }
            else
            {
                return false;
            }
        }

        if (checkConflicts && (GetConflicts(_sharedMoods).Contains(mood.ProtoId) || GetMoodProtoSet(_sharedMoods).Overlaps(mood.Conflicts)))
            return false;

        _sharedMoods.Add(mood);

        if (notify)
        {
            var enumerator = EntityManager.EntityQueryEnumerator<ThavenMoodsComponent>();
            while (enumerator.MoveNext(out var ent, out var comp))
            {
                if (!comp.FollowsSharedMoods)
                    continue;

                NotifyMoodChange((ent, comp));
            }
        }

        return true;
    }

    private void OnBoundUIOpened(EntityUid uid, ThavenMoodsComponent component, BoundUIOpenedEvent args)
    {
        UpdateBUIState(uid, component);
    }

    private void OnToggleMoodsScreen(EntityUid uid, ThavenMoodsComponent component, ToggleMoodsScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _bui.TryToggleUi(uid, ThavenMoodsUiKey.Key, actor.PlayerSession);
    }

    private bool TryPick(string datasetProto, [NotNullWhen(true)] out ThavenMoodPrototype? proto, IEnumerable<ThavenMood>? currentMoods = null, HashSet<string>? conflicts = null)
    {
        var dataset = _proto.Index<DatasetPrototype>(datasetProto);
        var choices = dataset.Values.ToList();

        currentMoods ??= new HashSet<ThavenMood>();
        conflicts ??= GetConflicts(currentMoods);

        var currentMoodProtos = GetMoodProtoSet(currentMoods);

        while (choices.Count > 0)
        {
            var moodId = _random.PickAndTake(choices);
            if (conflicts.Contains(moodId))
                continue; // Skip proto if an existing mood conflicts with it

            var moodProto = _proto.Index<ThavenMoodPrototype>(moodId);
            if (moodProto.Conflicts.Overlaps(currentMoodProtos))
                continue; // Skip proto if it conflicts with an existing mood

            proto = moodProto;
            return true;
        }

        proto = null;
        return false;
    }

    public void NotifyMoodChange(Entity<ThavenMoodsComponent> ent)
    {
        if (!TryComp<ActorComponent>(ent.Owner, out var actor))
            return;

        if (ent.Comp.MoodsChangedSound != null)
            _audio.PlayGlobal(ent.Comp.MoodsChangedSound, actor.PlayerSession);

        var msg = Loc.GetString("thaven-moods-update-notify");
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", msg));
        _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Orange);
    }

    public void UpdateBUIState(EntityUid uid, ThavenMoodsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        var state = new ThavenMoodsBuiState(comp.Moods, comp.FollowsSharedMoods ? _sharedMoods : []);
        _bui.SetUiState(uid, ThavenMoodsUiKey.Key, state);
    }

    public void AddMood(EntityUid uid, ThavenMood mood, ThavenMoodsComponent? comp = null, bool notify = true)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Moods.Add(mood);

        if (notify)
            NotifyMoodChange((uid, comp));

        UpdateBUIState(uid, comp);
    }

    /// <summary>
    /// Creates a ThavenMood instance from the given ThavenMoodPrototype, and rolls
    /// its mood vars.
    /// </summary>
    public ThavenMood RollMood(ThavenMoodPrototype proto)
    {
        var mood = new ThavenMood()
        {
            ProtoId = proto.ID,
            MoodName = proto.MoodName,
            MoodDesc = proto.MoodDesc,
            Conflicts = proto.Conflicts,
        };

        var alreadyChosen = new HashSet<string>();

        foreach (var (name, datasetID) in proto.MoodVarDatasets)
        {
            var dataset = _proto.Index<DatasetPrototype>(datasetID);

            if (proto.AllowDuplicateMoodVars)
            {
                mood.MoodVars.Add(name, _random.Pick(dataset));
                continue;
            }

            var choices = dataset.Values.ToList();
            var foundChoice = false;
            while (choices.Count > 0)
            {
                var choice = _random.PickAndTake(choices);
                if (alreadyChosen.Contains(choice))
                    continue;

                mood.MoodVars.Add(name, choice);
                alreadyChosen.Add(choice);
                foundChoice = true;
                break;
            }

            if (!foundChoice)
            {
                Log.Warning($"Ran out of choices for moodvar \"{name}\" in \"{proto.ID}\"! Picking a duplicate...");
                mood.MoodVars.Add(name, _random.Pick(dataset));
            }
        }

        return mood;
    }

    /// <summary>
    /// Checks if the given mood prototype conflicts with the current moods, and
    /// adds the mood if it does not.
    /// </summary>
    public bool TryAddMood(EntityUid uid, ThavenMoodPrototype moodProto, ThavenMoodsComponent? comp = null, bool allowConflict = false, bool notify = true)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (!allowConflict && GetConflicts(uid, comp).Contains(moodProto.ID))
            return false;

        AddMood(uid, RollMood(moodProto), comp, notify);
        return true;
    }

    public bool TryAddRandomMood(EntityUid uid, string datasetProto, ThavenMoodsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        if (TryPick(datasetProto, out var moodProto, GetActiveMoods(uid, comp)))
        {
            AddMood(uid, RollMood(moodProto), comp);
            return true;
        }

        return false;
    }

    public bool TryAddRandomMood(EntityUid uid, ThavenMoodsComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

var datasetProto = _proto.Index(GetMoodConfig(comp).RandomMoodDataset).Pick();

        return TryAddRandomMood(uid, datasetProto, comp);
    }

    public void SetMoods(EntityUid uid, IEnumerable<ThavenMood> moods, ThavenMoodsComponent? comp = null, bool notify = true)
    {
        if (!Resolve(uid, ref comp))
            return;

        comp.Moods = moods.ToList();
        if (notify)
            NotifyMoodChange((uid, comp));

        UpdateBUIState(uid, comp);
    }

    public void SetSharedMoods(IEnumerable<ThavenMood> moods)
    {
        _sharedMoods.Clear();
        _sharedMoods.AddRange(moods);

        var enumerator = EntityManager.EntityQueryEnumerator<ThavenMoodsComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (!comp.FollowsSharedMoods)
                continue;

            NotifyMoodChange((uid, comp));
            UpdateBUIState(uid, comp);
        }
    }

    public HashSet<string> GetConflicts(IEnumerable<ThavenMood> moods)
    {
        var conflicts = new HashSet<string>();

        foreach (var mood in moods)
        {
            conflicts.Add(mood.ProtoId); // Specific moods shouldn't be added twice
            conflicts.UnionWith(mood.Conflicts);
        }

        return conflicts;
    }

    public HashSet<string> GetConflicts(EntityUid uid, ThavenMoodsComponent? moods = null)
    {
        // TODO: Should probably cache this when moods get updated

        if (!Resolve(uid, ref moods))
            return new();

        var conflicts = GetConflicts(GetActiveMoods(uid, moods));

        return conflicts;
    }

    public HashSet<string> GetMoodProtoSet(IEnumerable<ThavenMood> moods)
    {
        var moodProtos = new HashSet<string>();
        foreach (var mood in moods)
            if (!string.IsNullOrEmpty(mood.ProtoId))
                moodProtos.Add(mood.ProtoId);
        return moodProtos;
    }

    /// <summary>
    /// Return a list of the moods that are affecting this entity.
    /// </summary>
    public List<ThavenMood> GetActiveMoods(EntityUid uid, ThavenMoodsComponent? comp = null, bool includeShared = true)
    {
        if (!Resolve(uid, ref comp))
            return [];

        if (includeShared && comp.FollowsSharedMoods)
        {
            return new List<ThavenMood>(SharedMoods.Concat(comp.Moods));
        }
        else
        {
            return comp.Moods;
        }
    }

    private void OnThavenMoodInit(EntityUid uid, ThavenMoodsComponent comp, ComponentStartup args)
    {
        if (comp.LifeStage != ComponentLifeStage.Starting)
            return;

        EnsureSharedMoods(comp);
        var config = GetMoodConfig(comp);

        // "Yes, and" moods
        if (TryPick(config.YesAndDataset, out var mood, GetActiveMoods(uid, comp)))
            TryAddMood(uid, mood, comp, true, false);

        // "No, and" moods
        if (TryPick(config.NoAndDataset, out mood, GetActiveMoods(uid, comp)))
            TryAddMood(uid, mood, comp, true, false);

        comp.Action = _actions.AddAction(uid, config.ViewMoodsAction);
    }

    private void OnThavenMoodShutdown(EntityUid uid, ThavenMoodsComponent comp, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, comp.Action);
    }

    // Begin DeltaV: thaven mood upsets
    public void AddWildcardMood(Entity<ThavenMoodsComponent> ent)
    {
        TryAddRandomMood(ent.Owner, GetMoodConfig(ent.Comp).WildcardDataset, ent.Comp);
    }
    // End DeltaV: thaven mood upsets

    protected override void OnEmagged(Entity<ThavenMoodsComponent> ent, ref GotEmaggedEvent args)
    {
        // Only the standard emag (screwdriver-style interaction) triggers wild moods.
        // Access breakers and other emag types are ignored.
        if (args.Type != EmagType.Interaction)
            return;

        // Cap how many times this Thaven can be wild-mood emagged.
        if (ent.Comp.WildMoodEmagCount >= ent.Comp.MaxWildMoodEmags)
            return;

        base.OnEmagged(ent, ref args);
        if (!args.Handled)
            return;

        var config = GetMoodConfig(ent.Comp);
        var doAfterArgs = new DoAfterArgs(EntityManager, args.UserUid, TimeSpan.FromSeconds(config.EmagDoAfterSeconds), new ThavenEmagDoAfterEvent(), ent.Owner, target: ent.Owner, used: args.EmagUid)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        };
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnEmagDoAfter(Entity<ThavenMoodsComponent> ent, ref ThavenEmagDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var config = GetMoodConfig(ent.Comp);
        AddWildcardMood(ent);
        _damageable.TryChangeDamage(ent.Owner, config.EmagDamage);
    }

    private void AddThavenAdminVerb(Entity<ThavenMoodsComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!_adminManager.HasAdminFlag(args.User, AdminFlags.Admin))
            return;

        if (!_playerManager.TryGetSessionByEntity(args.User, out var session))
            return;

        var comp = ent.Comp;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("thaven-moods-admin-verb"),
            Category = VerbCategory.Admin,
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () =>
            {
                var eui = new ThavenMoodsEui(this, EntityManager, _adminManager);
                _euiManager.OpenEui(eui, session);
                eui.UpdateMoods(comp, ent.Owner);
            }
        });
    }

    /// <summary>
    /// When an ion storm starts, trigger a wildcard mood on all Thavens present.
    /// This replicates the noosphere storm mechanic from the original fork.
    /// </summary>
    private void OnGameRuleStarted(ref GameRuleStartedEvent args)
    {
        if (!HasComp<IonStormRuleComponent>(args.RuleEntity))
            return;

        var query = EntityQueryEnumerator<ThavenMoodsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            AddWildcardMood((uid, comp));
        }
    }
}

