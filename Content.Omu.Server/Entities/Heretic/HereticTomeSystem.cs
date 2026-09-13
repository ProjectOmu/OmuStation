using Content.Server.Heretic.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Heretic;
using Content.Shared.Examine;
using Content.Server.Mind;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Chat.Managers;
using Content.Omu.Shared.Entities.Heretic;
using Content.Shared.Actions;
using Content.Shared.Humanoid;

namespace Content.Omu.Server.Entities.Heretic;

public sealed class HereticTomeSystem : EntitySystem
{

    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatMan = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticTomeComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<HereticTomeComponent, BoundUIClosedEvent>(OnInteract);
    }

    private void OnExamine(Entity<HereticTomeComponent> ent, ref ExaminedEvent args)
    {
        if (_heretic.IsHereticOrGhoul(args.Examiner))
            return;

        if (!_mind.TryGetMind(args.Examiner, out _, out var mind))
            return;

        if (!_playerMan.TryGetSessionById(mind.UserId, out var session))
            return;

        var baseMessage = ent.Comp.ExamineBaseMessage;
        var message = Loc.GetString(_random.Pick(ent.Comp.HeathenExamineMessages));
        var size = ent.Comp.FontSize;
        var loc = Loc.GetString(baseMessage, ("size", size), ("text", message));
        SharedChatSystem.UpdateFontSize(size, ref message, ref loc);
        _chatMan.ChatMessageToOne(ChatChannel.Server, message, loc, default, false, session.Channel, canCoalesce: false);
    }

    private void OnInteract(EntityUid book, HereticTomeComponent component, ref BoundUIClosedEvent args)
    {
        var actor = args.Actor;       //Get the players entity!

        if (!HasComp<HumanoidAppearanceComponent>(args.Actor))       //Ensure reader is a human, funny oversight.
            return;

        if (component.Readers != null)
            if (component.Readers.Contains(actor))          //Have they read it before?
                return;

        if (!_mind.TryGetMind(args.Actor, out var mindId, out var mind))
            return;

        if (!_playerMan.TryGetSessionById(mind.UserId, out var session))
            return;

        if (!TryComp<FascinationComponent>(actor, out var fasc))
            EnsureComp<FascinationComponent>(actor, out fasc);
        float fascAmount;       //How much fascination to give them?

        fascAmount = component.KnowledgeGain;
        if (component.ProductHereticKnowledge != null)
            fascAmount = fascAmount + 1; //One extra fascination per hereticknowledge gained!
        if (component.ProductAction != null)
            fascAmount = fascAmount + 1; //One extra fascination for an action gained!


        RaiseLocalEvent(actor, new FascinationChangedArgs { Amount = fascAmount});

        var message = Loc.GetString(fasc.MadnessMessage);       //Warn the user
        var size = component.FontSize;
        var loc = Loc.GetString(component.ExamineBaseMessage, ("size", size), ("text", message));
        SharedChatSystem.UpdateFontSize(size, ref message, ref loc);
        _chatMan.ChatMessageToOne(ChatChannel.Server, message, loc, default, false, session.Channel, canCoalesce: false);

        if (_heretic.TryGetHereticComponent(actor, out _, out _))             //Get heretic entity
        {
            _heretic.UpdateKnowledge(actor, component.KnowledgeGain);         //Give them knowledge
            if (component.ProductHereticKnowledge != null)                    //Does it come with extra gamer points?
                _heretic.TryAddKnowledge(mindId, component.ProductHereticKnowledge.Value, mind.CurrentEntity);      //Give em the gamer thinkin'
        }

        if (component.ProductAction != null)            //Used for single actions
        {
            EntityUid? actionId;
            actionId = _actionContainer.AddAction(mindId, component.ProductAction);
        }

        component.Readers?.Add(actor);           // No double dipping!
    }
}
