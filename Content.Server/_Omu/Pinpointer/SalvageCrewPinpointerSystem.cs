using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Omu.Pinpointer;

/// <summary>
/// This handles non-movement alerts of salvage crew IDs.
/// </summary>
public sealed class SalvageCrewPinpointerSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _speech = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SalvageCrewPinpointerComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float delta)
    {
        base.Update(delta);
        var query = EntityQueryEnumerator<PinpointerComponent, SalvageCrewPinpointerComponent>();
        while (query.MoveNext(out var uid, out var pin, out var salv))
        {
            if (salv.NextCheck == TimeSpan.Zero)
            {
                salv.NextCheck = _gameTiming.CurTime + salv.CheckInterval;
                continue;
            }

            if (_gameTiming.CurTime < salv.NextCheck)
                continue;

            if (pin.Targets.Count < 1)
                continue;

            salv.NextCheck = _gameTiming.CurTime + salv.CheckInterval;
            if (salv.PreviousPositionWorld == _transform.GetWorldPosition(pin.Targets.First()) ||
                salv.PreviousPositionTileLocal == _transform.GetGridOrMapTilePosition(pin.Targets.First()))
            {
                if (!TryComp<IdCardComponent>(pin.Targets.First(), out var card))
                    continue;
                salv.FailedChecks++;
                _speech.TrySendInGameICMessage(uid,
                    Loc.GetString("salvage-crew-pinpointer-chat-stationary",
                            ("name", (card.FullName ?? "ERROR")),
                            ("time", FormatFailedChecks(salv))),
                    InGameICChatType.Speak,
                    false);
            }
            else
            {
                salv.FailedChecks = 0;
            }
            salv.PreviousPositionWorld = _transform.GetWorldPosition(pin.Targets.First());
            salv.PreviousPositionTileLocal = _transform.GetGridOrMapTilePosition(pin.Targets.First());
        }
    }

    private void OnExamined(EntityUid entity, SalvageCrewPinpointerComponent comp, ExaminedEvent args)
    {
        if (!TryComp<PinpointerComponent>(entity, out var pinpointer))
            return;
        if (pinpointer.Targets.Count == 0)
            return;
        if (!TryComp<IdCardComponent>(pinpointer.Targets.First(), out var idCard))
            return;
        if (!(TryComp<TransformComponent>(pinpointer.Targets.First(), out var cardXform) &&
            TryComp<TransformComponent>(entity, out var pinXform)))
            return;


        if (!args.IsInDetailsRange || pinpointer.TargetName == null)
            return;
        args.PushMarkup(Loc.GetString("examine-pinpointer-linked", ("target", idCard.FullName ?? "ERROR")));
        args.PushMarkup(Loc.GetString("salvage-crew-pinpointer-examine-stationary", ("time", FormatFailedChecks(comp))));
        if (cardXform.MapID != pinXform.MapID)
            args.PushMarkup(Loc.GetString("salvage-crew-pinpointer-examine-off-map"));
        else
        {
            var dist = (_transform.GetWorldPosition(cardXform) - _transform.GetWorldPosition(pinXform)).Length();
            args.PushMarkup(Loc.GetString("salvage-crew-pinpointer-examine-distance", ("dist", dist.ToString("N0"))));
        }
    }

    private string FormatFailedChecks(SalvageCrewPinpointerComponent pin) =>
        ((double) pin.FailedChecks * pin.CheckInterval.TotalMinutes).ToString("N0");

}
