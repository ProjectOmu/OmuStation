using Content.Shared.Examine;
using Content.Shared.IdentityManagement;

namespace Content.Omu.Shared.DescSquad;

public sealed class DescSquadSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DescSquadComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DescSquadComponent> descSquad, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        var desc = descSquad.Comp.Description;
        if (desc != "")
        {
            desc += " ";
        }
        string details;

        if (descSquad.Comp.IsCustom)
        {
            details = Loc.GetString("desc-squad-custom",
                ("color", descSquad.Comp.Color),
                ("target", Identity.Entity(descSquad, EntityManager)),
                ("fullcustom", descSquad.Comp.FullCustom));
        }
        else
        {
            details = Loc.GetString("desc-squad-examined",
                ("color", descSquad.Comp.Color),
                ("target", Identity.Entity(descSquad, EntityManager)),
                ("verb", descSquad.Comp.Verb),
                ("description", desc),
                ("determiner", descSquad.Comp.Determiner),
                ("adjective", descSquad.Comp.Adjective),
                ("word", descSquad.Comp.Word));
        }

        args.PushMarkup(details, -1);
    }
}
