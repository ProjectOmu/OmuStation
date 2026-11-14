using Robust.Shared.Timing;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;

[ImplicitDataDefinitionForInheritors]
public abstract partial class ChaosFactor
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _lastRun = TimeSpan.Zero;

    [DataField]
    public TimeSpan Interval;

    public virtual void Initialize()
    {
        IoCManager.InjectDependencies(this);
    }

    public float CalculateTimeAdjustedChaos(ChaosContext context)
    {
        var curTime = _timing.CurTime;
        if (_lastRun == TimeSpan.Zero)
            _lastRun = curTime;

        if (curTime.Subtract(_lastRun) < Interval)
            return 0;

        var elapsed = context.FrameTime;
        if (Interval != TimeSpan.Zero)
            elapsed = (float) curTime.Subtract(_lastRun).TotalSeconds;

        _lastRun = curTime;
        var chaos = CalculateChaos(context) * elapsed;
        SecretDoublePlusMetrics.ChaosFactor.WithLabels(GetType().Name, chaos < 0 ? "negative" : "positive").Inc(MathF.Abs(chaos));

        return chaos;
    }

    protected abstract float CalculateChaos(ChaosContext context);
}

public sealed record ChaosContext(float FrameTime, float Ramping);
