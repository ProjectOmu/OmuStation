using Content.Server.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Events;
using Content.Shared.Database;

using Content.Omu.Server.MetashieldInformationBagles.Components;

namespace Content.Omu.Server.MetashieldInformationBagles;

public sealed class MetashieldInformationBagleSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [ViewVariables] private List<(TimeSpan, string)> _allBrokenMetashields = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MetashieldInformationBagleComponent, UseInHandEvent>(OnBreak);
        SubscribeLocalEvent<RoundStartingEvent>(ResetOnRoundStart);
    }
    public void OnBreak(Entity<MetashieldInformationBagleComponent> ent, ref UseInHandEvent args)
    {
        var currentTime = _ticker.RoundDuration();
        string breakMessage = ent.Comp.MetashieldBroken;
        _adminLogger.Add(LogType.MetashieldBreak, $"broke the metashield {breakMessage}");
        _allBrokenMetashields.Add((currentTime, breakMessage));
        return;
    }
    private void ResetOnRoundStart(RoundStartingEvent ev)
    {
        _allBrokenMetashields = new();
    }
    public List<(TimeSpan, string)> GetBrokenMetashieldList()
    {
        return _allBrokenMetashields;
    }
}
