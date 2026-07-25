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

        var details = Loc.GetString("desc-squad-examined",
            ("color", descSquad.Comp.Color),
            ("target", Identity.Entity(descSquad, EntityManager)),
            ("description", desc),
            ("adjective", descSquad.Comp.Adjective),
            ("word", descSquad.Comp.Word));
        args.PushMarkup(details, -1);
    }
}
