using Content.Goobstation.Shared.Blob.Components;
using Content.Omu.Shared.Entities.Heretic;
using Content.Omu.Shared.Overlays;
using Content.Shared._Omu.Heretic;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Heretic;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Security.Cryptography;

namespace Content.Omu.Shared.MansusMob;

public sealed class MirrorMaidenSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _aud = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MansusMobComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ToggleMansusSeek>(OnMansusSeek);
        SubscribeLocalEvent<MansusScreechEvent>(OnMansusScreech);
        SubscribeLocalEvent<MansusSeekComponent, ComponentInit>(OnMansusSeekStartup);
        SubscribeLocalEvent<MansusSeekComponent, ComponentShutdown>(OnMansusSeekRemove);
    }

    private void OnMeleeHit(Entity<MansusMobComponent> ent, ref MeleeHitEvent args)
    {
        foreach (var hit in args.HitEntities)
        {
            if (!HasComp<ActorComponent>(hit) || !TryComp<FascinationComponent>(hit, out var fascination) || fascination.FascinationValue < 5)
            {
                _popup.PopupPredicted(Loc.GetString("mansus-mob-miss-hit"), ent, ent, PopupType.SmallCaution);
                args.Handled = true;
                continue;
            }
        }
    }

    private void OnMansusSeekStartup(Entity<MansusSeekComponent> ent, ref ComponentInit args)
    {
        ent.Comp.ActionUid = _actions.AddAction(ent, ent.Comp.ActionId);
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
        List<EntityUid> players = new();
        // Check if currently tracking
        if (!TryComp<MansusSeekComponent>(args.Performer, out var seek))
            return;

        // Continuous tracking logic
        if (seek.Tracking && TryComp<FascinationComponent>(seek.TrackedId, out var trackedFascination))
        {
            if (trackedFascination.FascinationValue >= 5)
            {
                players.Add(seek.TrackedId);
            }
            else
                seek.Tracking = false;
        }
        else
        {
            // Find random madness inflicted player
            var query = EntityQueryEnumerator<FascinationComponent>();
            while (query.MoveNext(out var uid, out var fascination))
            {
                if (fascination.FascinationValue >= 5)
                {
                    players.Add(uid);
                }
            }
        }

        // Pick random player from list
        if (players.Count > 0)
        {
            var randomPlayer = _random.Pick(players);
            _popup.PopupEntity(Loc.GetString("mansus-mob-seek-player"), randomPlayer, randomPlayer, PopupType.SmallCaution);

            // Raise Living Heart Event
            var ev = new EventHereticLivingHeartActivate();
            ev.Target = GetNetEntity(randomPlayer);
            ev.Actor = args.Performer;
            RaiseLocalEvent(ev);

            seek.Tracking = true;
            seek.TrackedId = randomPlayer;
        }
        else
        {
            _popup.PopupPredicted(Loc.GetString("mansus-mob-no-player"), args.Performer, args.Performer, PopupType.SmallCaution);
        }

        args.Handled = true;
    }

    private void OnMansusScreech(MansusScreechEvent args)
    {
        if (!TryComp<MansusMobComponent>(args.Performer, out var mansus))
            return;

        _aud.PlayGlobal(new SoundPathSpecifier("/Audio/_Omu/Entities/eerie_scream.ogg"), args.Performer, AudioParams.Default.WithVolume(-3f));

        foreach (var lookupUid in _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(args.Performer).Coordinates, mansus.ScreechRange))
        {
            if (TryComp<PoweredLightComponent>(lookupUid, out var light))
            {
                _poweredLight.ToggleBlinkingLight(lookupUid, light, true);
#pragma warning disable CS0618
                args.Performer.SpawnTimer(light.GhostBlinkingTime, () =>
                {
                    _poweredLight.ToggleBlinkingLight(args.Performer, light, false);
                });
#pragma warning restore CS0618
            }
        }
        args.Handled = true;
    }
}
