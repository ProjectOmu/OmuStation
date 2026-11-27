using System.Linq;
using Content.Shared.Ghost;
using JetBrains.Annotations;
using Robust.Server.Player;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;

[UsedImplicitly]
public sealed partial class DeadPlayersFactor : ChaosFactor
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    /// <summary>
    ///   How much to change chaos per second per player in control of a ghost which can return to a body.
    /// </summary>
    [DataField]
    public float DeadChaosChange;

    protected override float CalculateChaos(ChaosContext context)
    {
        var playerCount = 0;
        foreach (var player in _playerManager.Sessions.ToList())
        {
            if (player.AttachedEntity == null)
                continue;

            if (_entityManager.TryGetComponent<GhostComponent>(player.AttachedEntity, out var ghost) && ghost.CanReturnToBody)
                playerCount++;
        }

        return playerCount * DeadChaosChange;
    }
}
