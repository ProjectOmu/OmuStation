// SPDX-FileCopyrightText: 2025 duston <66768086+dch-GH@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 misghast <51974455+misterghast@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
// SPDX-FileCopyrightText: 2026 Steve <marlumpy@gmail.com>
// SPDX-FileCopyrightText: 2026 corresp0nd <46357632+corresp0nd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 marc-pelletier <113944176+marc-pelletier@users.noreply.github.com>
// SPDX-FileCopyrightText: 2026 taydeo <tay@funkystation.org>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Robust.Shared.Configuration;

namespace Content.Shared._Funkystation.CCVars;

[CVarDefs]
public sealed class CCVars_Funky
{
    /// <summary>
    /// If the content warning should be displayed.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningDisplay =
        CVarDef.Create("cw.display", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// If ignoring the content warning should kick you from the server.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningKickOnIgnore =
        CVarDef.Create("cw.kick", true, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// If the content warning popup was acknowledged.
    /// </summary>
    public static readonly CVarDef<bool> ContentWarningAcknowledged =
        CVarDef.Create("cw.acknowledged", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
