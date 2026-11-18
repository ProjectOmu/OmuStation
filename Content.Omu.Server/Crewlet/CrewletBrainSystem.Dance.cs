// SPDX-License-Identifier: MIT

using Content.Server.Instruments;
using Content.Shared.Instruments;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private bool WantsDance(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        return ent.Comp.Scanned && IsReady(ent.Comp, CrewletActivity.Dance, now) && HearsMusic(ent);
    }

    private bool HearsMusic(Entity<CrewletBrainComponent> ent)
    {
        var origin = _transform.GetMapCoordinates(ent);
        using var query = EntityQueryEnumerator<ActiveInstrumentComponent, InstrumentComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var instrument, out var xform))
        {
            if (instrument.Playing && _transform.GetMapCoordinates(uid, xform).InRange(origin, ent.Comp.MusicRange))
                return true;
        }

        return false;
    }

    private void RunDance(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Step == CrewletStep.Wait && now < brain.StepEnds)
            return;

        if (brain.Count >= brain.DanceBeats || !HearsMusic(ent))
        {
            if (brain.ActivityCooldowns.TryGetValue(CrewletActivity.Dance, out var cooldown))
                brain.ActivityReadyAt[CrewletActivity.Dance] = now + cooldown;

            End(ent);
            return;
        }

        brain.Count++;
        _rotate.TryFaceAngle(ent, Angle.FromDegrees(brain.Count * 90));
        if (brain.Count % 2 == 0 && brain.DanceEmote is { } emote)
            _chat.TryEmoteWithoutChat(ent, emote);

        WaitFor(ent, brain.DanceBeat, now);
    }
}
