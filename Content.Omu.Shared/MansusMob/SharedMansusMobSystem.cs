using Content.Shared._Omu.Heretic.Components;
using Content.Shared.Actions;
using Content.Shared.Heretic;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Omu.Shared.MansusMob;

public sealed class SharedMansusMobSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _aud = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MansusMobComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ToggleMansusSeek>(OnMansusSeek);
        SubscribeLocalEvent<MansusScreechEvent>(OnMansusScreech);
        SubscribeLocalEvent<MansusSeekComponent, ComponentStartup>(OnMansusSeekStartup);
        SubscribeLocalEvent<MansusSeekComponent, ComponentShutdown>(OnMansusSeekRemove);
    }

    private void OnMeleeHit(Entity<MansusMobComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (HasComp<MansusMaddenedComponent>(hit))
                continue;
            _popup.PopupPredicted(Loc.GetString("mansus-mob-miss-hit"), ent, ent, PopupType.SmallCaution);
            args.Handled = true;
        }
    }

    private void OnMansusSeekStartup(Entity<MansusSeekComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ActionUid = _actions.AddAction(ent, ent.Comp.ActionId);
        if (_net.IsServer)
            Dirty(ent);
    }

    private void OnMansusSeekRemove(Entity<MansusSeekComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ActionUid != null)
        {
            _actions.RemoveAction(ent.Comp.ActionUid.Value);
        }
    }

    private void OnMansusSeek(ToggleMansusSeek args)
    {
        // Check if currently tracking
        if (!TryComp<MansusSeekComponent>(args.Performer, out var seek))
            return;

        if (!seek.Tracking // for client target may be out of PVS and thus if it doesn't exist for client don't try to predict popups
            || !HasComp<MansusMaddenedComponent>(seek.TrackedId)) // (this is an implicit check here, for client seek.TrackedId may not exist.)
        {
            if (_net.IsClient) // kinda shit but i cba right now.
                return;

            _popup.PopupEntity(Loc.GetString("mansus-mob-no-player"), args.Performer, args.Performer, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        _popup.PopupEntity(Loc.GetString("mansus-mob-seek-player"), seek.TrackedId, seek.TrackedId, PopupType.SmallCaution);
            RaiseLocalEvent(new EventHereticLivingHeartActivate
            {
                Target = GetNetEntity(seek.TrackedId),
                Actor = args.Performer,
            });

            seek.Tracking = true;
            if (_net.IsServer)
                Dirty(args.Performer, seek);

        args.Handled = true;
    }

    private void OnMansusScreech(MansusScreechEvent args)
    {
        if (!TryComp<MansusMobComponent>(args.Performer, out var mansus))
            return;

        foreach (var lookupUid in _lookup.GetEntitiesInRange<ActorComponent>(Transform(args.Performer).Coordinates, mansus.ScreechRange))
        {
            _aud.PlayEntity(mansus.ScreechSound, lookupUid.Owner, lookupUid.Owner);
        }

        foreach (var lookupUid in _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(args.Performer).Coordinates, mansus.ScreechRange))
        {
            if (TryComp<PoweredLightComponent>(lookupUid, out var light))
            {
                _poweredLight.ToggleBlinkingLight(lookupUid, light, true);
#pragma warning disable CS0618
                lookupUid.Owner.SpawnTimer(light.GhostBlinkingTime, () =>
                {
                    _poweredLight.ToggleBlinkingLight(lookupUid, light, false);
                });
#pragma warning restore CS0618
            }
        }
        args.Handled = true;
    }
}
