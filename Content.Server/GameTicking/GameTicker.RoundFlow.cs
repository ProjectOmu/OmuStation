// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Server.Announcements;
using Content.Server.Discord;
using Content.Server.GameTicking.Events;
using Content.Server.Roles;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles.Components;
using JetBrains.Annotations;
using Prometheus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Audio;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

using Content.Server.Maps;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        [Dependency] private readonly DiscordWebhook _discord = default!;
        [Dependency] private readonly ITaskManager _taskManager = default!;

        private static readonly Counter RoundNumberMetric = Metrics.CreateCounter(
            "ss14_round_number",
            "Round number.");

        private static readonly Gauge RoundLengthMetric = Metrics.CreateGauge(
            "ss14_round_length",
            "Round length in seconds.");

#if EXCEPTION_TOLERANCE
        [ViewVariables]
        private int _roundStartFailCount = 0;
#endif

        [ViewVariables]
        private bool _startingRound;

        [ViewVariables]
        private GameRunLevel _runLevel;

        private RoundEndMessageEvent.RoundEndPlayerInfo[]? _replayRoundPlayerInfo;

        private string? _replayRoundText;

        [ViewVariables]
        public GameRunLevel RunLevel
        {
            get => _runLevel;
            private set
            {
                // Game admins can run `restartroundnow` while still in-lobby, which'd break things with this check.
                // if (_runLevel == value) return;

                var old = _runLevel;
                _runLevel = value;

                RaiseLocalEvent(new GameRunLevelChangedEvent(old, value));
            }
        }

        /// <summary>
        /// Returns true if the round's map is eligible to be updated.
        /// </summary>
        /// <returns></returns>
        public bool CanUpdateMap()
        {
            return RunLevel == GameRunLevel.PreRoundLobby &&
                   _roundStartTime - RoundPreloadTime > _gameTiming.CurTime;
        }

        /// <summary>
        ///     Loads all the maps for the given round.
        /// </summary>
        /// <remarks>
        ///     Must be called before the runlevel is set to InRound.
        /// </remarks>
        private void LoadMaps()
        {
            if (_mapManager.MapExists(DefaultMap))
                return;

            AddGamePresetRules();

            var maps = new List<GameMapPrototype>();

            // the map might have been force-set by something
            // (i.e. votemap or forcemap)
            var mainStationMap = _gameMapManager.GetSelectedMap();
            if (mainStationMap == null)
            {
                // otherwise set the map using the config rules
                _gameMapManager.SelectMapByConfigRules();
                mainStationMap = _gameMapManager.GetSelectedMap();
            }

            // Small chance the above could return no map.
            // ideally SelectMapByConfigRules will always find a valid map
            if (mainStationMap != null)
            {
                maps.Add(mainStationMap);
            }
            else
            {
                throw new Exception("invalid config; couldn't select a valid station map!");
            }

            if (CurrentPreset?.MapPool != null &&
                _prototypeManager.TryIndex<GameMapPoolPrototype>(CurrentPreset.MapPool, out var pool) &&
                !pool.Maps.Contains(mainStationMap.ID))
            {
                var msg = Loc.GetString("game-ticker-start-round-invalid-map",
                    ("map", mainStationMap.MapName),
                    ("mode", Loc.GetString(CurrentPreset.ModeTitle)));
                Log.Debug(msg);
                SendServerMessage(msg);
            }

            // Let game rules dictate what maps we should load.
            RaiseLocalEvent(new LoadingMapsEvent(maps));

            if (maps.Count == 0)
            {
                _map.CreateMap(out var mapId, runMapInit: false);
                DefaultMap = mapId;
                return;
            }

            for (var i = 0; i < maps.Count; i++)
            {
                LoadGameMap(maps[i], out var mapId);
                DebugTools.Assert(!_map.IsInitialized(mapId));

                if (i == 0)
                    DefaultMap = mapId;
            }
        }

        public PreGameMapLoad RaisePreLoad(
            GameMapPrototype proto,
            DeserializationOptions? opts = null,
            Vector2? offset = null,
            Angle? rot = null)
        {
            offset ??= proto.MaxRandomOffset != 0f
                ? _robustRandom.NextVector2(proto.MaxRandomOffset)
                : Vector2.Zero;

            rot ??= proto.RandomRotation
                ? _robustRandom.NextAngle()
                : Angle.Zero;

            opts ??= DeserializationOptions.Default;
            var ev = new PreGameMapLoad(proto, opts.Value, offset.Value, rot.Value);
            RaiseLocalEvent(ev);
            return ev;
        }

        /// <summary>
        ///     Loads a new map, allowing systems interested in it to handle loading events.
        ///     In the base game, this is required to be used if you want to load a station.
        ///     This does not initialze maps, unles specified via the <see cref="DeserializationOptions"/>.
        /// </summary>
        /// <remarks>
        /// This is basically a wrapper around a <see cref="MapLoaderSystem"/> method that auto generate
        /// some <see cref="MapLoadOptions"/> using information in a prototype, and raise some events to allow content
        /// to modify the options and react to the map creation.
        /// </remarks>
        /// <param name="proto">Game map prototype to load in.</param>
        /// <param name="mapId">The id of the map that was loaded.</param>
        /// <param name="options">Entity loading options, including whether the maps should be initialized.</param>
        /// <param name="stationName">Name to assign to the loaded station.</param>
        /// <returns>All loaded entities and grids.</returns>
        public IReadOnlyList<EntityUid> LoadGameMap(
            GameMapPrototype proto,
            out MapId mapId,
            DeserializationOptions? options = null,
            string? stationName = null,
            Vector2? offset = null,
            Angle? rot = null)
        {
            var ev = RaisePreLoad(proto, options, offset, rot);

            if (ev.GameMap.IsGrid)
            {
                var mapUid = _map.CreateMap(out mapId, runMapInit: options?.InitializeMaps ?? false);
                if (!_loader.TryLoadGrid(mapId,
                        ev.GameMap.MapPath,
                        out var grid,
                        ev.Options,
                        ev.Offset,
                        ev.Rotation))
                {
                    throw new Exception($"Failed to load game-map grid {ev.GameMap.ID}");
                }

                _metaData.SetEntityName(mapUid, proto.MapName);
                var g = new List<EntityUid> {grid.Value.Owner};
                RaiseLocalEvent(new PostGameMapLoad(proto, mapId, g, stationName));
                return g;
            }

            if (!_loader.TryLoadMap(ev.GameMap.MapPath,
                    out var map,
                    out var grids,
                    ev.Options,
                    ev.Offset,
                    ev.Rotation))
            {
                throw new Exception($"Failed to load game map {ev.GameMap.ID}");
            }

            mapId = map.Value.Comp.MapId;
            _metaData.SetEntityName(map.Value.Owner, proto.MapName);
            var gridUids = grids.Select(x => x.Owner).ToList();
            RaiseLocalEvent(new PostGameMapLoad(proto, mapId, gridUids, stationName));
            return gridUids;
        }

        /// <summary>
        /// Variant of <see cref="LoadGameMap"/> that attempts to assign the provided <see cref="MapId"/> to the
        /// loaded map.
        /// </summary>
        public IReadOnlyList<EntityUid> LoadGameMapWithId(
            GameMapPrototype proto,
            MapId mapId,
            DeserializationOptions? opts = null,
            string? stationName = null,
            Vector2? offset = null,
            Angle? rot = null)
        {
            var ev = RaisePreLoad(proto, opts, offset, rot);

            if (ev.GameMap.IsGrid)
            {
                var mapUid = _map.CreateMap(mapId);
                if (!_loader.TryLoadGrid(mapId,
                        ev.GameMap.MapPath,
                        out var grid,
                        ev.Options,
                        ev.Offset,
                        ev.Rotation))
                {
                    throw new Exception($"Failed to load game-map grid {ev.GameMap.ID}");
                }

                _metaData.SetEntityName(mapUid, proto.MapName);
                var g = new List<EntityUid> {grid.Value.Owner};
                RaiseLocalEvent(new PostGameMapLoad(proto, mapId, g, stationName));
                return g;
            }

            if (!_loader.TryLoadMapWithId(
                    mapId,
                    ev.GameMap.MapPath,
                    out var map,
                    out var grids,
                    ev.Options,
                    ev.Offset,
                    ev.Rotation))
            {
                throw new Exception($"Failed to load map");
            }

            _metaData.SetEntityName(map.Value.Owner, proto.MapName);
            var gridUids = grids.Select(x => x.Owner).ToList();
            RaiseLocalEvent(new PostGameMapLoad(proto, mapId, gridUids, stationName));
            return gridUids;
        }

        /// <summary>
        /// Variant of <see cref="LoadGameMap"/> that loads and then merges a game map onto an existing map.
        /// </summary>
        public IReadOnlyList<EntityUid> MergeGameMap(
            GameMapPrototype proto,
            MapId targetMap,
            DeserializationOptions? opts = null,
            string? stationName = null,
            Vector2? offset = null,
            Angle? rot = null)
        {
            // TODO MAP LOADING use a new event?
            // This is quite different from the other methods, which will actually create a **new** map.
            var ev = RaisePreLoad(proto, opts, offset, rot);

            if (ev.GameMap.IsGrid)
            {
                if (!_loader.TryLoadGrid(targetMap,
                        ev.GameMap.MapPath,
                        out var grid,
                        ev.Options,
                        ev.Offset,
                        ev.Rotation))
                {
                    throw new Exception($"Failed to load game-map grid {ev.GameMap.ID}");
                }

                var g = new List<EntityUid> {grid.Value.Owner};
                // TODO MAP LOADING use a new event?
                RaiseLocalEvent(new PostGameMapLoad(proto, targetMap, g, stationName));
                return g;
            }

            if (!_loader.TryMergeMap(targetMap,
                    ev.GameMap.MapPath,
                    out var grids,
                    ev.Options,
                    ev.Offset,
                    ev.Rotation))
            {
                throw new Exception($"Failed to load map");
            }

            var gridUids = grids.Select(x => x.Owner).ToList();

            // TODO MAP LOADING use a new event?
            RaiseLocalEvent(new PostGameMapLoad(proto, targetMap, gridUids, stationName));
            return gridUids;
        }

        public int ReadyPlayerCount()
        {
            var total = 0;
            foreach (var (userId, status) in _playerGameStatuses)
            {
                if (LobbyEnabled && status == PlayerGameStatus.NotReadyToPlay)
                    continue;

                if (!_playerManager.TryGetSessionById(userId, out _))
                    continue;

                total++;
            }

            return total;
        }

        public void StartRound(bool force = false)
        {
#if EXCEPTION_TOLERANCE
            try
            {
#endif
            // If this game ticker is a dummy or the round is already being started, do nothing!
            if (DummyTicker || _startingRound)
                return;

            _startingRound = true;

            if (RoundId == 0)
                IncrementRoundNumber();

            ReplayStartRound();

            DebugTools.Assert(RunLevel == GameRunLevel.PreRoundLobby);
            _sawmill.Info("Starting round!");

            SendServerMessage(Loc.GetString("game-ticker-start-round"));

            var readyPlayers = new List<ICommonSession>();
            var readyPlayerProfiles = new Dictionary<NetUserId, HumanoidCharacterProfile>();
            var autoDeAdmin = _cfg.GetCVar(CCVars.AdminDeadminOnJoin);
            foreach (var (userId, status) in _playerGameStatuses)
            {
                if (LobbyEnabled && status != PlayerGameStatus.ReadyToPlay) continue;
                if (!_playerManager.TryGetSessionById(userId, out var session)) continue;

                if (autoDeAdmin && _adminManager.IsAdmin(session))
                {
                    _adminManager.DeAdmin(session);
                }
#if DEBUG
                DebugTools.Assert(_userDb.IsLoadComplete(session), $"Player was readied up but didn't have user DB data loaded yet??");
#endif

                readyPlayers.Add(session);
                HumanoidCharacterProfile profile;
                if (_prefsManager.TryGetCachedPreferences(userId, out var preferences))
                {
                    profile = (HumanoidCharacterProfile) preferences.SelectedCharacter;
                }
                else
                {
                    var speciesToBlacklist =
                        new HashSet<string>(_cfg.GetCVar(CCVars.ICNewAccountSpeciesBlacklist).Split(","));
                    profile = HumanoidCharacterProfile.Random(speciesToBlacklist);
                }
                readyPlayerProfiles.Add(userId, profile);
            }

            DebugTools.AssertEqual(readyPlayers.Count, ReadyPlayerCount());

            // Just in case it hasn't been loaded previously we'll try loading it.
            LoadMaps();

            // map has been selected so update the lobby info text
            // applies to players who didn't ready up
            UpdateInfoText();

            StartGamePresetRules();

            RoundLengthMetric.Set(0);

            var startingEvent = new RoundStartingEvent(RoundId);
            RaiseLocalEvent(startingEvent);

            var origReadyPlayers = readyPlayers.ToArray();

            if (!StartPreset(origReadyPlayers, force))
            {
                _startingRound = false;
                return;
            }

            // MapInitialize *before* spawning players, our codebase is too shit to do it afterwards...
            _map.InitializeMap(DefaultMap);

            SpawnPlayers(readyPlayers, readyPlayerProfiles, force);

            _roundStartDateTime = DateTime.UtcNow;
            RunLevel = GameRunLevel.InRound;

            RoundStartTimeSpan = _gameTiming.CurTime;
            SendStatusToAll();
            ReqWindowAttentionAll();
            UpdateLateJoinStatus();
            AnnounceRound();
            UpdateInfoText();
            SendRoundStartedDiscordMessage();

#if EXCEPTION_TOLERANCE
            }
            catch (Exception e)
            {
                _roundStartFailCount++;

                if (RoundStartFailShutdownCount > 0 && _roundStartFailCount >= RoundStartFailShutdownCount)
                {
                    _sawmill.Fatal($"Failed to start a round {_roundStartFailCount} time(s) in a row... Shutting down!");
                    _runtimeLog.LogException(e, nameof(GameTicker));
                    _baseServer.Shutdown("Restarting server");
                    return;
                }

                _sawmill.Error($"Exception caught while trying to start the round! Restarting round...");
                _runtimeLog.LogException(e, nameof(GameTicker));
                _startingRound = false;
                RestartRound();
                return;
            }

            // Round started successfully! Reset counter...
            _roundStartFailCount = 0;
#endif
            _startingRound = false;
        }

        private void RefreshLateJoinAllowed()
        {
            var refresh = new RefreshLateJoinAllowedEvent();
            RaiseLocalEvent(refresh);
            // This used to assign the event's result outright, which silently discarded the
            // admin/config setting from CCVars.GameDisallowLateJoins on every refresh (and the event has
            // no subscribers, so the discarded value was always replaced with false). A subscriber may
            // only ever *tighten* the gate, never loosen one an admin has closed.
            DisallowLateJoin = _cfg.GetCVar(CCVars.GameDisallowLateJoins) || refresh.DisallowLateJoin;
        }

        public void EndRound(string text = "")
        {
            // If this game ticker is a dummy, do nothing!
            if (DummyTicker)
                return;

            DebugTools.Assert(RunLevel == GameRunLevel.InRound);
            _sawmill.Info("Ending round!");

            RunLevel = GameRunLevel.PostRound;

            try
            {
                ShowRoundEndScoreboard(text);
            }
            catch (Exception e)
            {
                Log.Error($"Error while showing round end scoreboard: {e}");
            }

            try
            {
                SendRoundEndDiscordMessage();
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending round end Discord message: {e}");
            }
        }

        public void ShowRoundEndScoreboard(string text = "")
        {
            // Log end of round
            _adminLogger.Add(LogType.EmergencyShuttle, LogImpact.High, $"Round ended, showing summary");

            //Tell every client the round has ended.
            var gamemodeTitle = CurrentPreset != null ? Loc.GetString(CurrentPreset.ModeTitle) : string.Empty;

            // Let things add text here.
            var textEv = new RoundEndTextAppendEvent();
            RaiseLocalEvent(textEv);

            var roundEndText = $"{text}\n{textEv.Text}";

            //Get the timespan of the round.
            var roundDuration = RoundDuration();

            //Generate a list of basic player info to display in the end round summary.
            var listOfPlayerInfo = new List<RoundEndMessageEvent.RoundEndPlayerInfo>();
            // Grab the great big book of all the Minds, we'll need them for this.
            var allMinds = EntityQueryEnumerator<MindComponent>();
            var pvsOverride = _cfg.GetCVar(CCVars.RoundEndPVSOverrides);
            while (allMinds.MoveNext(out var mindId, out var mind))
            {
                // TODO don't list redundant observer roles?
                // I.e., if a player was an observer ghost, then a hamster ghost role, maybe just list hamster and not
                // the observer role?
                var userId = mind.UserId ?? mind.OriginalOwnerUserId;

                var connected = false;
                var observer = _roles.MindHasRole<ObserverRoleComponent>(mindId);
                // Continuing
                if (userId != null && _playerManager.ValidSessionId(userId.Value))
                {
                    connected = true;
                }
                ContentPlayerData? contentPlayerData = null;
                if (userId != null && _playerManager.TryGetPlayerData(userId.Value, out var playerData))
                {
                    contentPlayerData = playerData.ContentData();
                }
                // Finish

                var antag = _roles.MindIsAntagonist(mindId);

                var playerIcName = "Unknown";

                if (mind.CharacterName != null)
                    playerIcName = mind.CharacterName;
                else if (mind.CurrentEntity != null && TryName(mind.CurrentEntity.Value, out var icName))
                    playerIcName = icName;

                if (TryGetEntity(mind.OriginalOwnedEntity, out var entity) && pvsOverride)
                {
                    _pvsOverride.AddGlobalOverride(entity.Value);
                }

                var roles = _roles.MindGetAllRoleInfo(mindId);

                var playerEndRoundInfo = new RoundEndMessageEvent.RoundEndPlayerInfo()
                {
                    // Note that contentPlayerData?.Name sticks around after the player is disconnected.
                    // This is as opposed to ply?.Name which doesn't.
                    PlayerOOCName = contentPlayerData?.Name ?? "(IMPOSSIBLE: REGISTERED MIND WITH NO OWNER)",
                    // Character name takes precedence over current entity name
                    PlayerICName = playerIcName,
                    PlayerGuid = userId,
                    PlayerNetEntity = GetNetEntity(entity),
                    Role = antag
                        ? roles.First(role => role.Antagonist).Name
                        : roles.FirstOrDefault().Name ?? Loc.GetString("game-ticker-unknown-role"),
                    Antag = antag,
                    JobPrototypes = roles.Where(role => !role.Antagonist).Select(role => role.Prototype).ToArray(),
                    AntagPrototypes = roles.Where(role => role.Antagonist).Select(role => role.Prototype).ToArray(),
                    Observer = observer,
                    Connected = connected,
                };

                // Let content in any assembly annotate this player's entry with structured data.
                // Do NOT add fork-specific fields above; subscribe to RoundEndPlayerInfoEvent from
                // your own assembly instead.
                var playerInfoEv = new RoundEndPlayerInfoEvent(mindId, mind, playerEndRoundInfo);
                RaiseLocalEvent(playerInfoEv);

                listOfPlayerInfo.Add(playerInfoEv.Info);
            }

            // This ordering mechanism isn't great (no ordering of minds) but functions
            var listOfPlayerInfoFinal = listOfPlayerInfo.OrderBy(pi => pi.PlayerOOCName).ToArray();
            var sound = RoundEndSoundCollection == null ? null : _audio.ResolveSound(new SoundCollectionSpecifier(RoundEndSoundCollection));

            var roundEndMessageEvent = new RoundEndMessageEvent(
                gamemodeTitle,
                roundEndText,
                roundDuration,
                RoundId,
                listOfPlayerInfoFinal.Length,
                listOfPlayerInfoFinal,
                sound
            );
            RaiseNetworkEvent(roundEndMessageEvent);
            RaiseLocalEvent(roundEndMessageEvent);

            _replayRoundPlayerInfo = listOfPlayerInfoFinal;
            _replayRoundText = roundEndText;
        }

        private async void SendRoundEndDiscordMessage()
        {
            try
            {
                if (_webhookIdentifier == null)
                    return;

                var duration = RoundDuration();
                var content = Loc.GetString("discord-round-notifications-end",
                    ("id", RoundId),
                    ("hours", Math.Truncate(duration.TotalHours)),
                    ("minutes", duration.Minutes),
                    ("seconds", duration.Seconds));
                var payload = new WebhookPayload { Content = content };

                await _discord.CreateMessage(_webhookIdentifier.Value, payload);

                if (DiscordRoundEndRole == null)
                    return;

                content = Loc.GetString("discord-round-notifications-end-ping", ("roleId", DiscordRoundEndRole));
                payload = new WebhookPayload { Content = content };
                payload.AllowedMentions.AllowRoleMentions();

                await _discord.CreateMessage(_webhookIdentifier.Value, payload);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord round end message:\n{e}");
            }
        }

        public void RestartRound()
        {
            // If this game ticker is a dummy, do nothing!
            if (DummyTicker)
                return;

            ReplayEndRound();

            // Handle restart for server update
            if (_serverUpdates.RoundEnded())
                return;

            // Check if the GamePreset needs to be reset
            TryResetPreset();

            _sawmill.Info("Restarting round!");

            SendServerMessage(Loc.GetString("game-ticker-restart-round"));

            RoundNumberMetric.Inc();

            PlayersJoinedRoundNormally = 0;

            RunLevel = GameRunLevel.PreRoundLobby;
            RandomizeLobbyBackground();
            ResettingCleanup();
            IncrementRoundNumber();
            SendRoundStartingDiscordMessage();

            if (!LobbyEnabled)
            {
                StartRound();
            }
            else
            {
                if (_playerManager.PlayerCount == 0)
                    _roundStartCountdownHasNotStartedYetDueToNoPlayers = true;
                else
                    _roundStartTime = _gameTiming.CurTime + LobbyDuration;

                SendStatusToAll();
                UpdateInfoText();

                ReqWindowAttentionAll();
            }
        }

        private async void SendRoundStartingDiscordMessage()
        {
            try
            {
                if (_webhookIdentifier == null)
                    return;

                var content = Loc.GetString("discord-round-notifications-new");

                var payload = new WebhookPayload { Content = content };

                await _discord.CreateMessage(_webhookIdentifier.Value, payload);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord round starting message:\n{e}");
            }
        }

        /// <summary>
        ///     Cleanup that has to run to clear up anything from the previous round.
        ///     Stuff like wiping the previous map clean.
        /// </summary>
        private void ResettingCleanup()
        {
            // Move everybody currently in the server to lobby.
            foreach (var player in _playerManager.Sessions)
            {
                PlayerJoinLobby(player);
            }

            // Round restart cleanup event, so entity systems can reset.
            var ev = new RoundRestartCleanupEvent();
            RaiseLocalEvent(ev);

            // So clients' entity systems can clean up too...
            RaiseNetworkEvent(ev);

            EntityManager.FlushEntities();

            _mapManager.Restart();

            _banManager.Restart();

            _gameMapManager.ClearSelectedMap();

            // Clear up any game rules.
            ClearGameRules();
            CurrentPreset = null;

            _allPreviousGameRules.Clear();

            DisallowLateJoin = false;
            _playerGameStatuses.Clear();
            foreach (var session in _playerManager.Sessions)
            {
                _playerGameStatuses[session.UserId] = LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;
            }
        }

        public bool DelayStart(TimeSpan time)
        {
            if (_runLevel != GameRunLevel.PreRoundLobby)
            {
                return false;
            }

            _roundStartTime += time;

            RaiseNetworkEvent(new TickerLobbyCountdownEvent(_roundStartTime, Paused));

            _chatManager.DispatchServerAnnouncement(Loc.GetString("game-ticker-delay-start", ("seconds", time.TotalSeconds)));

            return true;
        }

        private void UpdateRoundFlow(float frameTime)
        {
            if (RunLevel == GameRunLevel.InRound)
            {
                RoundLengthMetric.Inc(frameTime);
            }

            if (_roundStartTime == TimeSpan.Zero ||
                RunLevel != GameRunLevel.PreRoundLobby ||
                Paused ||
                _roundStartTime - RoundPreloadTime > _gameTiming.CurTime ||
                _roundStartCountdownHasNotStartedYetDueToNoPlayers)
            {
                return;
            }

            if (_roundStartTime < _gameTiming.CurTime)
            {
                StartRound();
            }
            // Preload maps so we can start faster
            else if (_roundStartTime - RoundPreloadTime < _gameTiming.CurTime)
            {
                LoadMaps();
            }
        }

        private void AnnounceRound()
        {
            if (CurrentPreset == null) return;

            var options = _prototypeManager.EnumeratePrototypes<RoundAnnouncementPrototype>().ToList();

            if (options.Count == 0)
                return;

            var proto = _robustRandom.Pick(options);

            if (proto.Message != null)
                _chatSystem.DispatchGlobalAnnouncement(Loc.GetString(proto.Message), playSound: true);

            if (proto.Sound != null)
                _audio.PlayGlobal(proto.Sound, Filter.Broadcast(), true);
        }

        private async void SendRoundStartedDiscordMessage()
        {
            try
            {
                if (_webhookIdentifier == null)
                    return;

                var mapName = _gameMapManager.GetSelectedMap()?.MapName ?? Loc.GetString("discord-round-notifications-unknown-map");
                var content = Loc.GetString("discord-round-notifications-started", ("id", RoundId), ("map", mapName));

                var payload = new WebhookPayload { Content = content };

                await _discord.CreateMessage(_webhookIdentifier.Value, payload);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending discord round start message:\n{e}");
            }
        }
    }

    public enum GameRunLevel
    {
        PreRoundLobby = 0,
        InRound = 1,
        PostRound = 2
    }

    public sealed class GameRunLevelChangedEvent
    {
        public GameRunLevel Old { get; }
        public GameRunLevel New { get; }

        public GameRunLevelChangedEvent(GameRunLevel old, GameRunLevel @new)
        {
            Old = old;
            New = @new;
        }
    }

    /// <summary>
    ///     Event raised before maps are loaded in pre-round setup.
    ///     Contains a list of game map prototypes to load; modify it if you want to load different maps,
    ///     for example as part of a game rule.
    /// </summary>
    [PublicAPI]
    public sealed class LoadingMapsEvent : EntityEventArgs
    {
        public List<GameMapPrototype> Maps;

        public LoadingMapsEvent(List<GameMapPrototype> maps)
        {
            Maps = maps;
        }
    }

    /// <summary>
    ///     Event raised before the game loads a given map.
    ///     This event is mutable, and load options should be tweaked if necessary.
    ///     This is the hook for altering a map per-round (offset, rotation, deserialization options) - reach for
    ///     it instead of special-casing map loading somewhere else.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     <b>This event currently has no subscribers anywhere in the tree.</b> If you change its shape or stop
    ///     raising it, nothing will fail.
    ///     </para>
    ///     <para>
    ///     No subscriber of this event declares <c>before:</c>/<c>after:</c> ordering, so dispatch takes the
    ///     unordered path and resolution is system registration order. If your subscriber genuinely has to run
    ///     after <c>StationSystem</c>, ordering has to be declared on <i>every</i> subscriber of this event, not
    ///     just yours - and declaring any ordering switches the whole event onto the ordered dispatch path.
    ///     </para>
    /// </remarks>
    [PublicAPI]
    public sealed class PreGameMapLoad(GameMapPrototype gameMap, DeserializationOptions options, Vector2 offset, Angle rotation) : EntityEventArgs
    {
        public readonly GameMapPrototype GameMap = gameMap;
        public DeserializationOptions Options = options;
        public Vector2 Offset = offset;
        public Angle Rotation = rotation;
    }

    /// <summary>
    ///     Event raised after the game loads a given map.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     This used to say "you likely want to subscribe to this after StationSystem". That was aspirational.
    ///     Neither of the two subscribers (<c>StationSystem</c> and <c>GridPreloaderSystem</c>) declares any
    ///     <c>before:</c>/<c>after:</c> ordering, so this event takes the unordered dispatch path and the order
    ///     handlers run in is system registration order.
    ///     </para>
    ///     <para>
    ///     If your subscriber genuinely depends on <c>StationSystem</c> having already run, do not rely on that
    ///     - either declare ordering on <i>every</i> subscriber of this event (ordering declared on one
    ///     subscription alone does not order the others, and adding any ordering switches the whole event onto
    ///     the ordered dispatch path), or move the dependent work to a later event, which is what
    ///     <c>EmergencyShuttleSystem</c> does.
    ///     </para>
    /// </remarks>
    [PublicAPI]
    public sealed class PostGameMapLoad : EntityEventArgs
    {
        public readonly GameMapPrototype GameMap;
        public readonly MapId Map;
        public readonly IReadOnlyList<EntityUid> Grids;
        public readonly string? StationName;

        public PostGameMapLoad(GameMapPrototype gameMap, MapId map, IReadOnlyList<EntityUid> grids, string? stationName)
        {
            GameMap = gameMap;
            Map = map;
            Grids = grids;
            StationName = stationName;
        }
    }

    /// <summary>
    ///     Event raised to refresh the late join status.
    ///     If you want to disallow late joins, listen to this and call Disallow.
    ///     This is the hook for mode-specific late-join rules - use it instead of writing to
    ///     <see cref="GameTicker.DisallowLateJoin"/>, which every refresh recomputes from scratch.
    ///     A subscriber can only ever <i>add</i> a reason to disallow: the refresh ORs this event's
    ///     result with <c>CCVars.GameDisallowLateJoins</c>, so the admin/config setting is a floor
    ///     that no subscriber can clear.
    /// </summary>
    /// <remarks>
    ///     <b>This event currently has no subscribers outside the integration tests.</b> If you change its
    ///     shape or stop raising it, only <c>Content.IntegrationTests/Tests/_Omu/LateJoinTest.cs</c> will notice.
    /// </remarks>
    public sealed class RefreshLateJoinAllowedEvent
    {
        public bool DisallowLateJoin { get; private set; } = false;

        public void Disallow()
        {
            DisallowLateJoin = true;
        }
    }

    /// <summary>
    ///     Attempt event raised on round start.
    ///     This can be listened to by GameRule systems to cancel round start if some condition is not met, like player count.
    /// </summary>
    public sealed class RoundStartAttemptEvent : CancellableEntityEventArgs
    {
        public ICommonSession[] Players { get; }
        public bool Forced { get; }

        public RoundStartAttemptEvent(ICommonSession[] players, bool forced)
        {
            Players = players;
            Forced = forced;
        }
    }

    /// <summary>
    ///     Event raised before readied up players are spawned and given jobs by the GameTicker.
    ///     You can use this to spawn people off-station, like in the case of nuke ops or wizard.
    ///     Remove the players you spawned from the PlayerPool and call <see cref="GameTicker.PlayerJoinGame"/> on them.
    /// </summary>
    public sealed class RulePlayerSpawningEvent
    {
        /// <summary>
        ///     Pool of players to be spawned.
        ///     If you want to handle a specific player being spawned, remove it from this list and do what you need.
        /// </summary>
        /// <remarks>If you spawn a player by yourself from this event, don't forget to call <see cref="GameTicker.PlayerJoinGame"/> on them.</remarks>
        public List<ICommonSession> PlayerPool { get; }
        public IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> Profiles { get; }
        public bool Forced { get; }

        public RulePlayerSpawningEvent(List<ICommonSession> playerPool, IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles, bool forced)
        {
            PlayerPool = playerPool;
            Profiles = profiles;
            Forced = forced;
        }
    }

    /// <summary>
    ///     Event raised after players were assigned jobs by the GameTicker and have been spawned in.
    ///     You can give on-station people special roles by listening to this event.
    /// </summary>
    public sealed class RulePlayerJobsAssignedEvent
    {
        public ICommonSession[] Players { get; }
        public IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> Profiles { get; }
        public bool Forced { get; }

        public RulePlayerJobsAssignedEvent(ICommonSession[] players, IReadOnlyDictionary<NetUserId, HumanoidCharacterProfile> profiles, bool forced)
        {
            Players = players;
            Profiles = profiles;
            Forced = forced;
        }
    }

    /// <summary>
    ///     Event raised to allow subscribers to add text to the round end summary screen.
    ///     For per-player structured data, use
    ///     <see cref="Content.Shared.GameTicking.RoundEndPlayerInfoEvent"/> instead.
    /// </summary>
    public sealed class RoundEndTextAppendEvent
    {
        private bool _doNewLine;

        /// <summary>
        ///     Text to display in the round end summary screen.
        /// </summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>
        ///     Invoke this method to add text to the round end summary screen.
        /// </summary>
        /// <param name="text"></param>
        public void AddLine(string text)
        {
            if (_doNewLine)
                Text += "\n";

            Text += text;
            _doNewLine = true;
        }

        // goob edit
        public void AppendAtStart(string text)
        {
            Text = text + Text;
        }
    }
}
