using Content.Omu.Shared.Entities.Heretic;
using Content.Omu.Shared.MansusMob;
using Content.Server.NPC.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Popups;

namespace Content.Omu.Server.MansusMob;

public sealed class MansusMobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaidenSpawnEvent>(OnMaidenSpawn);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var seekQuery = EntityQueryEnumerator<MansusSeekComponent>();
        while (seekQuery.MoveNext(out var uid, out var seek))
        {
            if (Deleted(seek.TrackedId))
                seek.Tracking = false;

            // If tracking, check if tracked entity is still valid and has enough fascination value
            if (seek.Tracking && TryComp<FascinationComponent>(seek.TrackedId, out var trackedFascination))
            {
                if (trackedFascination.FascinationValue < 5)
                {
                    seek.Tracking = false;
                }
            }
            else
                seek.Tracking = false;

            // If not tracking, check for another maddened player
            if (!seek.Tracking)
            {
                var madQuery = EntityQueryEnumerator<FascinationComponent>();
                while (madQuery.MoveNext(out var madUid, out var fascination))
                {
                    if (fascination.FascinationValue >= 5)
                    {
                        seek.Tracking = true;
                        seek.TrackedId = madUid;
                        break;
                    }
                }
            }
            if (seek.NextSeek < _timing.CurTime || seek.NextSeek == null)
            {
                // Only update non-player entities
                if (seek.Tracking)
                {
                    if (!HasComp<ActorComponent>(uid))
                    {
                        var trackedCoords = Transform(seek.TrackedId).Coordinates;
                        _npc.SetBlackboard(uid, "CallTarget", trackedCoords);
                        seek.NextSeek = _timing.CurTime + TimeSpan.FromSeconds(seek.SeekCooldown);
                    }
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("mansus-mob-no-player-damage"), uid, uid, PopupType.SmallCaution);
                    var dmg = new DamageSpecifier()
                    {
                        DamageDict =
                        {
                            { "Blunt", seek.PassiveDamage }
                        }
                    };
                    _damage.TryChangeDamage(uid, dmg, true, false, targetPart: TargetBodyPart.All);
                    seek.NextSeek = _timing.CurTime + TimeSpan.FromSeconds(seek.PassiveDamageCooldown);
                }
            }
        }
    }
    private void OnMaidenSpawn(MaidenSpawnEvent args)
    {
        if (!TryComp<MansusSeekComponent>(args.SpawnedEntity, out var seek))
            return;
        seek.Tracking = true;
        seek.TrackedId = args.TrackedEntity;
    }
}
