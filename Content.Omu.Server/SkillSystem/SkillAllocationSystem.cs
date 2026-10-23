// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Omu.Server.SkillSystem.Components;
using Content.Omu.Server.SkillSystem.Events;
using Content.Omu.Shared.SkillSystem;
using Content.Shared.GameTicking;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.SkillSystem;

public sealed class SkillAllocationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillAllocationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SkillAllocationComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        Subs.BuiEvents<SkillAllocationComponent>(SkillAllocationUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnOpened);
                subs.Event<SkillAllocationConfirmMessage>(OnConfirmed);
            });
    }

    private void OnInit(Entity<SkillAllocationComponent> ent, ref ComponentInit args)
    {
        _ui.SetUi(ent.Owner, SkillAllocationUiKey.Key, new InterfaceData("SkillAllocationBoundUserInterface"));
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<RoundStartSkillGrantingComponent>(ev.Mob))
            return;

        if (EnsureComp<SkillAllocationComponent>(ev.Mob).Points > 0)
            _ui.OpenUi(ev.Mob, SkillAllocationUiKey.Key, ev.Mob);
    }

    private void OnGetVerbs(Entity<SkillAllocationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.User != ent.Owner || !args.CanInteract || ent.Comp.Points <= 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("skill-allocation-verb"),
            Act = () => _ui.OpenUi(ent.Owner, SkillAllocationUiKey.Key, user),
        });
    }

    private void OnOpened(Entity<SkillAllocationComponent> ent, ref BoundUIOpenedEvent args)
    {
        var levels = CompOrNull<MindContainerComponent>(ent)?.Mind is { } mind
            ? CompOrNull<SkillHolderComponent>(mind)?.SkillDic
            : null;

        var skills = new List<SkillAllocationEntry>();
        foreach (var skill in _prototype.EnumeratePrototypes<SkillNamePrototype>().OrderBy(skill => skill.ID))
        {
            var level = 0;
            levels?.TryGetValue(skill.ID, out level);
            skills.Add(new SkillAllocationEntry(skill.ID, skill.Name, skill.Description, level));
        }

        _ui.SetUiState(ent.Owner,
            SkillAllocationUiKey.Key,
            new SkillAllocationBoundUserInterfaceState(skills, ent.Comp.Points));
    }

    private void OnConfirmed(Entity<SkillAllocationComponent> ent, ref SkillAllocationConfirmMessage args)
    {
        var total = 0;
        foreach (var (skill, amount) in args.Allocation)
        {
            if (amount < 0 || !_prototype.HasIndex<SkillNamePrototype>(skill))
                return;

            total += amount;
        }

        if (total <= 0 || total > ent.Comp.Points)
            return;

        foreach (var (skill, amount) in args.Allocation)
        {
            if (amount <= 0)
                continue;

            ent.Comp.Allocation.TryGetValue(skill, out var current);
            ent.Comp.Allocation[skill] = current + amount;
        }

        ent.Comp.Points -= total;
        Apply(ent, args.Allocation);
        _ui.CloseUi(ent.Owner, SkillAllocationUiKey.Key, args.Actor);
    }

    private void Apply(EntityUid mob, Dictionary<string, int> allocation)
    {
        if (CompOrNull<MindContainerComponent>(mob)?.Mind is not { } mind)
            return;

        var holder = EnsureComp<SkillHolderComponent>(mind);
        foreach (var (skill, amount) in allocation)
        {
            if (amount <= 0)
                continue;

            holder.SkillDic.TryGetValue(skill, out var current);
            holder.SkillDic[skill] = current + amount;
        }

        var ev = new SkillLevelChengeEvent();
        RaiseLocalEvent(mob, ref ev);
    }
}
