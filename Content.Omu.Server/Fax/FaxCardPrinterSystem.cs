// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Fax;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Fax;

public sealed class FaxCardPrinterSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FaxCardPrinterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<FaxCardPrinterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<FaxMachineComponent>(ent, out var fax))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fax-card-printer-verb"),
            Disabled = fax.SendTimeoutRemaining > 0,
            Act = () => PrintCard(ent.Comp.Card, fax),
        });
    }

    private void PrintCard(EntProtoId card, FaxMachineComponent fax)
    {
        if (fax.SendTimeoutRemaining > 0)
            return;

        var proto = _prototype.Index(card);
        var content = proto.TryGetComponent<PaperComponent>(out var paper, EntityManager.ComponentFactory)
            ? Loc.GetString(paper.Content)
            : string.Empty;

        fax.PrintingQueue.Enqueue(new FaxPrintout(content, proto.Name, prototypeId: card));
        fax.SendTimeoutRemaining += fax.SendTimeout;
    }
}
