using System.Linq;
using Content.Shared.Actions;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Vampire.Traits;

public abstract class LesserVampireTraitSystem<T> : EntitySystem where T : LesserVampireTraitComponent
{
    [Dependency] protected readonly SharedLesserVampireSystem Vampire = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<T, MapInitEvent>(OnInit);
        SubscribeLocalEvent<T, GetVampireBloodPoolChange>(OnBloodPoolChange);
    }

    private void OnBloodPoolChange(Entity<T> ent, ref GetVampireBloodPoolChange args)
    {
        if (!TryComp<LesserVampireComponent>(ent, out var vampire)) return;

        args.Change -= ent.Comp.PassiveDrain;

        RefreshBloodpoolDrain((ent.Owner, vampire, ent.Comp), ref args);
    }

    private void OnInit(Entity<T> ent, ref MapInitEvent args)
    {
        if (!TryComp<LesserVampireComponent>(ent, out var vampire)) return;

        Vampire.RefreshBloodPoolChange((ent.Owner, vampire));
        TraitInit((ent.Owner, vampire, ent.Comp));
    }

    protected virtual void TraitInit(Entity<LesserVampireComponent, T> ent) { }
    protected virtual void RefreshBloodpoolDrain(Entity<LesserVampireComponent, T> ent, ref GetVampireBloodPoolChange args) { }
}

public abstract class LesserVampirePassiveTraitSystem<T> : LesserVampireTraitSystem<T>
    where T : LesserVampirePassiveTraitComponent
{
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityManager.AllEntityQueryEnumerator<LesserVampireComponent, T>();
        while (query.MoveNext(out var uid, out var vampire, out var comp))
        {
            if (comp.TickRate == TimeSpan.Zero || Timing.CurTime < comp.NextUpdate) continue;
            comp.NextUpdate = Timing.CurTime + comp.TickRate;

            UpdateEffect((uid, vampire, comp));
        }
    }

    protected virtual void UpdateEffect(Entity<LesserVampireComponent, T> ent) { }
}

public abstract class LesserVampireActionTraitSystem<T, TEvent> : LesserVampireTraitSystem<T>
    where T : LesserVampireActionTraitComponent
    where TEvent : BaseActionEvent
{
    [Dependency] protected readonly SharedActionsSystem Actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<T, TEvent>(OnActionUsed);
    }

    private void OnActionUsed(Entity<T> ent, ref TEvent args)
    {
        if (!TryComp<LesserVampireComponent>(ent, out var vampire)) return;

        ActionUsed((ent.Owner, vampire, ent.Comp), ref args);
    }

    protected override void TraitInit(Entity<LesserVampireComponent, T> ent)
    {
        base.TraitInit(ent);

        Actions.AddAction(ent, ent.Comp2.Action);
    }

    protected virtual void ActionUsed(Entity<LesserVampireComponent, T> ent, ref TEvent args) { }
}

public abstract class LesserVampireToggleActionTraitSystem<T, TEvent> : LesserVampireActionTraitSystem<T, TEvent>
    where T : LesserVampireToggleActionComponent where TEvent : InstantActionEvent
{
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<T, OutOfBloodPoolEvent>(OnOutOfBloodPool);
    }

    private void OnOutOfBloodPool(Entity<T> ent, ref OutOfBloodPoolEvent args)
    {
        if (!TryComp<LesserVampireComponent>(ent, out var vampire) ||
            !ent.Comp.Toggled)
            return;

        ent.Comp.Toggled = false;
        Vampire.RefreshBloodPoolChange((ent, vampire));
        Dirty(ent);

        var action = Actions.GetActions(ent)
            .Where(p => MetaData(p).EntityPrototype is { } entProto && entProto.ID == ent.Comp.Action)
            .FirstOrNull();

        if (action == null) return;

        Actions.SetToggled(action.Value.AsNullable(), false);
    }

    protected override void TraitInit(Entity<LesserVampireComponent, T> ent)
    {
        base.TraitInit(ent);

        var action = Actions.GetActions(ent)
            .Where(p => MetaData(p).EntityPrototype is { } entProto && entProto.ID == ent.Comp2.Action)
            .FirstOrNull();

        if (action == null) return;

        Actions.SetToggled(action.Value.AsNullable(), ent.Comp2.Toggled);
        OnToggled(ent);
    }

    protected override void ActionUsed(Entity<LesserVampireComponent, T> ent, ref TEvent args)
    {
        base.ActionUsed(ent, ref args);

        if (Vampire.GetBloodPool(ent) == 0) return;

        if (_net.IsServer)
        {
            ent.Comp2.Toggled = !ent.Comp2.Toggled;
            Vampire.RefreshBloodPoolChange((ent.Owner, ent.Comp1));
            Dirty(ent);
        }

        Actions.SetToggled(args.Action.AsNullable(), ent.Comp2.Toggled);
        OnToggled(ent);
    }

    protected override void RefreshBloodpoolDrain(Entity<LesserVampireComponent, T> ent,
        ref GetVampireBloodPoolChange args)
    {
        base.RefreshBloodpoolDrain(ent, ref args);

        if (ent.Comp2.Toggled)
            args.Change -= ent.Comp2.DrainWhenToggled;
    }

    protected virtual void OnToggled(Entity<LesserVampireComponent, T> ent) { }
}