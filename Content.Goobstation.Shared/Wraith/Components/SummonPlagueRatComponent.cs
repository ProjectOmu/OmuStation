using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.Wraith.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SummonPlagueRatComponent : Component
{
    [DataField]
    public EntProtoId RatProto = "MobPlagueRatSmall";
// Omu edit start
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan TimerLength = TimeSpan.FromSeconds(10);

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan EndTimer = TimeSpan.Zero; // Omu edit end
}
