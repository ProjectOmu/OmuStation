using Content.Server._Omu.Traits;
using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Roles.Jobs;
using Content.Server.Traits;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.Server._Hardlight.Traits;

public sealed class HardlightUpdatedTraitSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly TraitSystem _traits = default!;

    public void ApplySelectedTraits(EntityUid original, EntityUid clone)
    {
        if (!_mind.TryGetMind(original, out _, out var mind) ||
            mind.UserId == null ||
            _prefs.GetPreferences(mind.UserId.Value).SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        // Clone equipment separately; replay only the selected trait components here.
        _traits.ApplyProfileTraits(clone, profile, addTraitGear: false);
    }
}
