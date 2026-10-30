using Content.Goobstation.Server.Chaplain.Components;
using Content.Goobstation.Shared.CustomFactionIcons;
using Content.Omu.Shared.Entities.Heretic;
using Content.Omu.Shared.MansusMob;
using Content.Server.GameTicking;
using Content.Server.NPC.Systems;
using Content.Server.Popups;
using Content.Shared._Omu.Heretic;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.MansusMob;

public sealed class MansusMobSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MansusMobComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MansusMaddenedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MansusMaddenedComponent, ComponentRemove>(OnRemoval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var seekQuery = EntityQueryEnumerator<MansusSeekComponent>();
        while (seekQuery.MoveNext(out var uid, out var seek))
        {
            if (Deleted(seek.TrackedId))
                seek.Tracking = false;

            // If tracking, check if tracked entity is still valid
            if (seek.Tracking && !HasComp<MansusMaddenedComponent>(seek.TrackedId))
            {
                seek.Tracking = false;
            }

            // If not tracking, check for another maddened player
            if (!seek.Tracking)
            {
                List<EntityUid> maddenedPlayers = new();
                using var madQuery = EntityQueryEnumerator<MansusMaddenedComponent>();
                while (madQuery.MoveNext(out var madUid, out var fascination))
                {
                    maddenedPlayers.Add(madUid);
                }
                if (maddenedPlayers.Count > 0)
                {
                    seek.Tracking = true;
                    seek.TrackedId = _random.Pick(maddenedPlayers);
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

    private void OnMobStateChanged(EntityUid uid, MansusMobComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            bool noMaidens = true;
            using var seekQuery = EntityQueryEnumerator<MansusMobComponent>();
            while (seekQuery.MoveNext(out var seekUid, out var seek))
            {
                if (seekUid != uid)
                {
                    noMaidens = false;
                    break;
                }

            }

            // Resupply the hunt if we are tracking madness 
            if (noMaidens)
            {
                using var madQuery = EntityQueryEnumerator<MansusMaddenedComponent>();
                while (madQuery.MoveNext(out var seekUid, out var fascination))
                {
                    _gameTicker.StartGameRule("MirrorMaidenSpawn", out var spawnEnt);
                    break;
                }
            }
        }
    }

    private void OnStartup(EntityUid uid, MansusMaddenedComponent maddened, ComponentStartup args)
    {
        var fascination = EnsureComp<FascinationComponent>(uid);

        if (fascination.FascinationValue < 5)
            fascination.FascinationValue = 5;

        if (fascination.Naturalsight == false)
        {
            _adminLog.Add(LogType.AdminMessage, LogImpact.Extreme,
            $"{uid} has fascination 5, making valid");
            EnsureComp<SeeHereticFixturesComponent>(uid);
            fascination.AlteredVision = true;
            _eye.RefreshVisibilityMask(uid);
        }
        if (fascination.Naturalsight == true && fascination.NaturalHereticsight == false)
        {
            if (TryComp<SeeHereticFixturesComponent>(uid, out var seefixtures))
            {
                seefixtures.SeeShifts = true;
                _eye.RefreshVisibilityMask(uid);
            }
        }
        if (fascination.AlteredFaction != true)
        {
            fascination.AlteredFaction = true;
            var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(uid);    //Make them valid to the mirror maiden
            userFactionIcons.FactionIcons.Add(fascination.IconToAdd);
            _faction.AddFaction(uid, fascination.FactionToAdd); //Give them the faction so AI works
            Dirty(uid, userFactionIcons);
        }

        Dirty(uid, fascination);
    }

    private void OnRemoval(EntityUid uid, MansusMaddenedComponent maddened, ComponentRemove args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        FascinationComponent? fascination = null;
        if (!Resolve(uid, ref fascination))
            return;

        if (fascination.Naturalsight == false && fascination.AlteredVision == true)
        {
            RemComp<SeeHereticFixturesComponent>(uid);
            fascination.AlteredVision = false;
        }
        if (fascination.Naturalsight == true && fascination.NaturalHereticsight == false)
        {
            if (TryComp<SeeHereticFixturesComponent>(uid, out var seefixtures))
                seefixtures.SeeShifts = false;
        }
        if (fascination.AlteredFaction == true)
        {
            var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(uid);    //Make them un-valid to the mirror maiden
            userFactionIcons.FactionIcons.Remove(fascination.IconToAdd);
            _faction.RemoveFaction(uid, fascination.FactionToAdd); // remove the faction
            fascination.AlteredFaction = false;
            Dirty(uid, userFactionIcons);
        }
    }
}
