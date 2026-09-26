using System.Linq;
using Content.Client.Lobby.UI.Loadouts;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<SpeciesPrototype> _subspecies = [];

    private void UpdateSubspecies()
    {
        CSubspecies.Visible = false;
        _subspecies = [];
        SubspeciesButton.Clear();

        var species = _species.Find(x => x.ID == Profile?.Species) ?? _species.First();

        if(species.HasSubspecies == false && species.SubspeciesOf == null)
            return;

        List<SpeciesPrototype> subspecies = [];
        var selected = 0;

        if (species.HasSubspecies)
        {
            List<SpeciesPrototype> allSubspecies = [.. _prototypeManager.EnumeratePrototypes<SpeciesPrototype>().Where(p => p.SubspeciesOf == species.ID)];
            allSubspecies.Sort((a, b) => string.Compare(a.SubspeciesName ?? a.Name, b.SubspeciesName ?? b.Name, StringComparison.OrdinalIgnoreCase));

            subspecies.Add(species);
            subspecies.AddRange(allSubspecies);
        }
        else if (species.SubspeciesOf != null)
        {
            List<SpeciesPrototype> allSubspecies = [.. _prototypeManager.EnumeratePrototypes<SpeciesPrototype>().Where(p => p.SubspeciesOf == species.SubspeciesOf)];
            allSubspecies.Sort((a, b) => string.Compare(a.SubspeciesName ?? a.Name, b.SubspeciesName ?? b.Name, StringComparison.OrdinalIgnoreCase));
            var parent = _prototypeManager.Index(species.SubspeciesOf);

            subspecies.Add(parent);
            subspecies.AddRange(allSubspecies);
            selected = subspecies.IndexOf(species);
        }

        if (subspecies.Count == 0)
            return;

        for (var i = 0; i < subspecies.Count; i++)
        {
            _subspecies.Add(subspecies[i]);

            var name = Loc.GetString(subspecies[i].SubspeciesName == null ? subspecies[i].Name : subspecies[i].SubspeciesName!.Value);
            SubspeciesButton.AddItem(name, i);
        }


        SubspeciesButton.SelectId(selected);
        CSubspecies.Visible = true;
    }
}
