// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.StatusEffect;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Shared.Entities.Voodoo;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class VoodooDollComponent : Component
{
    [DataField]
    public EntityUid? Target;

    [ViewVariables]
    public List<EntityUid> Candidates = new();

    [DataField]
    public string DelayId = "voodoo";

    [DataField]
    public ProtoId<ReagentPrototype> HolyReagent = "Holywater";

    [DataField]
    public TimeSpan BindDelay = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan RemoteBindDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan UnbindDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier BindSound = new SoundPathSpecifier("/Audio/_Goobstation/Heretic/voidblink.ogg");

    [DataField]
    public SoundSpecifier BindUserSound = new SoundPathSpecifier("/Audio/_Goobstation/Heretic/voidblink.ogg",
        AudioParams.Default.WithVolume(-8));

    [DataField]
    public EntProtoId? RebindCooldownStatusEffect = "StatusEffectVoodooRebindCooldown";

    [DataField]
    public TimeSpan RebindCooldown = TimeSpan.FromMinutes(2);

    // Spooky victim text size
    [DataField]
    public int FontSize = 20;

    [DataField]
    public float? Range = 150f;

    [DataField]
    public bool LocationHints = true;

    [DataField]
    public float HintBeaconChance = 0.5f;

    [DataField]
    public int ShockDamage = 5;

    [DataField]
    public TimeSpan ShockTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan FlashTime = TimeSpan.FromSeconds(5);

    [DataField]
    public float FlashSlowTo = 0.5f;

    [DataField]
    public SoundSpecifier FlashSound = new SoundPathSpecifier("/Audio/Weapons/flash.ogg",
        AudioParams.Default.WithVolume(1f).WithMaxDistance(3f));

    [DataField]
    public EntProtoId BlindStatusEffect = "StatusEffectVoodooBlindness";

    [DataField]
    public TimeSpan BlindTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float HeatChange = 40f;

    [DataField]
    public SoundSpecifier HeatSound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    // Rolls with the heat interaction, % to add one fire stack.
    [DataField]
    public float IgniteChance = 0.33f;

    [DataField(required: true)]
    public DamageSpecifier CutDamage = default!;

    [DataField]
    public float BleedAmount = 3.75f;

    [DataField]
    public SoundSpecifier CutSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    [DataField(required: true)]
    public DamageSpecifier ThrowDamage = default!;

    [DataField]
    public float ThrowDistance = 4f;

    [DataField]
    public float ThrowSpeed = 7f;

    [DataField]
    public TimeSpan TripTime = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ForcedAttackTime = TimeSpan.FromSeconds(6);

    [DataField]
    public float ForcedShotRange = 3f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ForcedAttackStart;

    [DataField]
    public float FlingDistance = 4f;

    [DataField]
    public float FlingSpeed = 10f;

    [DataField]
    public ProtoId<TagPrototype> PenTag = "Pen";

    [DataField]
    public ProtoId<StatusEffectPrototype> MuteStatusEffect = "Muted";

    [DataField]
    public TimeSpan MuteTime = TimeSpan.FromSeconds(10);

    [DataField]
    public ProtoId<TagPrototype> LizardPlushieTag = "PlushieLizard";

    [DataField]
    public ProtoId<EmotePrototype> WehEmote = "Weh";

    [DataField]
    public ProtoId<TagPrototype> BikeHornTag = "BikeHorn";

    [DataField]
    public ProtoId<EmotePrototype> HonkEmote = "Honk";

    [DataField]
    public ProtoId<TagPrototype> BedsheetTag = "Bedsheet";

    [DataField]
    public ProtoId<EmotePrototype> YawnEmote = "Yawn";

    [DataField]
    public ProtoId<TagPrototype> SoapTag = "Soap";

    // Friction override during soap
    [DataField]
    public float SoapFriction = 0.025f;

    [DataField]
    public TimeSpan SoapTime = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? SoapEnd;

    [DataField(required: true)]
    public DamageSpecifier DestroyedDamage = default!;

    [DataField]
    public TimeSpan ViewTime = TimeSpan.FromSeconds(10);

    [DataField]
    public EntityUid? Viewer;

    [ViewVariables]
    public ICommonSession? ViewerSession;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ViewEnd;
}
