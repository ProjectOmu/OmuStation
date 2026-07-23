using Content.Server.Heretic.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Heretic;
using Content.Shared.Examine;
using Content.Server.Mind;
using Robust.Server.Player;
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Server.Chat.Managers;
using Robust.Server.Toolshed.Commands.Players;

namespace Content.Server._Omu.Entities.Heretic;

public sealed class HereticTomeSystem : EntitySystem
{

    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerMan = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatMan = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    public override void Initialize()
    {
        base.Initialize();
        //SubscribeLocalEvent<HereticTomeComponent, UseInHandEvent>(OnInteract);
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

        if (!_heretic.TryGetHereticComponent(actor, out _, out _))            //Get heretic entity
            return;

        if (!_mind.TryGetMind(args.Actor, out _, out var mind))
            return;

        if (!_playerMan.TryGetSessionById(mind.UserId, out var session))
            return;

        if (!TryComp<FascinationComponent>(actor, out var fasc))
            EnsureComp<FascinationComponent>(actor, out fasc);

        RaiseLocalEvent(actor, new FascinationChangedArgs { Amount = component.KnowledgeGain });

        var message = Loc.GetString(fasc.MadnessMessage);       //Warn the user
        var size = component.FontSize;
        var loc = Loc.GetString(component.ExamineBaseMessage, ("size", size), ("text", message));
        SharedChatSystem.UpdateFontSize(size, ref message, ref loc);
        _chatMan.ChatMessageToOne(ChatChannel.Server, message, loc, default, false, session.Channel, canCoalesce: false);

        _heretic.UpdateKnowledge(actor, component.KnowledgeGain);       //Give them knowledge
        Spawn("Ash", Transform(book).Coordinates);          //Ash the book
        QueueDel(book);
    }
}
