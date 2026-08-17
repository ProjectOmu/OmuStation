using Content.Shared.Silicons.StationAi;
using Content.Shared.Examine;

namespace Content.Omu.Shared.StationAi;

public partial class SharedStationAiWhitelistExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationAiWhitelistComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, StationAiWhitelistComponent component, ExaminedEvent args)
    {
        if (!component.Enabled)
            return;

        args.PushMarkup(Loc.GetString("station-ai-whitelist-examine"));
    }
}
