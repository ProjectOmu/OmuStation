// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.SSDIndicator;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.Billiards;

public abstract class SharedBilliardsTableSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedUserInterfaceSystem UserInterface = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public const float MinShotPower = 0.03f;

    private const float MinAimLength = 0.001f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BilliardsTableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BilliardsTableComponent, InteractUsingEvent>(OnInteractUsing);

        Subs.BuiEvents<BilliardsTableComponent>(BilliardsUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<BilliardsAimMessage>(OnAim);
            subs.Event<BilliardsShootMessage>(OnShoot);
            subs.Event<BilliardsPlaceCueMessage>(OnPlaceCue);
            subs.Event<BilliardsNewGameMessage>(OnNewGame);
            subs.Event<BilliardsJoinMessage>(OnJoin);
            subs.Event<BilliardsLeaveMessage>(OnLeave);
            subs.Event<BilliardsSkipMessage>(OnSkip);
        });
    }

    public static int ObjectBallsLeft(BilliardsTableComponent table)
    {
        return table.Balls.Skip(1).Count(ball => !ball.Pocketed);
    }

    public static int PlayerLimit(BilliardsMode mode)
    {
        return mode switch
        {
            BilliardsMode.Casual or BilliardsMode.Killer => 8,
            BilliardsMode.SolidsStripes => 2,
            BilliardsMode.FewestShots => 1,
            _ => 0,
        };
    }

    public string FinishMessage(BilliardsTableComponent table)
    {
        if (table.Winner < 0 || table.Winner >= table.Players.Count)
        {
            return Loc.GetString(table.Mode switch
            {
                BilliardsMode.Casual => "billiards-finish-tie",
                BilliardsMode.FewestShots => "billiards-finish-scratched",
                _ => "billiards-finish-game-over",
            });
        }

        var winner = table.Players[table.Winner];
        return table.Mode switch
        {
            BilliardsMode.Casual =>
                Loc.GetString("billiards-finish-casual", ("player", winner.Name), ("score", winner.Score)),
            BilliardsMode.SolidsStripes => Loc.GetString("billiards-finish-rack", ("player", winner.Name)),
            BilliardsMode.Killer => Loc.GetString("billiards-finish-killer", ("player", winner.Name)),
            _ => Loc.GetString("billiards-finish-cleared", ("shots", winner.Score)),
        };
    }

    public bool HoldsCue(EntityUid user)
    {
        return _hands.EnumerateHeld(user).Any(HasComp<BilliardsCueComponent>);
    }

    public bool CanShoot(Entity<BilliardsTableComponent> ent, EntityUid user)
    {
        var table = ent.Comp;
        if (table.ShotEnd != null || table.Balls.Count != BilliardsSimulation.BallCount || !HoldsCue(user))
            return false;

        if (table.Mode == BilliardsMode.Freeplay)
            return true;

        return table.Mode != BilliardsMode.Unselected
               && !table.Finished && PlayerIndex(table, user) == table.Turn;
    }

    public bool IsAiming(Entity<BilliardsTableComponent> ent)
    {
        return ent.Comp.Aimer is { } aimer && CanShoot(ent, aimer);
    }

    public bool CanStartGame(Entity<BilliardsTableComponent> ent, EntityUid user)
    {
        var table = ent.Comp;
        return table.ShotEnd == null
               && HoldsCue(user)
               && (table.Players.Count == 0 || IsPlaying(table, user) || IsIdle(table));
    }

    public bool IsIdle(BilliardsTableComponent table)
    {
        return Timing.CurTime - table.LastActivity >= table.IdleReset;
    }

    public bool CanJoin(Entity<BilliardsTableComponent> ent, EntityUid user)
    {
        var table = ent.Comp;
        return table.Players.Count < PlayerLimit(table.Mode) && HoldsCue(user) && !IsPlaying(table, user);
    }

    public bool CanLeave(Entity<BilliardsTableComponent> ent, EntityUid user)
    {
        return ent.Comp.ShotEnd == null && IsPlaying(ent.Comp, user);
    }

    public bool CanSkip(Entity<BilliardsTableComponent> ent, EntityUid user)
    {
        var table = ent.Comp;
        var index = PlayerIndex(table, user);
        if (table.ShotEnd != null || index < 0 || index == table.Turn || table.Turn >= table.Players.Count)
            return false;

        if (!TryGetEntity(table.Players[table.Turn].Entity, out var current))
            return true;

        return !_transform.InRange(ent.Owner, current.Value, table.AbsentRange)
               || _mobState.IsIncapacitated(current.Value)
               || (TryComp<SSDIndicatorComponent>(current.Value, out var ssd) && ssd.IsSSD);
    }

    protected static void AdvanceTurn(BilliardsTableComponent table)
    {
        for (var step = 0; step < table.Players.Count; step++)
        {
            table.Turn = (table.Turn + 1) % table.Players.Count;
            if (table.Mode != BilliardsMode.Killer || table.Players[table.Turn].Score > 0)
                return;
        }
    }

    protected void Finish(Entity<BilliardsTableComponent> ent, int winner, EntityUid? user = null)
    {
        ent.Comp.Finished = true;
        ent.Comp.Winner = winner;
        Popup.PopupPredicted(FinishMessage(ent.Comp), ent, user);
    }

    protected bool TryFinishKiller(Entity<BilliardsTableComponent> ent, EntityUid? user = null)
    {
        var players = ent.Comp.Players;
        var alive = players.Count(player => player.Score > 0);
        if (alive > 1 || (alive == 1 && players.Count == 1))
            return false;

        Finish(ent, players.FindIndex(player => player.Score > 0), user);
        return true;
    }

    protected virtual void UpdateUi(Entity<BilliardsTableComponent> ent)
    {
    }

    protected virtual float ShotSeconds(BilliardsTableComponent table)
    {
        return BilliardsSimulation.MaxSeconds;
    }

    private static bool TryReadAim(Vector2 direction, float power, out Vector2 aim, out float strength)
    {
        var length = direction.Length();
        aim = direction / length;
        strength = Math.Clamp(power, 0f, 1f);
        return float.IsFinite(length) && length >= MinAimLength && float.IsFinite(strength);
    }

    private static int StartingScore(BilliardsTableComponent table, BilliardsMode mode)
    {
        return mode == BilliardsMode.Killer ? table.KillerLives : 0;
    }

    private int PlayerIndex(BilliardsTableComponent table, EntityUid user)
    {
        var netUser = GetNetEntity(user);
        return table.Players.FindIndex(player => player.Entity == netUser);
    }

    private bool IsPlaying(BilliardsTableComponent table, EntityUid user)
    {
        return PlayerIndex(table, user) >= 0;
    }

    private void OnMapInit(Entity<BilliardsTableComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Balls.Count == BilliardsSimulation.BallCount)
            return;

        ent.Comp.Balls = BilliardsSimulation.Rack();
        Dirty(ent);
    }

    private void OnInteractUsing(Entity<BilliardsTableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<BilliardsCueComponent>(args.Used))
            return;

        UserInterface.TryOpenUi(ent.Owner, BilliardsUiKey.Key, args.User, predicted: true);
        args.Handled = true;
    }

    private void OnUiClosed(Entity<BilliardsTableComponent> ent, ref BoundUIClosedEvent args)
    {
        if (ent.Comp.Aimer != args.Actor)
            return;

        ent.Comp.Aimer = null;
        DirtyField(ent.Owner, ent.Comp, nameof(BilliardsTableComponent.Aimer));
    }

    private void OnAim(Entity<BilliardsTableComponent> ent, ref BilliardsAimMessage args)
    {
        if (!CanShoot(ent, args.Actor) || !TryReadAim(args.Direction, args.Power, out var direction, out var power))
            return;

        ent.Comp.Aimer = args.Actor;
        ent.Comp.AimDirection = direction;
        ent.Comp.AimPower = power;
        ent.Comp.LastActivity = Timing.CurTime;
        DirtyFields(ent.Owner,
            ent.Comp,
            null,
            nameof(BilliardsTableComponent.Aimer),
            nameof(BilliardsTableComponent.AimDirection),
            nameof(BilliardsTableComponent.AimPower),
            nameof(BilliardsTableComponent.LastActivity));
        UpdateUi(ent);
    }

    private void OnPlaceCue(Entity<BilliardsTableComponent> ent, ref BilliardsPlaceCueMessage args)
    {
        var table = ent.Comp;
        if (!table.BallInHand
            || !CanShoot(ent, args.Actor)
            || !float.IsFinite(args.Position.X)
            || !float.IsFinite(args.Position.Y)
            || !BilliardsSimulation.CanPlace(table.Balls, args.Position))
        {
            return;
        }

        table.Balls[BilliardsSimulation.CueBall] = new BilliardsBall(args.Position, false);
        table.LastActivity = Timing.CurTime;
        DirtyFields(ent.Owner,
            table,
            null,
            nameof(BilliardsTableComponent.Balls),
            nameof(BilliardsTableComponent.LastActivity));
        UpdateUi(ent);
    }

    private void OnShoot(Entity<BilliardsTableComponent> ent, ref BilliardsShootMessage args)
    {
        if (!CanShoot(ent, args.Actor)
            || !TryReadAim(args.Direction, args.Power, out var direction, out var power)
            || power < MinShotPower)
        {
            return;
        }

        var table = ent.Comp;
        table.ShotVelocity = direction * (power * BilliardsSimulation.MaxSpeed);
        table.Shots++;
        table.Aimer = null;
        table.AimPower = 0f;
        table.BallInHand = false;
        table.ShotStart = Timing.CurTime;
        table.LastActivity = Timing.CurTime;
        table.ShotEnd = Timing.CurTime + TimeSpan.FromSeconds(ShotSeconds(table));
        Dirty(ent);

        _audio.PlayPredicted(table.StrikeSound, ent, args.Actor);
        UpdateUi(ent);
    }

    private void OnNewGame(Entity<BilliardsTableComponent> ent, ref BilliardsNewGameMessage args)
    {
        if (!CanStartGame(ent, args.Actor) || !Enum.IsDefined(args.Mode) || args.Mode == BilliardsMode.Unselected)
            return;

        var table = ent.Comp;
        var limit = PlayerLimit(args.Mode);
        var starter = PlayerIndex(table, args.Actor);
        if (starter < 0)
            table.Players.Clear();
        else if (limit > 0 && starter >= limit)
        {
            var player = table.Players[starter];
            table.Players.RemoveAt(starter);
            table.Players.Insert(limit - 1, player);
        }

        if (table.Players.Count > limit)
            table.Players.RemoveRange(limit, table.Players.Count - limit);

        for (var index = 0; index < table.Players.Count; index++)
        {
            table.Players[index] = table.Players[index] with { Score = StartingScore(table, args.Mode) };
        }

        table.Mode = args.Mode;
        table.Turn = 0;
        table.Aimer = null;
        table.BallInHand = false;
        table.SolidsPlayer = -1;
        table.Finished = false;
        table.Winner = -1;
        table.LastActivity = Timing.CurTime;
        table.Balls = BilliardsSimulation.Rack();
        table.AwaitingBreak = true;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnJoin(Entity<BilliardsTableComponent> ent, ref BilliardsJoinMessage args)
    {
        if (!CanJoin(ent, args.Actor))
            return;

        var table = ent.Comp;
        var name = Identity.Name(args.Actor, EntityManager);
        table.Players.Add(new BilliardsPlayer(GetNetEntity(args.Actor), name, StartingScore(table, table.Mode)));
        table.LastActivity = Timing.CurTime;
        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnLeave(Entity<BilliardsTableComponent> ent, ref BilliardsLeaveMessage args)
    {
        if (!CanLeave(ent, args.Actor))
            return;

        var table = ent.Comp;
        var index = PlayerIndex(table, args.Actor);
        table.Players.RemoveAt(index);
        table.LastActivity = Timing.CurTime;

        if (index < table.Turn)
            table.Turn--;

        if (table.Turn >= table.Players.Count)
            table.Turn = 0;

        if (index < table.Winner)
            table.Winner--;
        else if (index == table.Winner)
            table.Winner = -1;

        if (index < table.SolidsPlayer)
            table.SolidsPlayer--;
        else if (index == table.SolidsPlayer)
            table.SolidsPlayer = table.Players.Count;

        if (table.Mode == BilliardsMode.Killer
            && !table.Finished
            && table.Players.Count > 0
            && !TryFinishKiller(ent, args.Actor)
            && table.Players[table.Turn].Score <= 0)
        {
            AdvanceTurn(table);
        }

        Dirty(ent);
        UpdateUi(ent);
    }

    private void OnSkip(Entity<BilliardsTableComponent> ent, ref BilliardsSkipMessage args)
    {
        if (!CanSkip(ent, args.Actor))
            return;

        AdvanceTurn(ent.Comp);
        ent.Comp.LastActivity = Timing.CurTime;
        Dirty(ent);
        UpdateUi(ent);
    }
}
