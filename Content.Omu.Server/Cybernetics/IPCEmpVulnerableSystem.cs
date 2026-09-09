using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Content._Omu.Shared.Cybernetics;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Emp;

namespace Content.Omu.Server.Cybernetics;

internal sealed class CyberneticsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<IPCEmpVulnerableComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<IPCEmpVulnerableComponent, EmpDisabledRemovedEvent>(OnEmpDisabledRemoved);
    }

    private readonly string DamageTypeIon = "Ion";

    private void OnEmpPulse(Entity<IPCEmpVulnerableComponent> cyberEnt, ref EmpPulseEvent ev)
    {
        if (!cyberEnt.Comp.Disabled)
        {
            ev.Affected = true;
            ev.Disabled = true;
            cyberEnt.Comp.Disabled = true;

            if (TryComp(cyberEnt, out DamageableComponent? damageable))
            {
                var ion = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(DamageTypeIon), 75); // 75 ion damage, 75 vital damage -> 19 wires to heal
                _damageable.TryChangeDamage(cyberEnt, ion, ignoreResistances: true, targetPart: TargetBodyPart.Vital, splitDamage: SplitDamageBehavior.SplitEnsureAll, damageable: damageable);
                Dirty(cyberEnt, damageable);
            }
        }
    }

    private void OnEmpDisabledRemoved(Entity<IPCEmpVulnerableComponent> cyberEnt, ref EmpDisabledRemovedEvent ev)
    {
        if (cyberEnt.Comp.Disabled)
        {
            cyberEnt.Comp.Disabled = false;
        }
    }
}
