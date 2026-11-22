// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Content.Server.Verbs;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Random;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem
{
    private void InitializeReactions()
    {
        SubscribeLocalEvent<CrewletBrainComponent, CrewletOpinionChangedEvent>(OnOpinionChanged);
        SubscribeLocalEvent<CrewletBrainComponent, InteractionSuccessEvent>(OnHugged,
            after: [typeof(CrewletReputationSystem)]);
        SubscribeLocalEvent<CrewletBrainComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CrewletBrainComponent, AfterGotPointedAtEvent>(OnPointedAt);
        SubscribeLocalEvent<CrewletBrainComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<CrewletOwnerComponent, EmoteEvent>(OnOwnerEmote);
        SubscribeLocalEvent<CrewletOwnerComponent, UserInteractHandEvent>(OnOwnerInteractHand);
        SubscribeLocalEvent<CrewletOwnerComponent, UserInteractUsingEvent>(OnOwnerInteractUsing);
        SubscribeLocalEvent<CrewletOwnerComponent, ContactInteractionEvent>(OnOwnerContact);
        SubscribeLocalEvent<ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<CrewletOwnerComponent, DamageChangedEvent>(OnOwnerDamaged);
        SubscribeLocalEvent<CrewletOwnerComponent, DidUnequipEvent>(OnOwnerUnequipped);
        SubscribeLocalEvent<CrewletBrainComponent, CrewletPickpocketDoAfterEvent>(OnPickpocketDoAfter);
        SubscribeNetworkEvent<ExecuteVerbEvent>(OnOwnerVerb, before: [typeof(VerbSystem)]);

        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld,
                new PointerInputCmdHandler(OnOwnerAltUse),
                typeof(SharedInteractionSystem))
            .Register<CrewletBrainSystem>();
    }

    private void OnOpinionChanged(Entity<CrewletBrainComponent> ent, ref CrewletOpinionChangedEvent args)
    {
        if (!ent.Comp.Active || args.Reason is not (CrewletOpinionReason.Hurt or CrewletOpinionReason.Shoved))
            return;

        if (_crewletQuery.TryComp(ent, out var crewlet) && crewlet.CrewletOwner == args.Person)
        {
            Untrain(ent);
            if (TryComp<CrewletReputationComponent>(ent, out var reputation)
                && args.Opinion > reputation.WaryThreshold)
                return;
        }

        var now = _timing.CurTime;
        if (!FightCrewlet(ent, args.Person, now))
            return;

        ent.Comp.RetaliationTarget = args.Person;
        ent.Comp.RetaliateUntil = now + ent.Comp.RetaliationTime;
    }

    private void OnHugged(Entity<CrewletBrainComponent> ent, ref InteractionSuccessEvent args)
    {
        if (!ent.Comp.Active || args.User == ent.Owner)
            return;

        if (TryComp<CrewletBrainComponent>(args.User, out var tagger)
            && tagger.Activity == CrewletActivity.Tag
            && tagger.Target == ent.Owner)
        {
            if (tagger.Count < tagger.TagExchanges)
            {
                ent.Comp.Tagger = args.User;
                ent.Comp.TagCount = tagger.Count;
            }

            return;
        }

        var opinion = _reputation.GetOpinion(ent.Owner, args.User);
        if (TryComp<CrewletReputationComponent>(ent, out var reputation) && opinion <= reputation.WaryThreshold)
            ent.Comp.HugResponse = CrewletHugResponse.Shove;
        else if (opinion < ent.Comp.HugBackOpinion)
            ent.Comp.HugResponse = CrewletHugResponse.Pause;
        else if (_random.Prob(ent.Comp.HugBackChance))
            ent.Comp.HugResponse = CrewletHugResponse.HugBack;
        else
            ent.Comp.HugResponse = CrewletHugResponse.Pause;

        ent.Comp.Hugger = args.User;
        ent.Comp.HugReactionUntil = _timing.CurTime + ent.Comp.HugMemory;
    }

    private void OnPointedAt(Entity<CrewletBrainComponent> ent, ref AfterGotPointedAtEvent args)
    {
        if (!ent.Comp.Active
            || args.Pointer == ent.Owner
            || _ghostQuery.HasComp(args.Pointer)
            || !_random.Prob(ent.Comp.PointBackChance))
            return;

        Point(ent, args.Pointer);
    }

    private void OnExamined(Entity<CrewletBrainComponent> ent, ref ExaminedEvent args)
    {
        var examiner = args.Examiner;
        if (!ent.Comp.Active
            || examiner == ent.Owner
            || _ghostQuery.HasComp(examiner)
            || _crewletQuery.HasComp(examiner))
            return;

        _popup.PopupEntity(Loc.GetString(ent.Comp.LookingText), ent, examiner, PopupType.Medium);
        ent.Comp.Examiner = examiner;
        ent.Comp.StareReactionUntil = _timing.CurTime + ent.Comp.StareMemory;
    }

    private void OnListen(Entity<CrewletBrainComponent> ent, ref ListenEvent args)
    {
        if (!ent.Comp.Active || args.Source == ent.Owner)
            return;

        var now = _timing.CurTime;
        if (args.Source == ent.Comp.KnownOwner)
        {
            NoticeCome(ent, args.Message, now);
            NoticeFetch(ent, args.Message, now);
            NoticeStay(ent, args.Message, now);
        }

        if (now < ent.Comp.NextNameResponse
            || _crewletQuery.HasComp(args.Source)
            || _ghostQuery.HasComp(args.Source)
            || !CalledByName(ent, args.Message))
            return;

        ent.Comp.NextNameResponse = now + ent.Comp.NameCooldown;
        ent.Comp.NameCalled = true;
    }

    private bool CalledByName(Entity<CrewletBrainComponent> ent, string message)
    {
        if (ent.Comp.NamePhrase is { } phrase)
            return ContainsWord(message, phrase);

        return ContainsWord(message, Name(ent))
            || (_crewletQuery.TryComp(ent, out var crewlet) && ContainsWord(message, crewlet.CallName));
    }

    private static bool ContainsWord(string message, string word)
    {
        if (word.Length == 0)
            return false;

        var start = message.IndexOf(word, StringComparison.OrdinalIgnoreCase);
        while (start >= 0)
        {
            var end = start + word.Length;
            if ((start == 0 || !char.IsLetterOrDigit(message[start - 1]))
                && (end == message.Length || !char.IsLetterOrDigit(message[end])))
                return true;

            start = message.IndexOf(word, start + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private bool WantsHugReaction(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Hugger is not { } hugger)
            return false;

        if (now >= brain.HugReactionUntil || !Present(hugger))
        {
            brain.Hugger = null;
            return false;
        }

        if (!IsReady(brain, CrewletActivity.HugReaction, now))
            return false;

        brain.Candidate = hugger;
        brain.Hugger = null;
        return true;
    }

    private bool WantsStare(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Examiner is not { } examiner)
            return false;

        if (now >= brain.StareReactionUntil || !Present(examiner))
        {
            brain.Examiner = null;
            return false;
        }

        if (!IsReady(brain, CrewletActivity.Stare, now))
            return false;

        brain.Candidate = examiner;
        brain.Examiner = null;
        return true;
    }

    private void RunHugReaction(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        switch (brain.Step)
        {
            case CrewletStep.Start:
                if (brain.HugResponse != CrewletHugResponse.Pause && brain.Target is { } hugger && Present(hugger))
                    Approach(ent, hugger, brain.MeleeRange, now);
                else
                    WaitFor(ent, brain.HugRest, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived || brain.Target is not { } target || !Present(target))
                {
                    WaitFor(ent, brain.HugRest, now);
                    return;
                }

                if (brain.HugResponse == CrewletHugResponse.HugBack)
                {
                    Interact(ent, target);
                    WaitFor(ent, brain.HugRest, now);
                    return;
                }

                brain.Step = CrewletStep.Act;
                brain.StepEnds = now + brain.StrikeTimeout;
                ShoveHugger(ent, now);
                return;
            case CrewletStep.Act:
                ShoveHugger(ent, now);
                return;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                    End(ent);
                return;
        }
    }

    private void ShoveHugger(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is { } hugger
            && Present(hugger)
            && now < brain.StepEnds
            && DistanceTo(ent, hugger) <= brain.InteractRange)
        {
            if (!Strike(ent, hugger, shove: true))
                return;

            Complain(ent, hugger, now);
        }

        WaitFor(ent, brain.HugRest, now);
    }

    private bool WantsHello(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.HelloCandidate is not { } person || !IsReady(brain, CrewletActivity.Hello, now))
            return false;

        brain.HelloCandidate = null;
        if (!Present(person)
            || !_mobState.IsAlive(person)
            || DistanceTo(ent, person) > brain.HelloRange
            || !InSight(ent, person)
            || !_actionBlocker.CanSpeak(ent))
            return false;

        brain.Candidate = person;
        return true;
    }

    private void RunHello(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is not { } person || !Present(person))
        {
            End(ent);
            return;
        }

        if (brain.Step == CrewletStep.Start)
        {
            if (_crewletQuery.HasComp(person))
            {
                brain.CrewletHelloReadyAt = now + brain.CrewletHelloCooldown;
                Point(ent, person);
            }
            else
            {
                brain.HelloReadyAt[person] = now + brain.HelloCooldown;
            }

            var lines = _proto.Index(brain.HelloLines).Values;
            var name = CrewletSystem.GetShortName(Identity.Name(person, EntityManager, ent));
            brain.PendingLine = Loc.GetString(lines[_random.Next(lines.Count)], ("name", name));
            StartTyping(ent, now + brain.TypingTime);
            WaitFor(ent, brain.TypingTime, now);
        }
        else if (now >= brain.StepEnds)
        {
            End(ent);
            return;
        }

        _rotate.TryFaceCoordinates(ent, _transform.GetWorldPosition(person));
    }

    private void RunStare(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var brain = ent.Comp;
        if (brain.Target is not { } target || !Present(target))
        {
            End(ent);
            return;
        }

        switch (brain.Step)
        {
            case CrewletStep.Start:
                Approach(ent, target, brain.StareRange, now);
                return;
            case CrewletStep.Approach:
                var move = Move(ent, now);
                if (move == CrewletMove.Moving)
                    return;

                if (move != CrewletMove.Arrived)
                {
                    End(ent);
                    return;
                }

                _chat.TrySendInGameICMessage(ent,
                    Loc.GetString(brain.StareLine),
                    InGameICChatType.Speak,
                    hideChat: true,
                    hideLog: true);
                WaitFor(ent, _random.Next(brain.MinStareTime, brain.MaxStareTime), now);
                break;
            case CrewletStep.Wait:
                if (now >= brain.StepEnds)
                {
                    End(ent);
                    return;
                }

                break;
        }

        _rotate.TryFaceCoordinates(ent, _transform.GetWorldPosition(target));
    }
}
