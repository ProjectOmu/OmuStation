// SPDX-License-Identifier: MIT

using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Database;
using Content.Shared.Dataset;
using Content.Shared.DoAfter;
using Content.Shared.Nutrition.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CrewletBrainComponent : Component
{
    [DataField]
    public float VisionRadius = 8f;

    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(0.75);

    [DataField]
    public float FollowRange = 5f;

    [DataField]
    public float FollowCloseRange = 2f;

    [DataField]
    public float CatchUpRange = 14f;

    [DataField]
    public TimeSpan ApartTime = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan RecallDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float MeleeRange = 1f;

    [DataField]
    public float InteractRange = 1.5f;

    [DataField]
    public float WanderRange = 0.5f;

    [DataField]
    public float StareRange = 0.5f;

    [DataField]
    public float MopRange = 0.2f;

    [DataField]
    public float MopRadius = 0.45f;

    [DataField]
    public TimeSpan MoveTimeout = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan DeliverTimeout = TimeSpan.FromSeconds(8);

    [DataField]
    public float FetchRange = 7f;

    [DataField]
    public TimeSpan FetchMemory = TimeSpan.FromSeconds(10);

    [DataField]
    public int MinFetchWord = 4;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> ComeWords = "CrewletComeWords";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> StayWords = "CrewletStayWords";

    [DataField]
    public TimeSpan StayTime = TimeSpan.FromSeconds(10);

    [DataField]
    public LocId CommandEcho = "crewlet-command-echo";

    [DataField]
    public float WorryDamage = 5f;

    [DataField]
    public TimeSpan WorryMemory = TimeSpan.FromSeconds(5);

    [DataField]
    public ProtoId<EmotePrototype> WorryEmote = "Crying";

    [DataField]
    public float MusicRange = 8f;

    [DataField]
    public int DanceBeats = 12;

    [DataField]
    public TimeSpan DanceBeat = TimeSpan.FromSeconds(0.7);

    [DataField]
    public ProtoId<EmotePrototype>? DanceEmote;

    [DataField]
    public string HatSlot = "head";

    [DataField]
    public List<string> PickpocketSlots = new() { "head", "eyes", "ears", "neck", "gloves", "shoes" };

    [DataField]
    public EntityWhitelist? PickpocketBlacklist;

    [DataField]
    public TimeSpan PickpocketTime = TimeSpan.FromSeconds(3);

    [DataField]
    public LocId PickpocketText = "crewlet-pickpocket";

    [DataField]
    public EntProtoId PointingArrow = "PointingArrow";

    [DataField]
    public TimeSpan PointDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float PointBackChance = 0.5f;

    [DataField]
    public TimeSpan FailureCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan TargetCooldown = TimeSpan.FromSeconds(180);

    [DataField]
    public TimeSpan MinIdleTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MaxIdleTime = TimeSpan.FromSeconds(7);

    [DataField]
    public TimeSpan MinEscapeDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan MaxEscapeDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MinPullDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxPullDelay = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan WakeRollInterval = TimeSpan.FromSeconds(4);

    [DataField]
    public float WakeChance = 0.2f;

    [DataField]
    public float SlipCuffsChance = 0.065f;

    [DataField]
    public float PlayDeadChance = 0.0022f;

    [DataField]
    public TimeSpan PlayDeadTime = TimeSpan.FromSeconds(1);

    [DataField]
    public ProtoId<EmotePrototype> PlayDeadEmote = "DefaultDeathgasp";

    [DataField]
    public TimeSpan RetaliationTime = TimeSpan.FromSeconds(3.4);

    [DataField]
    public TimeSpan RetaliationRest = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan CrewletFightTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan CrewletFightGap = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CrewletTruceTime = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan PunchRest = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan StrikeTimeout = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan HugRest = TimeSpan.FromSeconds(2);

    [DataField]
    public float HugBackChance = 0.5f;

    [DataField]
    public float HugBackOpinion;

    [DataField]
    public TimeSpan HugMemory = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan StareMemory = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan MinStareTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan MaxStareTime = TimeSpan.FromSeconds(4);

    [DataField]
    public string? NamePhrase;

    [DataField]
    public TimeSpan NameCooldown = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan MinMournInterval = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan MaxMournInterval = TimeSpan.FromSeconds(40);

    [DataField]
    public SoundSpecifier MournSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> MournLines = "CrewletMourning";

    [DataField]
    public int KnockCount = 2;

    [DataField]
    public TimeSpan KnockInterval = TimeSpan.FromSeconds(1.5);

    [DataField]
    public int GlanceCount = 4;

    [DataField]
    public TimeSpan MinGlanceTime = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan MaxGlanceTime = TimeSpan.FromSeconds(1);

    [DataField]
    public int HonkHops = 8;

    [DataField]
    public TimeSpan MinHonkWait = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan MaxHonkWait = TimeSpan.FromSeconds(3.75);

    [DataField]
    public float HonkChance = 0.6f;

    [DataField]
    public TimeSpan MopWait = TimeSpan.FromSeconds(1.5);

    [DataField]
    public SoundSpecifier MopSound = new SoundPathSpecifier("/Audio/Effects/Fluids/watersplash.ogg");

    [DataField]
    public TimeSpan MinVocalizeInterval = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MaxVocalizeInterval = TimeSpan.FromSeconds(64);

    [DataField]
    public TimeSpan TypingTime = TimeSpan.FromSeconds(1.5);

    [DataField]
    public TimeSpan TypingRollInterval = TimeSpan.FromSeconds(3);

    [DataField]
    public float TypingChance = 0.1f;

    [DataField]
    public float GlowChance = 0.000208f;

    [DataField]
    public float CreepyLineChance = 0.00085f;

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> CreepyLines = "CrewletCreepy";

    [DataField]
    public float ParrotChance = 0.6f;

    [DataField]
    public TimeSpan MinParrotDelay = TimeSpan.FromSeconds(0.8);

    [DataField]
    public TimeSpan MaxParrotDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public int MaxEmoteMemory = 8;

    [DataField]
    public float HelloRange = 3f;

    [DataField]
    public float HelloLeaveRange = 8f;

    [DataField]
    public TimeSpan HelloCooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public TimeSpan CrewletHelloCooldown = TimeSpan.FromMinutes(7);

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> HelloLines = "CrewletHello";

    [DataField]
    public TimeSpan DoorTryInterval = TimeSpan.FromSeconds(1.5);

    [DataField]
    public int MaxDoorTries = 3;

    [DataField]
    public TimeSpan DoorRetry = TimeSpan.FromMinutes(1);

    [DataField]
    public TimeSpan DislikeLineCooldown = TimeSpan.FromSeconds(15);

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> DislikeLines = "CrewletDislike";

    [DataField]
    public ProtoId<EmotePrototype> DislikeEmote = "Crying";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> DislikeRareLines = "CrewletDislikeRare";

    [DataField]
    public float DislikeRareChance = 0.02f;

    [DataField]
    public float AloneDislikeRareChance = 0.15f;

    [DataField]
    public float EarshotRange = SharedChatSystem.VoiceRange;

    [DataField]
    public TimeSpan DislikeRareCooldown = TimeSpan.FromMinutes(10);

    [DataField]
    public TimeSpan DislikeRareTypingTime = TimeSpan.FromSeconds(4);

    [DataField]
    public float GiftOwnerWeight = 1f;

    [DataField]
    public float KeepAwayRange = 2f;

    [DataField]
    public TimeSpan TagCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public int TagExchanges = 3;

    [DataField]
    public float TagRunRange = 3f;

    [DataField]
    public TimeSpan TagWait = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan TagHeadStart = TimeSpan.FromSeconds(1.5);

    [DataField]
    public float ToolFloorRange = 2f;

    [DataField]
    public List<CrewletToolUse> ToolUses = new();

    [DataField]
    public EntityWhitelist? TargetBlacklist;

    [DataField]
    public EntityWhitelist? HabitToolBlacklist;

    [DataField]
    public ComponentRegistry FormBlacklist = new();

    [DataField]
    public EntityWhitelist? PersonTools;

    [DataField]
    public EntityWhitelist? InterfaceTargets;

    [DataField]
    public EntityWhitelist? ExpendableTargets;

    [DataField]
    public int LearnAfter = 2;

    [DataField]
    public int MaxHabits = 6;

    [DataField]
    public int MaxCandidates = 24;

    [DataField]
    public int MaxUnlearnable = 24;

    [DataField]
    public int MaxWatches = 16;

    [DataField]
    public float HabitWeight = 0.5f;

    [DataField]
    public float MaxHabitWeight = 2f;

    [DataField]
    public TimeSpan HabitSettleTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan HabitRecheckTime = TimeSpan.FromSeconds(30);

    [DataField]
    public int RiskyLearnAfter = 3;

    [DataField]
    public LogImpact RiskyImpact = LogImpact.High;

    [DataField]
    public HashSet<string> VerbTypes = new()
    {
        nameof(InteractionVerb),
        nameof(UtilityVerb),
        nameof(AlternativeVerb),
        nameof(ActivationVerb),
    };

    [DataField]
    public TimeSpan VerbRetryTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan VerbRetryInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan UntrainWindow = TimeSpan.FromSeconds(5);

    [DataField]
    public ProtoId<EmotePrototype> UntrainEmote = "Crying";

    [DataField]
    public HungerThreshold HungerThreshold = HungerThreshold.Peckish;

    [DataField]
    public ThirstThreshold ThirstThreshold = ThirstThreshold.Parched;

    [DataField]
    public Dictionary<CrewletActivity, float> IdleWeights = new()
    {
        { CrewletActivity.Hug, 0.048f },
        { CrewletActivity.Loot, 0.0156f },
        { CrewletActivity.Climb, 0.048f },
        { CrewletActivity.Knock, 0.0424f },
        { CrewletActivity.Punch, 0.0055f },
        { CrewletActivity.Pet, 0.04f },
        { CrewletActivity.Glance, 0.1765f },
        { CrewletActivity.Sit, 0.0183f },
        { CrewletActivity.Honk, 0.0183f },
        { CrewletActivity.Mop, 0.04f },
        { CrewletActivity.Mimic, 0.08f },
        { CrewletActivity.Tag, 0.05f },
        { CrewletActivity.Tinker, 0.03f },
        { CrewletActivity.Hat, 0.03f },
        { CrewletActivity.Pickpocket, 0.02f },
    };

    [DataField]
    public Dictionary<CrewletActivity, TimeSpan> ActivityCooldowns = new()
    {
        { CrewletActivity.Stare, TimeSpan.FromSeconds(10) },
        { CrewletActivity.Fetch, TimeSpan.FromSeconds(20) },
        { CrewletActivity.Loot, TimeSpan.FromMinutes(10) },
        { CrewletActivity.Worry, TimeSpan.FromSeconds(60) },
        { CrewletActivity.Dance, TimeSpan.FromSeconds(60) },
    };

    [DataField]
    public EntityWhitelist? LootBlacklist;

    [DataField]
    public EntityWhitelist? SeatBlacklist;

    [DataField]
    public EntityWhitelist? HornBlacklist;

    [DataField]
    public EntProtoId PortalEffect = "CrewletPortal";

    [DataField]
    public EntProtoId PortalGulpEffect = "CrewletPortalGulp";

    [DataField]
    public EntProtoId PortalExitEffect = "CrewletPortalExit";

    [DataField]
    public EntProtoId GlowEffect = "CrewletGlow";

    [DataField]
    public EntProtoId MergeCore = "AnomalyCoreFlesh";

    [DataField]
    public float MergeNoticeRange = 5f;

    [DataField]
    public TimeSpan MergeTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan MergeRetry = TimeSpan.FromSeconds(20);

    [DataField]
    public SoundSpecifier MergeStartSound = new SoundPathSpecifier("/Audio/Voice/Slime/slime_squish.ogg");

    [DataField]
    public SoundSpecifier MergeSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    [DataField]
    public LocId MergeStartText = "crewlet-merge-start";

    [DataField]
    public LocId MergeText = "crewlet-merge";

    [DataField]
    public LocId LookingText = "crewlet-looking";

    [DataField]
    public LocId StareLine = "crewlet-stare";

    [DataField]
    public LocId FormToolText = "crewlet-form-tool";

    [DataField]
    public SoundSpecifier FormToolSound = new SoundCollectionSpecifier("gib", AudioParams.Default.WithVolume(-3f));

    [DataField]
    public LocId RecallLine = "crewlet-recall";

    [DataField]
    public LocId DreadText = "crewlet-dread";

    [DataField]
    public bool Active;

    [DataField]
    public CrewletActivity Activity = CrewletActivity.None;

    [DataField]
    public CrewletStep Step;

    [DataField]
    public EntityUid? Target;

    [DataField]
    public EntityCoordinates Destination;

    [DataField]
    public float MoveRange;

    [DataField]
    public int Count;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StepEnds;

    [DataField]
    public EntityUid? KnownOwner;

    [DataField]
    public bool Greeted;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ApartSince;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan RestUntil;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StayUntil;

    [DataField]
    public EntityUid? RetaliationTarget;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan RetaliateUntil;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan CrewletFightStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastCrewletFight;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan CrewletTruceUntil;

    [DataField]
    public EntityUid? Hugger;

    [DataField]
    public CrewletHugResponse HugResponse;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan HugReactionUntil;

    [DataField]
    public EntityUid? Examiner;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan StareReactionUntil;

    [DataField]
    public EntityUid? FetchItem;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan FetchUntil;

    [DataField]
    public bool Summoned;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan WorryUntil;

    [DataField]
    public EntityUid? OwnerHat;

    [DataField]
    public string? PickpocketSlot;

    [DataField]
    public HashSet<int> TaughtUses = new();

    [DataField]
    public bool NameCalled;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextNameResponse;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDislikeLine;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDislikeRare;

    [DataField]
    public EntityUid? Door;

    [DataField]
    public int DoorTries;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDoorTry;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextScan;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextVocalize;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTypingRoll;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextWakeRoll;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? TypingEnds;

    [DataField]
    public string? PendingLine;

    [DataField]
    public bool InCombat;

    [DataField]
    public ProtoId<EmotePrototype>? PendingEmote;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PendingEmoteAt;

    [DataField]
    public List<ProtoId<EmotePrototype>> Emotes = new();

    [DataField]
    public EntityUid? HelloCandidate;

    [DataField]
    public EntityUid? GiftRecipient;

    [DataField]
    public EntityUid? Tagger;

    [DataField]
    public int TagCount;

    [DataField]
    public CrewletToolAction ToolAction;

    [DataField]
    public EntityUid? ToolTarget;

    [DataField]
    public CrewletHabitKey? ToolHabit;

    [DataField]
    public EntityUid? FormedTool;

    [DataField]
    public CrewletHabitKey? LastHabit;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan UntrainUntil;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextVerbTry;

    [DataField]
    public EntityCoordinates ToolSpot;

    [DataField, AutoPausedField]
    public Dictionary<CrewletActivity, TimeSpan> ActivityReadyAt = new();

    [DataField, AutoPausedField]
    public Dictionary<EntityUid, TimeSpan> TargetReadyAt = new();

    [DataField, AutoPausedField]
    public Dictionary<EntityUid, TimeSpan> HelloReadyAt = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan CrewletHelloReadyAt;

    [DataField]
    public List<CrewletHabit> Habits = new();

    [DataField]
    public List<CrewletHabitKey> Unlearnable = new();

    public readonly List<CrewletWatch> Watches = new();

    public readonly HashSet<EntityUid> Nearby = new();

    public readonly HashSet<EntityUid> Arrivals = new();

    public EntityUid? Candidate;

    public DoAfterId? ActivityDoAfter;

    public float OwnerDistance = float.PositiveInfinity;

    public bool Scanned;

    public readonly List<EntityUid> People = new();

    public readonly List<EntityUid> Sleepers = new();

    public readonly List<EntityUid> Animals = new();

    public readonly List<EntityUid> Items = new();

    public readonly List<EntityUid> Tables = new();

    public readonly List<EntityUid> Windows = new();

    public readonly List<EntityUid> Seats = new();

    public readonly List<EntityUid> Puddles = new();

    public readonly List<EntityUid> Edibles = new();

    public readonly List<EntityUid> Crewlets = new();

    public readonly List<EntityUid> Anomalies = new();
}

