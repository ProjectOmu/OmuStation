using Content.Shared._DV.PlantAnalyzer;

namespace Content.Client._DV.PlantAnalyzer.UI;

public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = new PlantAnalyzerWindow
        {
            Title = Loc.GetString("plant-analyzer-interface-title"),
        };
        _window.OnClose += Close;
        _window.OpenCenteredLeft();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null)
            return;

        if (state is PlantAnalyzerScannedSeedMessage msg)
            _window.Populate(msg);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
            _window.OnClose -= Close;

        _window?.Orphan();
    }
}
