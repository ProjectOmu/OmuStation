// SPDX-License-Identifier: AGPL-3.0-or-later

// whole file is Omu

using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

[RegisterComponent]
public sealed partial class DropkickedComponent : Component
{
    [DataField]
    public TimeSpan BreakThroughEnd = TimeSpan.Zero;

    [DataField]
    public List<EntProtoId> BreakableWindows = new();

    [DataField]
    public List<EntProtoId> BreakableGrilles = new();

    [DataField]
    public SoundSpecifier? BreakThroughSound;
}
