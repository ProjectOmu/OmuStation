// SPDX-License-Identifier: MIT

using Robust.Shared.Map;

namespace Content.Omu.Common.Construction;

[ByRefEvent]
public record struct ConstructionReachEvent(string Prototype, EntityUid User, EntityCoordinates Target, Angle Rotation);
