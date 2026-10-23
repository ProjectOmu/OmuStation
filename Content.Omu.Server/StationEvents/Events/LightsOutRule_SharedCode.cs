using Content.Server.Ghost;
using Content.Shared.Damage;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.StationEvents.Events;

public static class LightsOutRule_SharedCode
{
    public static void InteriorActiveTick(
        IGameTiming _time,
        IRobustRandom _rand,
        GhostSystem _ghost,
        DamageableSystem _dmg,
        ref TimeSpan? SmashingTime,
            IList<EntityUid> LightsToFlicker,
            IList<EntityUid> LightsToBreak,
        ref int TargetIndex,
            DamageSpecifier Damage,
            float DamageProbability
    ) {
        if (_time.CurTime < SmashingTime)
        {
            // lights flicker before the destruction starts. build suspense.
            foreach (EntityUid light in LightsToFlicker)
            {
                if (!_rand.Prob(0.25f))
                    continue;
                _ghost.DoGhostBooEvent(light);
            }
        }
        else
        {
            // now, the destruction, one light at a time
            if (TargetIndex < LightsToBreak.Count)
            {
                if (_rand.Prob(DamageProbability))
                    _dmg.TryChangeDamage(
                        LightsToBreak[TargetIndex],
                        Damage,
                        true
                    );
                TargetIndex++;
            }
            else
            {
                // finished smashing
                SmashingTime = null;
            }
        }
    }
}
