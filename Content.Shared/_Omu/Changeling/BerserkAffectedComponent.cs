using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared._Omu.Changeling;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BerserkAffectedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid ExcludedEntity;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan NextAttack = TimeSpan.Zero;

    [DataField]
    public List<LocId> AngerMessages = new()
    {
        "berserkchemical-rage",
        "berserkchemical-hate",
        "berserkchemical-anger",
        "berserkchemical-mom"
    };
}
