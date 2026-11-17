// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MartialArts.Events;

[Serializable, NetSerializable, ImplicitDataDefinitionForInheritors]
public abstract partial class BaseWrestlingEvent : EntityEventArgs
{
    [DataField]
    public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Effects/bodyfall1.ogg");

    [DataField]
    public SoundSpecifier? CrowdSound { get; set; }
}

public sealed partial class WrestlingSuplexPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public float ThrowDistance = 2f;
}

public sealed partial class WrestlingRkoPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public float MinVelocity = 2f;

    [DataField]
    public float LandShortOfTarget = 0.4f;

    [DataField]
    public TimeSpan SelfKnockdownTime = TimeSpan.FromSeconds(1);

    [DataField]
    public LocId? Catchphrase;
}

public sealed partial class WrestlingDdtPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public TimeSpan SelfKnockdownTime = TimeSpan.FromSeconds(0.8);
}

public sealed partial class WrestlingElbowDropPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public TimeSpan SelfKnockdownTime = TimeSpan.FromSeconds(0.8);
}

public sealed partial class WrestlingSubmissionPerformedEvent : BaseWrestlingEvent;

public sealed partial class WrestlingDropkickPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public float KnockbackDistance = 2f;

    [DataField]
    public TimeSpan SelfKnockdownTime = TimeSpan.FromSeconds(1);

    // Omu start
    [DataField]
    public float SelfDamage = 5f;

    [DataField]
    public List<EntProtoId> BreakableWindows = new();

    [DataField]
    public List<EntProtoId> BreakableGrilles = new();

    [DataField]
    public TimeSpan BreakThroughTime = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier? BreakThroughSound { get; set; }
    // Omu end
}

public sealed partial class WrestlingPowerbombPerformedEvent : BaseWrestlingEvent
{
    [DataField]
    public SoundSpecifier BellSound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/boxingbell.ogg");

    [DataField]
    public SoundSpecifier? CritSound { get; set; }

    // Omu start
    [DataField]
    public TimeSpan SubmissionCooldown = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan SlamDelay = TimeSpan.FromSeconds(0.58);

    [DataField]
    public List<EntProtoId> BreakableTables = new();
    // Omu end
}

[Serializable, NetSerializable]
public sealed class WrestlingSuplexAnimationEvent(NetEntity target, SoundSpecifier landSound, bool landedDown)
    : EntityEventArgs
{
    public NetEntity Target = target;
    public SoundSpecifier LandSound = landSound;
    public bool LandedDown = landedDown;
}

[Serializable, NetSerializable]
public sealed class WrestlingDropkickAnimationEvent(NetEntity performer, NetEntity target, SoundSpecifier impactSound)
    : EntityEventArgs
{
    public NetEntity Performer = performer;
    public NetEntity Target = target;
    public SoundSpecifier ImpactSound = impactSound;
}

[Serializable, NetSerializable]
public sealed class WrestlingRkoAnimationEvent(
    NetEntity performer,
    NetEntity target,
    SoundSpecifier impactSound,
    SoundSpecifier? crowdSound,
    bool landedDown) : EntityEventArgs
{
    public NetEntity Performer = performer;
    public NetEntity Target = target;
    public SoundSpecifier ImpactSound = impactSound;
    public SoundSpecifier? CrowdSound = crowdSound;
    public bool LandedDown = landedDown;
}

[Serializable, NetSerializable]
public sealed class WrestlingDdtAnimationEvent(
    NetEntity target,
    SoundSpecifier impactSound,
    bool headFirst,
    bool landedDown) : EntityEventArgs
{
    public NetEntity Target = target;
    public SoundSpecifier ImpactSound = impactSound;
    public bool HeadFirst = headFirst;
    public bool LandedDown = landedDown;
}

[Serializable, NetSerializable]
public sealed class WrestlingElbowDropAnimationEvent(NetEntity performer, NetEntity target, SoundSpecifier impactSound)
    : EntityEventArgs
{
    public NetEntity Performer = performer;
    public NetEntity Target = target;
    public SoundSpecifier ImpactSound = impactSound;
}

[Serializable, NetSerializable]
public sealed class WrestlingSlamAnimationEvent(
    NetEntity target,
    SoundSpecifier slamSound,
    SoundSpecifier? crowdSound,
    SoundSpecifier bellSound,
    SoundSpecifier? critSound) : EntityEventArgs
{
    public NetEntity Target = target;
    public SoundSpecifier SlamSound = slamSound;
    public SoundSpecifier? CrowdSound = crowdSound;
    public SoundSpecifier BellSound = bellSound;
    public SoundSpecifier? CritSound = critSound;
}

[Serializable, NetSerializable]
public sealed class WrestlingBeltCheerEvent(SoundSpecifier sound) : EntityEventArgs
{
    public SoundSpecifier Sound = sound;
}

public sealed class WrestlingCatchphraseEvent(string message) : EntityEventArgs
{
    public string Message = message;
}
