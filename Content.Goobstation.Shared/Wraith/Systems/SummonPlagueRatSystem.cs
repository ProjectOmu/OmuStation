using Content.Goobstation.Shared.Wraith.Components;
using Content.Goobstation.Shared.Wraith.Events;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Jittering;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.Wraith.Systems;

public sealed partial class SummonPlagueRatSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ISharedAdminLogManager _admin = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Omu edit

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SummonPlagueRatComponent, SummonPlagueRatEvent>(OnSummonRat);
    }

    // Part 2 TO DO: Only allow one summoned plaguerat to exist on station at any given time.

    private void OnSummonRat(Entity<SummonPlagueRatComponent> ent, ref SummonPlagueRatEvent args) // Omu edit: This whole system is reworked
    {
        var comp = ent.Comp;
        if (args.Handled)
            return;
        comp.EndTimer = _timing.CurTime + comp.TimerLength;
        comp.Target = args.Target;
        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("wraith-plaguerat-channel"), args.Performer, args.Performer);
            EnsureComp<JitteringComponent>(args.Target);
        }

        args.Handled = true;

    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SummonPlagueRatComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.EndTimer == TimeSpan.Zero)
                continue;
            if (curTime < comp.EndTimer)
                continue;
            comp.EndTimer = TimeSpan.Zero;
            if (_net.IsServer)
            {
                RemComp<JitteringComponent>(comp.Target);
                Spawn(comp.RatProto, Transform(comp.Target).Coordinates);
                _admin.Add(LogType.EntitySpawn, LogImpact.Medium, $"{uid} has summoned a plague rat");
            }

            Dirty(uid, comp);
        }
    }
}
