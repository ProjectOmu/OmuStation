// SPDX-License-Identifier: MIT

using Content.Shared.Chat;
using Content.Shared.Cuffs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Storage;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool WantsEscape(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return _container.IsEntityInContainer(ent) && IsReady(ent.Comp, CrewletActivity.Escape, now);
    }

    private bool WantsUnbuckle(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return _buckle.IsBuckled(ent) && IsReady(ent.Comp, CrewletActivity.Unbuckle, now);
    }

    private bool WantsBreakPull(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return _pulling.IsPulled(ent) && IsReady(ent.Comp, CrewletActivity.BreakPull, now);
    }

    private void RunEscape(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
        {
            End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Wait && now < ent.Comp.StepEnds)
            return;

        if (ent.Comp.Step == CrewletStep.Wait)
        {
            var holder = container.Owner;
            if (_entityStorageQuery.HasComp(holder))
                _entityStorage.TryOpenStorage(ent, holder);
            else if (_itemQuery.HasComp(holder) && HasComp<StorageComponent>(holder))
                _container.TryRemoveFromContainer(ent.Owner);
        }

        WaitFor(ent, _random.Next(ent.Comp.MinEscapeDelay, ent.Comp.MaxEscapeDelay), now);
    }

    private void RunUnbuckle(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!_buckle.IsBuckled(ent))
        {
            End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start)
        {
            WaitFor(ent, _random.Next(ent.Comp.MinEscapeDelay, ent.Comp.MaxEscapeDelay), now);
            return;
        }

        if (now < ent.Comp.StepEnds)
            return;

        _buckle.TryUnbuckle(ent.Owner, ent, popup: false);
        End(ent);
    }

    private void RunBreakPull(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        if (!TryComp<PullableComponent>(ent, out var pullable) || !pullable.BeingPulled)
        {
            End(ent);
            return;
        }

        if (ent.Comp.Step == CrewletStep.Start)
        {
            WaitFor(ent, _random.Next(ent.Comp.MinPullDelay, ent.Comp.MaxPullDelay), now);
            return;
        }

        if (now < ent.Comp.StepEnds)
            return;

        if (_actionBlocker.CanInteract(ent, ent))
            _pulling.TryStopPull(ent, pullable, ent);

        End(ent);
    }

    private void RollWake(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (now < brain.NextWakeRoll)
            return;

        brain.NextWakeRoll = now + brain.WakeRollInterval;
        var roll = _random.NextFloat();

        if (roll < brain.WakeChance)
        {
            _sleeping.TryWaking(ent.Owner, true);
            _stun.TryStanding(ent.Owner);
            return;
        }

        roll -= brain.WakeChance;
        if (roll < brain.SlipCuffsChance)
        {
            if (TryComp<CuffableComponent>(ent, out var cuffable)
                && _cuffable.TryGetLastCuff((ent, cuffable), out var cuff))
                _cuffable.Uncuff(ent, null, cuff.Value, cuffable);

            return;
        }

        roll -= brain.SlipCuffsChance;
        if (roll >= brain.PlayDeadChance)
            return;

        _stun.TryKnockdown(ent.Owner, brain.PlayDeadTime, drop: false, force: true);
        _chat.TryEmoteWithChat(ent, brain.PlayDeadEmote, ChatTransmitRange.GhostRangeLimit);
    }
}
