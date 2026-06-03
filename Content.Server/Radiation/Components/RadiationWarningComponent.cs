// SPDX-FileCopyrightText: 2026 puntsss <bex.ish.aholic@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server.Radiation.Components;

[RegisterComponent]
public sealed partial class RadiationWarningComponent : Component
{
    public TimeSpan NextWarningTime = TimeSpan.Zero;

    public int LastWarningStage = 0;
}
