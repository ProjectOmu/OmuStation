using Content.Shared.Roles;
using Robust.Shared.GameStates;

namespace Content.Shared._Starlight.Roles.Components;

/// <summary>
/// Added to mind role entities to tag that they are a thief.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SELFAgentRoleComponent : BaseMindRoleComponent;
