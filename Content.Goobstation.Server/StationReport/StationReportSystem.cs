using Content.Goobstation.Common.StationReport;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.Paper;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.StationReportSystem;

public sealed class StationReportSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!; // Omu
    private int _roundId; // Omu: round number in Discord reports

    public override void Initialize()
    {
        //subscribes to the endroundevent
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarted);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    private void OnRoundStarted(RoundStartingEvent ev) // Omu: Store round number for later
    {
        _roundId = ev.Id;
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent args)
    {
        var reportDefaultForm = Loc.GetString(((PaperComponent) _prototypeManager.Index("NanoRepStationReport").Components["Paper"].Component).Content); // Omu: Don't send a report that hasn't been filled in

        //locates the first entity with StationReportComponent then stops
        string? stationReportText = null;
        var query = EntityQueryEnumerator<StationReportComponent>();
        while (query.MoveNext(out var uid, out var tablet))//finds the first entity with stationreport
        {
            if (!TryComp<PaperComponent>(uid, out var paper))
                continue; // Omu: Make sure to check all station reports, just in case there are multiple

            stationReportText = paper.Content;

            // Omu: Add stamps
            if (paper.StampedBy.Count > 0)
            {
                stationReportText += $"{Environment.NewLine}{Environment.NewLine}Stamps:{Environment.NewLine}";
                foreach (var stamp in paper.StampedBy)
                {
                    if (Loc.TryGetString(stamp.StampedName, out var name))
                        stationReportText += $"[color={stamp.StampedColor.ToHexNoAlpha()}]⟦{name}⟧[/color] ";
                }
            }

            if (stationReportText != reportDefaultForm) // Omu: Make sure to check all station reports, just in case there are multiple
                break;
        }

        if(stationReportText != reportDefaultForm) // Omu: Don't send a report that hasn't been filled in
            BroadcastStationReport(stationReportText);
    }

    //sends a networkevent to tell the client to update the stationreporttext when recived
    public void BroadcastStationReport(string? stationReportText)
    {
        RaiseNetworkEvent(new StationReportEvent(stationReportText));//to send to client
        RaiseLocalEvent(new StationReportEvent($"{stationReportText}{Environment.NewLine}-# Round {_roundId}"));//to send to discord intergration // Omu: Add round number
    }
}
