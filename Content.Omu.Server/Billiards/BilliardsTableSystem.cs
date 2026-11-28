// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Omu.Shared.Billiards;

namespace Content.Omu.Server.Billiards;

public sealed class BilliardsTableSystem : SharedBilliardsTableSystem
{
    private const int EightBall = 8;
    private const int Solids = 0b0000_0000_1111_1110;
    private const int Stripes = 0b1111_1110_0000_0000;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<BilliardsTableComponent>();
        while (query.MoveNext(out var uid, out var table))
        {
            if (table.ShotEnd is { } end && Timing.CurTime >= end)
                ResolveShot((uid, table));
        }
    }

    protected override float ShotSeconds(BilliardsTableComponent table)
    {
        var simulation = new BilliardsSimulation(table.Balls, table.ShotVelocity);
        simulation.Finish();
        return simulation.Elapsed;
    }

    private static int PocketedMask(BilliardsTableComponent table)
    {
        var mask = 0;
        for (var ball = BilliardsSimulation.CueBall + 1; ball < table.Balls.Count; ball++)
        {
            if (table.Balls[ball].Pocketed)
                mask |= 1 << ball;
        }

        return mask;
    }

    private static int GroupMask(BilliardsTableComponent table, int player)
    {
        if (table.SolidsPlayer < 0)
            return 0;

        return player == table.SolidsPlayer ? Solids : Stripes;
    }

    private static void CountGroups(BilliardsTableComponent table)
    {
        var pocketed = PocketedMask(table);
        for (var index = 0; index < table.Players.Count; index++)
        {
            var down = BitOperations.PopCount((uint) (GroupMask(table, index) & pocketed));
            table.Players[index] = table.Players[index] with { Score = down };
        }
    }

    private static int Leader(BilliardsTableComponent table)
    {
        var leader = -1;
        var tied = false;

        for (var index = 0; index < table.Players.Count; index++)
        {
            if (leader < 0 || table.Players[index].Score > table.Players[leader].Score)
            {
                leader = index;
                tied = false;
            }
            else if (table.Players[index].Score == table.Players[leader].Score)
            {
                tied = true;
            }
        }

        return tied ? -1 : leader;
    }

    private void ResolveShot(Entity<BilliardsTableComponent> ent)
    {
        var table = ent.Comp;
        var simulation = new BilliardsSimulation(table.Balls, table.ShotVelocity);
        simulation.Finish();

        var sunk = simulation.PocketOrder.Where(ball => ball != BilliardsSimulation.CueBall).ToList();

        for (var ball = 0; ball < BilliardsSimulation.BallCount; ball++)
        {
            table.Balls[ball] = new BilliardsBall(simulation.Positions[ball], simulation.Pocketed[ball]);
        }

        var scratched = simulation.Pocketed[BilliardsSimulation.CueBall];
        var breaking = table.AwaitingBreak;
        table.AwaitingBreak = false;
        if (scratched)
        {
            var spot = BilliardsSimulation.FreeSpot(table.Balls, BilliardsSimulation.HeadSpot);
            table.Balls[BilliardsSimulation.CueBall] = new BilliardsBall(spot, false);
            table.BallInHand = true;
        }

        table.ShotStart = null;
        table.ShotEnd = null;
        table.LastActivity = Timing.CurTime;

        if (table.Mode == BilliardsMode.Freeplay || table.Turn >= table.Players.Count)
        {
            if (scratched)
                Popup.PopupEntity(Loc.GetString("billiards-popup-scratch-anyone"), ent);
        }
        else
        {
            var shooter = table.Players[table.Turn].Name;
            if (scratched && table.Mode != BilliardsMode.FewestShots)
                Popup.PopupEntity(Loc.GetString("billiards-popup-scratch", ("player", shooter)), ent);
            else if (sunk.Count > 0)
                Popup.PopupEntity(Loc.GetString("billiards-popup-sunk", ("player", shooter), ("count", sunk.Count)),
                    ent);

            switch (table.Mode)
            {
                case BilliardsMode.Casual:
                    ScoreCasual(ent, sunk.Count);
                    break;
                case BilliardsMode.SolidsStripes:
                    ScoreSolidsStripes(ent, sunk, scratched, breaking);
                    break;
                case BilliardsMode.Killer:
                    ScoreKiller(ent, sunk.Count, scratched, breaking);
                    break;
                case BilliardsMode.FewestShots:
                    ScoreFewestShots(ent, scratched);
                    break;
            }
        }

        Dirty(ent);
    }

    private void ScoreCasual(Entity<BilliardsTableComponent> ent, int sunk)
    {
        var table = ent.Comp;
        var shooter = table.Players[table.Turn];
        table.Players[table.Turn] = shooter with { Score = shooter.Score + sunk };

        if (ObjectBallsLeft(table) == 0)
        {
            Finish(ent, Leader(table));
            return;
        }

        AdvanceTurn(table);
    }

    private void ScoreSolidsStripes(Entity<BilliardsTableComponent> ent, List<int> sunk, bool scratched, bool breaking)
    {
        var table = ent.Comp;
        if (breaking && sunk.Remove(EightBall))
        {
            var spot = BilliardsSimulation.FreeSpot(table.Balls, BilliardsSimulation.FootSpot);
            table.Balls[EightBall] = new BilliardsBall(spot, false);
            Popup.PopupEntity(Loc.GetString("billiards-popup-eight-spotted"), ent);
        }

        var sunkMask = sunk.Aggregate(0, (mask, ball) => mask | (1 << ball));
        var shooter = table.Turn;
        var opponent = table.Players.Count == 2 ? 1 - shooter : -1;

        if (sunk.Contains(EightBall))
        {
            var group = GroupMask(table, shooter);
            var pocketedBefore = PocketedMask(table) & ~sunkMask;
            var cleared = group != 0 && (group & ~pocketedBefore) == 0;
            if (!cleared)
            {
                var name = table.Players[shooter].Name;
                Popup.PopupEntity(Loc.GetString("billiards-popup-early-eight", ("player", name)), ent);
            }

            CountGroups(table);
            Finish(ent, cleared && !scratched ? shooter : opponent);
            return;
        }

        if (table.SolidsPlayer < 0 && !scratched && sunk.Count > 0)
        {
            var solids = sunk[0] < EightBall;
            table.SolidsPlayer = solids ? shooter : 1 - shooter;
            var key = solids ? "billiards-popup-solids" : "billiards-popup-stripes";
            Popup.PopupEntity(Loc.GetString(key, ("player", table.Players[shooter].Name)), ent);
        }

        CountGroups(table);

        if (scratched || (sunkMask & GroupMask(table, shooter)) == 0)
            AdvanceTurn(table);
    }

    private void ScoreKiller(Entity<BilliardsTableComponent> ent, int sunk, bool scratched, bool breaking)
    {
        var table = ent.Comp;
        var shooter = table.Players[table.Turn];
        var lives = shooter.Score;

        if (scratched || (sunk == 0 && !breaking))
        {
            lives = Math.Max(0, lives - 1);
            var key = lives == 0 ? "billiards-popup-out" : "billiards-popup-life-lost";
            Popup.PopupEntity(Loc.GetString(key, ("player", shooter.Name)), ent);
        }

        table.Players[table.Turn] = shooter with { Score = lives };

        if (ObjectBallsLeft(table) == 0)
        {
            table.Balls = BilliardsSimulation.Rack();
            table.AwaitingBreak = true;
            table.BallInHand = false;
            Popup.PopupEntity(Loc.GetString("billiards-popup-rerack"), ent);
        }

        if (!TryFinishKiller(ent))
            AdvanceTurn(table);
    }

    private void ScoreFewestShots(Entity<BilliardsTableComponent> ent, bool scratched)
    {
        var table = ent.Comp;
        var shooter = table.Players[table.Turn];
        table.Players[table.Turn] = shooter with { Score = shooter.Score + 1 };

        if (scratched)
        {
            table.BallInHand = false;
            Finish(ent, -1);
            return;
        }

        if (ObjectBallsLeft(table) == 0)
            Finish(ent, table.Turn);
    }
}
