using Robust.Shared.Timing;
using Content.Shared.StatusIcon;
using Content.Goobstation.Shared.CustomFactionIcons;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(MoralePassedSystem), typeof(MoraleSystem))]
public sealed partial class MoralePassedComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Time = 60f; //One would have thought a timespan would have been better. One was wrong.

    [ViewVariables(VVAccess.ReadOnly)]
    public float UpdateAccumulator = 0f;

    [DataField]
    public FactionIconPrototype Faction;
}

public sealed class MoralePassedSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public string faction = "MoralePassedFaction";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MoralePassedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MoralePassedComponent, ComponentShutdown>(OnShutdown);
    }
    private void OnStartup(EntityUid uid, MoralePassedComponent component, ComponentStartup args)
    {
        var userFactionIcons = EnsureComp<CustomFactionIconsComponent>(uid);
        userFactionIcons.FactionIcons.Add(faction);
        Dirty(uid, userFactionIcons);
    }

    private void OnShutdown(EntityUid uid, MoralePassedComponent component, ComponentShutdown args)
    {
        if (!TryComp<CustomFactionIconsComponent>(uid, out var userFactionIcons))
            return;

        userFactionIcons.FactionIcons.Remove(faction);
        Dirty(uid, userFactionIcons);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var query = EntityManager.EntityQuery<MoralePassedComponent>();

        foreach (var comp in query)
        {
            if (TerminatingOrDeleted(comp.Owner))
                continue;

            comp.UpdateAccumulator += frameTime;

            if (comp.UpdateAccumulator >= comp.Time)
            {
                RemCompDeferred<MoralePassedComponent>(comp.Owner);
            }
        }
    }
}
