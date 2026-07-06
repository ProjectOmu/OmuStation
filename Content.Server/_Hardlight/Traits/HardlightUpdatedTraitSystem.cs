using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Traits;
using Content.Shared.Preferences;

namespace Content.Server._Hardlight.Traits;

public sealed class HardlightUpdatedTraitSystem : EntitySystem
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!; // HardLight
    [Dependency] private readonly MindSystem _mind = default!; // HardLight
    [Dependency] private readonly TraitSystem _traits = default!; // HardLight

    public void ApplySelectedTraits(EntityUid original, EntityUid clone) // HardLight
    {
        if (!_mind.TryGetMind(original, out _, out var mind) ||
            mind.UserId == null ||
            _prefs.GetPreferences(mind.UserId.Value).SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        // Clone equipment separately; replay only the selected trait components here.
        _traits.ApplyProfileTraits(clone, profile, addTraitGear: false);
    }
}