public enum CrewletActivity : byte
{
    Escape,
    Unbuckle,
    BreakPull,
    Retaliate,
    Rest,
    Stay,
    Merge,
    Recall,
    CatchUp,
    Mourn,
    Worry,
    Deliver,
    Fetch,
    HugReaction,
    Stare,
    Dance,
    Greet,
    Hello,
    WakeSleeper,
    Hug,
    Loot,
    Hat,
    Climb,
    Knock,
    Punch,
    Pickpocket,
    Pet,
    Glance,
    Sit,
    Honk,
    Mop,
    Mimic,
    Tag,
    Tinker,
    Follow,
    Eat,
    KeepAway,
    Wander,
    Idle,
    None,
}

public enum CrewletStep : byte
{
    Start,
    Approach,
    Act,
    Wait,
    Escort,
}

public enum CrewletHugResponse : byte
{
    Pause,
    HugBack,
    Shove,
}

[DataDefinition]
public sealed partial class CrewletToolUse
{
    [DataField(required: true)]
    public EntityWhitelist Tool = new();

    [DataField]
    public EntityWhitelist? Target;

    [DataField]
    public EntityWhitelist? TargetBlacklist;

    [DataField]
    public float Weight = 1f;

    [DataField]
    public bool Taught;

    [DataField]
    public CrewletToolAction Action = CrewletToolAction.Use;
}

