// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dataset;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusSystem))]
public sealed partial class PolybiusForgottenComponent : Component
{
    [DataField]
    public ProtoId<LocalizedDatasetPrototype> Names = "PolybiusForgottenNames";

    [DataField]
    public ProtoId<LocalizedDatasetPrototype> TrailOffs = "PolybiusForgottenTrailOffs";

    [DataField]
    public LocId Popup = "polybius-forgotten-popup";

    public string? LastWord;
}
