using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(CMGunSystem))] // Omu we don't have this system.
public sealed partial class RMCBattleExecuteComponent : Component
{
    //[DataField, AutoNetworkedField]
    //public EntProtoId<SkillDefinitionComponent> Skill = "RMCSkillExecution"; // Omu we don't have this or even use it.

    /// <summary>
    ///     The BE's Damage
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Blunt"] = 200 } };

    /// <summary>
    ///     How long it takes to perform a BE Do-After
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BattleExecuteTimeSeconds = TimeSpan.FromSeconds(1);
}
