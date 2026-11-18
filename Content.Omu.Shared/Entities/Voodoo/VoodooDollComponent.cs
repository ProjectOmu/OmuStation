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

    [DataField(required: true)]
    public TimeSpan BindDelay;

    [DataField(required: true)]
    public TimeSpan RemoteBindDelay;

    [DataField(required: true)]
    public TimeSpan UnbindDelay;

    [DataField(required: true)]
    public SoundSpecifier BindSound = default!;

    [DataField(required: true)]
    public SoundSpecifier BindUserSound = default!;

    [DataField]
    public EntProtoId? RebindCooldownStatusEffect = "StatusEffectVoodooRebindCooldown";

    [DataField(required: true)]
    public TimeSpan RebindCooldown;

    // Spooky victim text size
    [DataField(required: true)]
    public int FontSize;

    [DataField(required: true)]
    public float? Range;

    [DataField]
    public bool LocationHints = true;

    [DataField(required: true)]
    public float HintBeaconChance;

    [DataField(required: true)]
    public int ShockDamage;

    [DataField(required: true)]
    public TimeSpan ShockTime;

    [DataField(required: true)]
    public TimeSpan FlashTime;

    [DataField(required: true)]
    public float FlashSlowTo;

    [DataField(required: true)]
    public SoundSpecifier FlashSound = default!;

    [DataField]
    public EntProtoId BlindStatusEffect = "StatusEffectVoodooBlindness";

    [DataField(required: true)]
    public TimeSpan BlindTime;

    [DataField(required: true)]
    public float HeatChange;

    [DataField(required: true)]
    public SoundSpecifier HeatSound = default!;

    // Rolls with the heat interaction, % to add one fire stack.
    [DataField(required: true)]
    public float IgniteChance;

    [DataField(required: true)]
    public DamageSpecifier CutDamage = default!;

    [DataField(required: true)]
    public float BleedAmount;

    [DataField(required: true)]
    public SoundSpecifier CutSound = default!;

    [DataField(required: true)]
    public DamageSpecifier ThrowDamage = default!;

    [DataField(required: true)]
    public float ThrowDistance;

    [DataField(required: true)]
    public float ThrowSpeed;

    [DataField(required: true)]
    public TimeSpan TripTime;

    [DataField(required: true)]
    public TimeSpan ForcedAttackTime;

    [DataField(required: true)]
    public float ForcedShotRange;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ForcedAttackStart;

    [DataField(required: true)]
    public float FlingDistance;

    [DataField(required: true)]
    public float FlingSpeed;

    [DataField]
    public ProtoId<TagPrototype> PenTag = "Pen";

    [DataField]
    public ProtoId<StatusEffectPrototype> MuteStatusEffect = "Muted";

    [DataField(required: true)]
    public TimeSpan MuteTime;

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
    [DataField(required: true)]
    public float SoapFriction;

    [DataField(required: true)]
    public TimeSpan SoapTime;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? SoapEnd;

    [DataField(required: true)]
    public DamageSpecifier DestroyedDamage = default!;

    [DataField(required: true)]
    public TimeSpan ViewTime;

    [DataField]
    public EntityUid? Viewer;

    [ViewVariables]
    public ICommonSession? ViewerSession;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ViewEnd;
}
