// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.Wizard.EventSpells;

public abstract class SharedGhostVisibilitySystem : EntitySystem
{
    protected static readonly EntProtoId GameRule = "GhostsVisible";

    public bool GhostsVisible()
    {
        // Omu - do not add GameRuleComponent here: it is server-only, so the query would never match on the client.
        // GhostsVisibleRuleComponent + ActiveGameRuleComponent are both networked and are sufficient.
        var query = EntityQueryEnumerator<GhostsVisibleRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out _, out _, out _))
        {
            return true;
        }

        return false;
    }
}