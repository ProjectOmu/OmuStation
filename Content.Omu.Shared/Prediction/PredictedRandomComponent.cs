// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Prediction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PredictedRandomSystem))]
public sealed partial class PredictedRandomComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public int Rolls;
}
