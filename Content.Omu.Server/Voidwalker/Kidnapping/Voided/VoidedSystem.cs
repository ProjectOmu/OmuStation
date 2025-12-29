using Content.Omu.Common.Speech;
using Content.Omu.Common.VoidedVisualizer;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Voidwalker.Kidnapping.Voided;

public sealed class VoidedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly VoidwalkerSystem _voidwalker = default!;
    [Dependency] private readonly VoidwalkerKidnappedSystem _voidKidnapped = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VoidedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<VoidedComponent> entity, ref ComponentStartup args)
    {
        EnsureComp<VoidedVisualsComponent>(entity);
        EnsureComp<VoidAccentComponent>(entity); // This was muted in ss13, but I think this accent is cooler.
        EnsureComp<PacifiedComponent>(entity);
    }

    private void OnShutdown(Entity<VoidedComponent> entity, ref ComponentShutdown args)
    {
        RemComp<VoidedVisualsComponent>(entity);
        RemComp<VoidAccentComponent>(entity);
        RemComp<PacifiedComponent>(entity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check every X amount of seconds. Just in case.
        var query = EntityQueryEnumerator<VoidedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime <= comp.NextSpacedCheck)
                continue;

            if (_voidwalker.CheckInSpace(uid))
            {
                if (!_voidKidnapped.TryTeleportToRandomPartOfStation(uid))
                    return;

                var popup = Loc.GetString("voided-spaced-teleport");
                _popup.PopupEntity(popup, uid, uid, PopupType.MediumCaution);
            }

            comp.NextSpacedCheck = _timing.CurTime + comp.SpacedCheckInterval;
        }
    }
}
