using System.Linq;
using Content.Omu.Shared.StationEvents.SecretDoublePlus;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;

[UsedImplicitly]
public sealed partial class AntagAmbientFactor : ChaosFactor
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    [DataField]
    public ProtoId<AmbientAntagChaosRatesPrototype> ChaosRates;

    private MobStateSystem _mobs = default!;
    private SharedObjectivesSystem _objectives = default!;
    private SharedRoleSystem _roles = default!;

    private readonly Dictionary<ProtoId<AntagPrototype>, PresenceChaosEntry> _presenceTypes = new();
    private readonly Dictionary<Type, ObjectiveChaosEntry> _objectiveTypes = new();
    private readonly Dictionary<EntityUid, TimeSpan> _firstSeen = new();

    public override void Initialize()
    {
        base.Initialize();

        _mobs = _entityManager.System<MobStateSystem>();
        _objectives = _entityManager.System<SharedObjectivesSystem>();
        _roles = _entityManager.System<SharedRoleSystem>();

        var rates = _proto.Index(ChaosRates);
        foreach (var entry in rates.Presence.Values)
        {
            foreach (var antagId in entry.Antags)
            {
                _presenceTypes.Add(antagId, entry);
            }
        }

        foreach (var entry in rates.Objectives.Values)
        {
            foreach (var component in entry.Components)
            {
                _objectiveTypes.Add(_factory.GetRegistration(component).Type, entry);
            }
        }
    }

    protected override float CalculateChaos(ChaosContext context)
    {
        var chaos = 0f;
        var curTime = _timing.CurTime;

        var mindQuery = _entityManager.EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindId, out var mind))
        {
            if (!_roles.MindIsAntagonist(mindId))
                continue;

            if (mind.OwnedEntity is not { } body || !_mobs.IsAlive(body))
                continue;

            chaos += CalculateChaosForRole((mindId, mind), curTime);
            chaos += mind.Objectives
                .Where(objective => !_objectives.IsCompleted(objective, (mindId, mind)))
                .Sum(objective => CalculateChaosForObjective(objective, curTime));
        }

        _adminLogger.Add(LogType.SecretDoublePlus, $"Antag Ambient Factor adding {chaos} chaos per second");
        return chaos;
    }

    private float CalculateChaosForRole(Entity<MindComponent?> mind, TimeSpan curTime)
    {
        var chaos = 0f;
        foreach (var role in _roles.MindGetAllRoleInfo(mind))
        {
            if (!_presenceTypes.TryGetValue(role.Prototype, out var ambient))
                continue;

            var firstSeen = GetFirstSeen(mind.Owner, curTime);
            var delta = ambient.Rates.Chaos * GetTimeDecayMultiplier(ambient.Rates, firstSeen, curTime);

            var sign = delta < 0 ? SecretDoublePlusMetrics.Negative : SecretDoublePlusMetrics.Positive;
            SecretDoublePlusMetrics.AntagAmbientChaos.WithLabels(sign, role.Prototype).Inc(MathF.Abs(delta));

            chaos += delta;
        }

        return chaos;
    }

    private float CalculateChaosForObjective(EntityUid objective, TimeSpan curTime)
    {
        var chaos = 0f;
        foreach (var component in _entityManager.GetComponents(objective))
        {
            if (!_objectiveTypes.TryGetValue(component.GetType(), out var ambient))
                continue;

            var firstSeen = GetFirstSeen(objective, curTime);
            var delta = ambient.Rates.Chaos * GetTimeDecayMultiplier(ambient.Rates, firstSeen, curTime);

            var sign = delta < 0 ? SecretDoublePlusMetrics.Negative : SecretDoublePlusMetrics.Positive;
            SecretDoublePlusMetrics.AntagAmbientChaos.WithLabels(sign, component.GetType().Name).Inc(MathF.Abs(delta));

            chaos += delta;
        }

        return chaos;
    }

    private TimeSpan GetFirstSeen(EntityUid id, TimeSpan curTime)
    {
        if (_firstSeen.TryGetValue(id, out var firstSeen))
            return firstSeen;

        _firstSeen[id] = curTime;
        return curTime;
    }

    private float GetTimeDecayMultiplier(AmbientChaos ambient, TimeSpan firstSeen, TimeSpan curTime)
    {
        if (ambient.ExpectedTime == TimeSpan.Zero || firstSeen.Add(ambient.ExpectedTime) >= curTime)
            return 1f;

        var difference = curTime.Subtract(ambient.ExpectedTime).Subtract(firstSeen);
        var ratio = ambient.DecayPeriod != TimeSpan.Zero ? difference / ambient.DecayPeriod : 1f;

        return 1f - Math.Clamp((float) ratio, 0, 1);
    }
}
