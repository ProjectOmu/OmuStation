using Content.Server.Emp;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Cybernetics;
using Content.Shared.Body.Part;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Content._Omu.Shared.Cybernetics;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;

namespace Content._Omu.Server.Cybernetics;

internal sealed class CyberneticsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<IPCEmpVulnerableComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<IPCEmpVulnerableComponent, EmpDisabledRemoved>(OnEmpDisabledRemoved);
    }
    private void OnEmpPulse(Entity<IPCEmpVulnerableComponent> cyberEnt, ref EmpPulseEvent ev)
    {
        if (!cyberEnt.Comp.Disabled)
        {
            ev.Affected = true;
            ev.Disabled = true;
            cyberEnt.Comp.Disabled = true;

            if (TryComp(cyberEnt, out DamageableComponent? damageable))
            {
                var ion = new DamageSpecifier(_prototypes.Index<DamageTypePrototype>("Ion"), 500); // Something something, vital damage, this is spread across every limb.
                _damageable.TryChangeDamage(cyberEnt, ion, ignoreResistances: true, targetPart: TargetBodyPart.All, splitDamage: SplitDamageBehavior.SplitEnsureAll, damageable: damageable);
                Dirty(cyberEnt, damageable);
            }
        }
    }

    private void OnEmpDisabledRemoved(Entity<IPCEmpVulnerableComponent> cyberEnt, ref EmpDisabledRemoved ev)
    {
        if (cyberEnt.Comp.Disabled)
        {
            cyberEnt.Comp.Disabled = false;
        }
    }
}
