using Content.Server.Ghost;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Damage;
using Content.Shared.Station.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.StationEvents.Events;

public abstract class LightsOutRule<T> : StationEventSystem<T> where T : IComponent
{
    protected void InteriorActiveTick(
        IGameTiming time,
        IRobustRandom rand,
        GhostSystem ghost,
        DamageableSystem dmg,
        ref TimeSpan? smashingTime,
        IList<EntityUid> lightsToFlicker,
        IList<EntityUid> lightsToBreak,
        ref int targetIndex,
        DamageSpecifier damage,
        float damageProbability
    )
    {
        if (time.CurTime < smashingTime)
        {
            // lights flicker before the destruction starts. build suspense.
            foreach (var light in lightsToFlicker)
            {
                if (!rand.Prob(0.25f))
                    continue;

                ghost.DoGhostBooEvent(light);
            }
        }
        else
        {
            // now, the destruction, one light at a time
            if (targetIndex < lightsToBreak.Count)
            {
                if (rand.Prob(damageProbability))
                {
                    dmg.TryChangeDamage(
                        lightsToBreak[targetIndex],
                        damage,
                        true
                    );
                }

                targetIndex++;
            }
            else
            {
                // finished smashing
                smashingTime = null;
            }
        }
    }

    protected bool IsEntityOnStation(EntityUid ent, EntityUid station)
    {
        var xform = Transform(ent);

        if (!TryComp<StationMemberComponent>(xform.GridUid, out var sMember))
            return false;

        // Don't flicker lights on CC
        if (HasComp<StationCentcommComponent>(xform.GridUid))
            return false;

        // the ent is considered on-station if its grid has both BecomesStation and StationMember
        // AND
        // the StationMember.Station is equal to the station in question
        return HasComp<BecomesStationComponent>(xform.GridUid) && sMember.Station == station;
    }
}
