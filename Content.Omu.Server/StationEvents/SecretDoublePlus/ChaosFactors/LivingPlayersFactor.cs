using System.Linq;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using JetBrains.Annotations;
using Robust.Server.Player;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;

[UsedImplicitly]
public sealed partial class LivingPlayersFactor : ChaosFactor
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    ///   How much to change chaos per second per player in control of a humanoid.
    /// </summary>
    [DataField]
    public float LivingChaosChange;

    protected override float CalculateChaos(ChaosContext context)
    {
        var playerCount = 0;
        foreach (var player in _playerManager.Sessions.ToList())
        {
            if (player.AttachedEntity == null)
                continue;

            // Has to be humanoid
            if (!_entityManager.HasComponent<HumanoidAppearanceComponent>(player.AttachedEntity))
                continue;

            // But can't be a ghost
            if (_entityManager.HasComponent<GhostComponent>(player.AttachedEntity))
                continue;

            playerCount++;
        }

        return playerCount * LivingChaosChange * context.Ramping;
    }
}
