// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Server.Pointing.Components;
using Content.Shared.Animals.Components;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.IdentityManagement;
using Robust.Shared.Map;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void UpdateSpeech(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.TypingEnds is { } typingEnds && now >= typingEnds)
        {
            StopTyping(ent);
            if (brain.PendingLine is { } line)
            {
                brain.PendingLine = null;
                Say(ent, line);
            }
        }

        if (brain.NameCalled)
        {
            brain.NameCalled = false;
            brain.NextVocalize = now + _random.Next(brain.MinVocalizeInterval, brain.MaxVocalizeInterval);
            if (PickMemory(ent) is { } answer)
                Say(ent, answer);
        }

        if (now >= brain.NextVocalize && brain.PendingLine == null)
        {
            brain.NextVocalize = now + _random.Next(brain.MinVocalizeInterval, brain.MaxVocalizeInterval);
            if (PickMemory(ent) is { } memory && _actionBlocker.CanSpeak(ent))
            {
                brain.PendingLine = memory;
                StartTyping(ent, now);
            }
        }

        if (now < brain.NextTypingRoll)
            return;

        brain.NextTypingRoll = now + brain.TypingRollInterval;
        var roll = _random.NextFloat();
        if (roll < brain.TypingChance)
        {
            if (brain.TypingEnds == null && _actionBlocker.CanSpeak(ent))
                StartTyping(ent, now);
        }
        else if (roll < brain.TypingChance + brain.GlowChance)
        {
            SpawnAttachedTo(brain.GlowEffect, new EntityCoordinates(ent, Vector2.Zero));
            _crewlet.Flare(ent);
        }
    }

    private void StartTyping(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        ent.Comp.TypingEnds = now + ent.Comp.TypingTime;
        _appearance.SetData(ent, TypingIndicatorVisuals.State, TypingIndicatorState.Typing);
    }

    private void StopTyping(Entity<CrewletBrainComponent> ent)
    {
        if (ent.Comp.TypingEnds == null)
            return;

        ent.Comp.TypingEnds = null;
        _appearance.SetData(ent, TypingIndicatorVisuals.State, TypingIndicatorState.None);
    }

    private string? PickMemory(Entity<CrewletBrainComponent> ent)
    {
        if (!TryComp<ParrotMemoryComponent>(ent, out var memory) || memory.SpeechMemories.Count == 0)
            return null;

        return memory.SpeechMemories[_random.Next(memory.SpeechMemories.Count)].Message;
    }

    private void SayCreepyLine(Entity<CrewletBrainComponent> ent)
    {
        if (ent.Comp.CreepyLineChance <= 0f)
            return;

        var lines = _proto.Index(ent.Comp.CreepyLines).Values;
        var index = (int) (_random.NextFloat() / ent.Comp.CreepyLineChance);
        if (index < lines.Count)
            Say(ent, Loc.GetString(lines[index]));
    }

    private void Complain(Entity<CrewletBrainComponent> ent, EntityUid person, TimeSpan now)
    {
        var brain = ent.Comp;
        if (now < brain.NextDislikeLine
            || !TryComp<CrewletReputationComponent>(ent, out var reputation)
            || !Disliked(ent, reputation, person))
            return;

        brain.NextDislikeLine = now + brain.DislikeLineCooldown;
        var lines = _proto.Index(brain.DislikeLines).Values;
        var pick = _random.Next(lines.Count + 1);
        if (pick == lines.Count)
        {
            _chat.TryEmoteWithChat(ent, brain.DislikeEmote, forceEmote: true);
            return;
        }

        var name = CrewletSystem.GetShortName(Identity.Name(person, EntityManager, ent));
        Point(ent, person);
        Say(ent, Loc.GetString(lines[pick], ("name", name)));
    }

    private void Say(Entity<CrewletBrainComponent> ent, string line)
    {
        if (_actionBlocker.CanSpeak(ent))
            _chat.TrySendInGameICMessage(ent, line, InGameICChatType.Speak, false);
    }

    private void Point(Entity<CrewletBrainComponent> ent, EntityUid target)
    {
        if (!_pointing.CanPoint(ent) || !InSight(ent, target))
            return;

        _rotate.TryFaceCoordinates(ent, _transform.GetWorldPosition(target));
        var arrow = Spawn(ent.Comp.PointingArrow, Transform(target).Coordinates);
        if (TryComp<PointingArrowComponent>(arrow, out var pointing))
        {
            var start = _transform.ToCoordinates((arrow, Transform(arrow)), _transform.GetMapCoordinates(ent));
            pointing.StartPosition = start.Position;
            pointing.EndTime = _timing.CurTime + ent.Comp.PointDuration;
            Dirty(arrow, pointing);
        }

        _popup.PopupEntity(Loc.GetString("pointing-system-point-at-other-others",
                ("otherName", Identity.Entity(ent, EntityManager)),
                ("other", Identity.Entity(target, EntityManager))),
            ent);
    }
}
