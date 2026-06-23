using Content.Omu.Shared.Botany.PlantAnalyzer;

namespace Content.Omu.Client.PlantAnalyzer.UI;

public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    private PlantAnalyzerWindow? _window;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new PlantAnalyzerWindow();
        _window.OnClose += Close;
        _window.OpenCenteredLeft();
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

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is PlantAnalyzerScannedState scanned)
            _window.Populate(scanned);
    }
}
