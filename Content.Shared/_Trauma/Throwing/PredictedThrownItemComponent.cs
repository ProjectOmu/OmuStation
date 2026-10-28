// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Trauma.Throwing;

/// <summary>
/// Component that is added on both server and client, but only removed by server.
/// Controls predicting thrown item physics at the right time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PredictedThrownItemComponent : Component
{
    /// <summary>
    /// Omu - the entity that threw this, only their client predicts it.
    /// Matches how PlayPredicted etc work, where the user is the only client that predicted something.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Thrower;

    /// <summary>
    /// Omu - clientside, whether this client added the component itself by predicting a throw.
    /// Used for throws without a thrower such as ejected casings.
    /// </summary>
    [ViewVariables]
    public bool LocallyPredicted;
}
