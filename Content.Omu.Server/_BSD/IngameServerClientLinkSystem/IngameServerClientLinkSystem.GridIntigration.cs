using System.Linq;
using Content.Omu.Server._BSD.IngameServerClientLinkSystem.Components;

namespace Content.Omu.Server._BSD.IngameServerClientLinkSystem;

//Think this is obsolite rn but you never know
public sealed partial class BSDIngameServerClientLinkSystem : EntitySystem
{
    private string[] _defaultGridServerTypes = ["MaterialTransit"];
    private string[] _defaultGridClientTypes = [];
    public IngameServerClientLinkInfrastructureComponent TryAddSCICompToGridStandardGridSetup(EntityUid grid)
    {
        var comp = EnsureComp<IngameServerClientLinkInfrastructureComponent>(grid);
        foreach (var iterator in _defaultGridClientTypes)
        {
            comp.ClientTypes.Add(iterator);
        }
        foreach (var iterator in _defaultGridServerTypes)
        {
            comp.ServerTypes.Add(iterator);
        }
        return comp;
    }
}