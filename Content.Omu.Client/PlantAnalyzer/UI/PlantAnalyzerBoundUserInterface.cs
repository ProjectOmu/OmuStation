using Content.Omu.Shared.PlantAnalyzer;

namespace Content.Omu.Client.PlantAnalyzer.UI;

// this is the bridge between the server and the window.
// the server sends a PlantAnalyzerScannedSeedMessage after a successful scan,
// UpdateState catches it and forwards it to the window to display.
// the window itself does not talk to the server at all - it is purely display.
public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        // create the window and hook its close button to close the UI properly
        _window = new PlantAnalyzerWindow
        {
            Title = Loc.GetString("plant-analyzer-interface-title"),
        };
        _window.OnClose += Close;

        // open on the left side so it does not cover the world view
        _window.OpenCenteredLeft();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null)
            return;

        // only one state type triggers a full repopulate - the scan result.
        // mode-only updates (from verb toggle) are ignored since there are no buttons to sync.
        if (state is PlantAnalyzerScannedSeedMessage msg)
            _window.Populate(msg);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        // unhook the event before orphaning to avoid a dangling reference
        if (_window != null)
            _window.OnClose -= Close;

        _window?.Orphan();
    }
}
