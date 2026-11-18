using Robust.Shared.Prototypes;
using Content.Shared.Silicons.Laws;

namespace Content.Server._Omu.SELF;
/// <summary>
/// Adds a law no matter the default lawset.
/// Switching borg chassis type keeps this law.
/// </summary>
[RegisterComponent]
public sealed partial class FreedBorgComponent : Component
{
    [ViewVariables]
    public ProtoId<SiliconLawsetPrototype> Lawset = "FreeLawset";
}