public enum CrewletToolAction : byte
{
    Use,
    Floor,
    Sign,
    Hand,
    Activate,
    Vend,
    Verb,
}

[DataRecord]
public partial record struct CrewletHabitKey(
    CrewletToolAction Action,
    EntProtoId? Tool,
    EntProtoId? Target,
    string? VerbType,
    string? Verb);

[DataRecord]
public partial record struct CrewletHabit(
    CrewletHabitKey Key,
    int Count,
    bool Learned,
    bool Risky,
    CrewletFacts Facts,
    CrewletFacts Varied);

[Flags]
public enum CrewletFacts : ushort
{
    None = 0,
    Critical = 1 << 0,
    Dead = 1 << 1,
    Helpless = 1 << 2,
    Cuffed = 1 << 3,
    Buckled = 1 << 4,
    Liked = 1 << 5,
    Disliked = 1 << 6,
    Owner = 1 << 7,
}

public enum CrewletWatchState : byte
{
    Seen,
    DoAfter,
    Settle,
    Recheck,
}

public record struct CrewletWatch(
    CrewletHabitKey Key,
    EntityUid Owner,
    EntityUid Target,
    EntProtoId Prototype,
    bool Expendable,
    bool Risky,
    CrewletFacts Facts,
    GameTick Tick,
    TimeSpan Seen)
{
    public CrewletWatchState State;
    public bool Handled;
    public ushort DoAfter;
    public TimeSpan CheckAt;
}
