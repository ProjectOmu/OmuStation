// SPDX-License-Identifier: MIT

using Content.Omu.Server.Objectives.Systems;

namespace Content.Omu.Server.Objectives.Components;

[RegisterComponent, Access(typeof(TargetBodyConditionSystem))]
public sealed partial class TargetBorgedConditionComponent : Component;
