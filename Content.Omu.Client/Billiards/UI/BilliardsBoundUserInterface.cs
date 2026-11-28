// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client.UserInterface;
using Content.Omu.Shared.Billiards;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Billiards.UI;

public sealed class BilliardsBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey), IBuiPreTickUpdate
{
    private const int ObjectBalls = BilliardsSimulation.BallCount - 1;
    private const int SolidBall = 1;
    private const int StripeBall = 9;
    private const int EightBall = 8;
    private const int GroupSize = 7;

    private BilliardsWindow? _window;
    private InputCoalescer<BilliardsAimMessage> _aim;
    private bool _wasFinished;
    private bool _wasIdle;
    private bool _wasHoldingCue;
    private bool _couldSkip;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BilliardsWindow>();
        _window.SetTable(Owner);
        _window.OnAim += (direction, power) => _aim.Set(new BilliardsAimMessage(direction, power));
        _window.OnShoot += (direction, power) => SendPredictedMessage(new BilliardsShootMessage(direction, power));
        _window.OnPlace += spot => SendPredictedMessage(new BilliardsPlaceCueMessage(spot));
        _window.OnNewGame += mode => SendPredictedMessage(new BilliardsNewGameMessage(mode));
        _window.OnJoin += () => SendPredictedMessage(new BilliardsJoinMessage());
        _window.OnLeave += () => SendPredictedMessage(new BilliardsLeaveMessage());
        _window.OnSkip += () => SendPredictedMessage(new BilliardsSkipMessage());

        _wasFinished = EntMan.TryGetComponent(Owner, out BilliardsTableComponent? table) && table.Finished;
        Update();
    }

    public override void Update()
    {
        if (_window == null
            || PlayerManager.LocalEntity is not { } user
            || !EntMan.TryGetComponent(Owner, out BilliardsTableComponent? table))
        {
            return;
        }

        var billiards = EntMan.System<BilliardsTableSystem>();
        Entity<BilliardsTableComponent> ent = (Owner, table);
        var netUser = EntMan.GetNetEntity(user);
        var left = SharedBilliardsTableSystem.ObjectBallsLeft(table);
        var groupBalls = table.Players.Select((_, index) => GroupBall(table, index)).ToList();

        _window.SetMode(table.Mode);
        _window.SetAim(billiards.CanShoot(ent, user), table.BallInHand, table.AimPower);
        _window.SetPocketed(ObjectBalls - left, ObjectBalls);
        var lifeSlots = table.Mode == BilliardsMode.Killer ? table.KillerLives : 0;
        _window.SetPlayers(table.Players, groupBalls, lifeSlots, table.Turn);
        _window.SetActions(SharedBilliardsTableSystem.PlayerLimit(table.Mode) > 0,
            table.Players.Any(player => player.Entity == netUser),
            billiards.CanJoin(ent, user),
            billiards.CanLeave(ent, user),
            billiards.CanSkip(ent, user),
            billiards.CanStartGame(ent, user));

        if (table.Finished && !_wasFinished)
            _window.ShowCallout(billiards.FinishMessage(table));

        _wasFinished = table.Finished;
    }

    public void ShowTurnCallout(bool ballInHand)
    {
        var callout = ballInHand ? "billiards-callout-ball-in-hand" : "billiards-callout-your-turn";
        _window?.ShowCallout(Loc.GetString(callout));
    }

    void IBuiPreTickUpdate.PreTickUpdate()
    {
        if (_aim.CheckIsModified(out var message))
            SendPredictedMessage(message);

        if (PlayerManager.LocalEntity is not { } user
            || !EntMan.TryGetComponent(Owner, out BilliardsTableComponent? table))
        {
            return;
        }

        var billiards = EntMan.System<BilliardsTableSystem>();
        var idle = billiards.IsIdle(table);
        var holdingCue = billiards.HoldsCue(user);
        var canSkip = billiards.CanSkip((Owner, table), user);
        if (idle == _wasIdle && holdingCue == _wasHoldingCue && canSkip == _couldSkip)
            return;

        _wasIdle = idle;
        _wasHoldingCue = holdingCue;
        _couldSkip = canSkip;
        Update();
    }

    private static int GroupBall(BilliardsTableComponent table, int index)
    {
        if (table.Mode != BilliardsMode.SolidsStripes || table.SolidsPlayer < 0)
            return 0;

        if (table.Players[index].Score >= GroupSize)
            return EightBall;

        return index == table.SolidsPlayer ? SolidBall : StripeBall;
    }
}
