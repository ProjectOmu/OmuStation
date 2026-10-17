// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;

namespace Content.Omu.Common._Trauma.Throwing;

/// <summary>
/// Component that is added on both server and client, but only removed by server.
/// Controls predicting thrown item physics at the right time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PredictedThrownItemComponent : Component;
