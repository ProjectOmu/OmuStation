using Content.Server._Omu.Preferences;
using Content.Server.Mind;
using Content.Server.Traits;

namespace Content.Server._Hardlight.Traits;

public sealed class HardlightUpdatedTraitSystem : EntitySystem
{
    [Dependency] private readonly CharacterQueueSystem _characterQueue = default!; // Omu
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly TraitSystem _traits = default!;

    public bool ApplySelectedTraits(EntityUid original, EntityUid clone)
    {
        if (!_mind.TryGetMind(original, out _, out var mind) ||
            mind.UserId == null ||
            _characterQueue.TryGetCurrentCharacter(mind.UserId.Value) is not { } profile) // Omu
            return false;

        // Clone equipment separately; replay only the selected trait components here.
        _traits.ApplyProfileTraits(clone, profile, addTraitGear: false);
        return true;
    }
}
