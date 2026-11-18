// SPDX-License-Identifier: MIT

using Content.Omu.Server.StationEvents.Events;

namespace Content.Omu.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(CrewletOmenRule))]
public sealed partial class CrewletOmenRuleComponent : Component;
