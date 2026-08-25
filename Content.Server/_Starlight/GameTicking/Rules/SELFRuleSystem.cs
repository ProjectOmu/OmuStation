using Content.Server.Antag;
using Content.Server.Roles;
using Content.Server.GameTicking.Rules;
using Content.Shared._Starlight.Roles;
using SELFRuleComponent = Content.Server._Starlight.GameTicking.Rules.Components.SELFRuleComponent;

namespace Content.Server._Starlight.GameTicking.Rules;

public sealed class SELFRuleSystem : GameRuleSystem<SELFRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SELFRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);
        SubscribeLocalEvent<SELFAgentRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon SELF activation
    private void AfterAntagSelected(Entity<SELFRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;

        _antag.SendBriefing(ent, Loc.GetString("self-role-greeting-human"), null, null);
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<SELFAgentRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent == null)
            return;

        args.Append(Loc.GetString("self-role-greeting-human"));
    }
}
