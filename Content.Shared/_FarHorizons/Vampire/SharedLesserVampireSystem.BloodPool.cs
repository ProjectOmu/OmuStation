using Content.Shared.Nutrition.Components;

namespace Content.Shared._FarHorizons.Vampire;

public partial class SharedLesserVampireSystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = AllEntityQuery<LesserVampireComponent>();
        while (query.MoveNext(out var uid, out var vampire))
        {
            if (!_mobState.IsAlive(uid) ||
                Timing.CurTime < vampire.NextUpdate)
                continue;
            vampire.NextUpdate = Timing.CurTime + vampire.BloodPoolRefreshTime;

            if (GetBloodPool((uid, vampire)) > 0) continue;

            _hunger.ModifyHunger(uid, -vampire.HungerDrain);
            if (TryComp<ThirstComponent>(uid, out var thirst))
                _thirst.ModifyThirst(uid, thirst, -vampire.ThristDrain);

            var ev = new OutOfBloodPoolEvent();
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public float GetBloodPool(Entity<LesserVampireComponent> ent) =>
        Math.Clamp(
            (float) (ent.Comp.BloodPoolLastValue + (ent.Comp.BloodPoolChange *
                                                   (Timing.CurTime - ent.Comp.BloodPoolLastUpdated).TotalSeconds)),
            0, ent.Comp.BloodPoolMax);

    public virtual void SetBloodPool(Entity<LesserVampireComponent> ent, float value) { } // Server-only
    public virtual void RefreshBloodPoolChange(Entity<LesserVampireComponent> ent) { } // Server-only
}