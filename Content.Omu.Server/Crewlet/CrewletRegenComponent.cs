// SPDX-License-Identifier: MIT

using Content.Shared.Damage;
using Content.Shared.Mobs;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.Crewlet;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CrewletRegenComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    [DataField]
    public List<MobState> AllowedStates = new() { MobState.Alive, MobState.Critical };

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRegen;
}
