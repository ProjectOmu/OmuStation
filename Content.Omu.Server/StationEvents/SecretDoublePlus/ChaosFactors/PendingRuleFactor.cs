using Content.Server.Administration.Logs;
using Content.Server.Antag.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;

[UsedImplicitly]
public sealed partial class PendingRuleFactor : ChaosFactor
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    /// <summary>
    ///   Time in seconds it takes for a pending rule to contribute its full chaos value.
    ///   This is on top of the initial chaos value added when the rule was selected.
    ///   If the rule is pending for less than this time, less chaos will be added;
    ///   if the rule is pending for more than this time, more chaos will be added.
    /// </summary>
    [DataField]
    public float FullChaosInterval = 1200;

    protected override float CalculateChaos(ChaosContext context)
    {
        var chaos = 0f;
        var antagsQuery = _entityManager.EntityQueryEnumerator<DelayedStartRuleComponent, AntagSelectionComponent, GameRuleComponent>();
        while (antagsQuery.MoveNext(out _, out var antag, out var rule))
        {
            var antagChaos = 0f;

            // Try to read the per definition chaos first
            foreach (var (definition, sessions) in antag.PreSelectedSessions)
            {
                antagChaos += (definition.ChaosScore ?? 0) * sessions.Count / FullChaosInterval;
            }

            // Fallback to the overall rule chaos if we didn't find any on the definitions
            if (antagChaos == 0f)
                antagChaos = (rule.ChaosScore ?? 0) / FullChaosInterval;

            chaos += antagChaos;
        }

        if (chaos > 0)
            _adminLogger.Add(LogType.SecretDoublePlus, $"Pending Rule Factor adding {chaos} chaos per second");

        return chaos;
    }
}
