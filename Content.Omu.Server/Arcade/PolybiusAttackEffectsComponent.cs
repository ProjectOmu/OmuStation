// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dataset;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusAttackEffectsSystem)), AutoGenerateComponentPause]
public sealed partial class PolybiusAttackEffectsComponent : Component
{
    [DataField]
    public EntProtoId BlackoutEffect = "StatusEffectVoodooBlindness";

    [DataField]
    public LocId DiagnosticStart = "polybius-silicon-diagnostic-start";

    [DataField]
    public LocId DiagnosticEnd = "polybius-silicon-diagnostic-end";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> Diagnostics = "PolybiusDiagnostics";

    [DataField]
    public SoundSpecifier DiagnosticSound = new SoundCollectionSpecifier("sparks");

    [DataField]
    public LocId ParanoiaStart = "polybius-paranoia-start";

    [DataField]
    public LocId ParanoiaEnd = "polybius-paranoia-end";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> Paranoia = "PolybiusParanoia";

    [DataField]
    public Dictionary<ProtoId<SpeciesPrototype>, ProtoId<LocalizedDatasetPrototype>> SpeciesParanoia = new();

    [DataField]
    public int SpeciesLines = 2;

    [DataField]
    public int MiddleLines = 3;

    public readonly Queue<string> PendingLines = new();

    public bool LinesArePublic;

    public TimeSpan LineInterval;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextLineTime;

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenFreezes;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenFreezes;

    [DataField(required: true)]
    public TimeSpan MaxFreezeDuration;

    [DataField(required: true)]
    public TimeSpan MinFreezeDuration;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextFreezeTime;

    [DataField]
    public LocId UnlockPopup = "polybius-silicon-unlock-popup";

    [DataField]
    public LocId StationAiIntruderPopup = "polybius-ai-intruder-popup";

    [DataField]
    public SoundSpecifier StationAiIntruderSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg",
        AudioParams.Default.WithVolume(-5f));

    [DataField]
    public SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg",
        AudioParams.Default.WithVolume(-5f));

    [DataField(required: true)]
    public TimeSpan MaxTimeBetweenUnlocks;

    [DataField(required: true)]
    public TimeSpan MinTimeBetweenUnlocks;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUnlockTime;
}
