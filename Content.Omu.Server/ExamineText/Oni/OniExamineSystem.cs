// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition.Components;

namespace Content.Omu.Server.ExamineText.Oni;

public sealed partial class OniExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OniExamineComponent, ExaminedEvent>(OnExamined);
    }
    private void OnExamined(Entity<OniExamineComponent> oni, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;
        if (TryComp(oni, out HungerComponent? hunger) && TryComp(oni, out ThirstComponent? thirst) &&
           (hunger.CurrentThreshold <= hunger.StarvationThreshold || thirst.CurrentThirstThreshold <= thirst.DehydrationThreshold))
        {
            var details = Loc.GetString("oni-examined", ("target", Identity.Entity(oni, EntityManager)));
            args.PushMarkup(details, 5);
        }
    }
}
