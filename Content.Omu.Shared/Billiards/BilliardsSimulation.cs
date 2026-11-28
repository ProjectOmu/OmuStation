// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Content.Omu.Shared.Billiards;

public sealed class BilliardsSimulation
{
    public const int BallCount = 16;
    public const int CueBall = 0;
    public const float Length = 2f;
    public const float Width = 1f;
    public const float BallRadius = 0.03125f;
    public const float MaxSpeed = 5f;
    public const float MaxSeconds = MaxSteps * StepSeconds;

    public static readonly Vector2 HeadSpot = new(0.5f, 0.5f);
    public static readonly Vector2 FootSpot = new(1.5f, 0.5f);

    private const float CornerPocketRadius = 0.07f;
    private const float SidePocketRadius = 0.05f;
    private const float Reach = BallRadius * 2f;
    private const float StepSeconds = 1f / 240f;
    private const int MaxSteps = 4800;
    private const float RollingDeceleration = 0.12f;
    private const float Drag = 0.3f;
    private const float StopSpeed = 0.01f;
    private const float CushionBounce = 0.75f;
    private const float BallBounce = 0.96f;
    private const float RackSpacing = BallRadius * 2f + 0.001f;
    private const float RackRowSpacing = RackSpacing * 0.8660254f;
    private const int RackRows = 5;

    private static readonly (Vector2 Position, float Radius)[] Pockets =
    [
        (new Vector2(0f, 0f), CornerPocketRadius),
        (new Vector2(Length / 2f, 0f), SidePocketRadius),
        (new Vector2(Length, 0f), CornerPocketRadius),
        (new Vector2(0f, Width), CornerPocketRadius),
        (new Vector2(Length / 2f, Width), SidePocketRadius),
        (new Vector2(Length, Width), CornerPocketRadius),
    ];

    private static readonly int[] RackOrder = [1, 10, 3, 11, 8, 5, 2, 13, 14, 6, 12, 7, 15, 4, 9];

    public readonly Vector2[] Positions = new Vector2[BallCount];
    public readonly bool[] Pocketed = new bool[BallCount];
    public readonly List<int> PocketOrder = [];

    private readonly Vector2[] _velocities = new Vector2[BallCount];
    private int _steps;
    private bool _moving;

    public BilliardsSimulation(IReadOnlyList<BilliardsBall> balls, Vector2 cueVelocity)
    {
        for (var ball = 0; ball < BallCount; ball++)
        {
            Positions[ball] = balls[ball].Position;
            Pocketed[ball] = balls[ball].Pocketed;
        }

        _velocities[CueBall] = cueVelocity;
        _moving = cueVelocity != Vector2.Zero && !Pocketed[CueBall];
    }

    public int Contacts { get; private set; }

    public float Elapsed => _steps * StepSeconds;

    public static List<BilliardsBall> Rack()
    {
        var balls = new BilliardsBall[BallCount];
        balls[CueBall] = new BilliardsBall(HeadSpot, false);

        var slot = 0;
        for (var row = 0; row < RackRows; row++)
        {
            for (var column = 0; column <= row; column++)
            {
                var offset = new Vector2(row * RackRowSpacing, (column - row / 2f) * RackSpacing);
                balls[RackOrder[slot++]] = new BilliardsBall(FootSpot + offset, false);
            }
        }

        return [.. balls];
    }

    public static bool CanPlace(IReadOnlyList<BilliardsBall> balls, Vector2 spot)
    {
        if (spot.X < BallRadius || spot.X > Length - BallRadius || spot.Y < BallRadius || spot.Y > Width - BallRadius)
            return false;

        foreach (var (pocket, radius) in Pockets)
        {
            if (Vector2.DistanceSquared(spot, pocket) < radius * radius)
                return false;
        }

        for (var ball = CueBall + 1; ball < balls.Count; ball++)
        {
            if (!balls[ball].Pocketed && Vector2.DistanceSquared(balls[ball].Position, spot) < Reach * Reach)
                return false;
        }

        return true;
    }

    public static Vector2 FreeSpot(IReadOnlyList<BilliardsBall> balls, Vector2 spot)
    {
        var limit = (int) ((Width / 2f - BallRadius) / RackSpacing);

        for (var step = 0; step <= limit; step++)
        {
            var above = spot + new Vector2(0f, step * RackSpacing);
            if (IsFree(balls, above))
                return above;

            var below = spot - new Vector2(0f, step * RackSpacing);
            if (IsFree(balls, below))
                return below;
        }

        return spot;
    }

    public void AdvanceTo(float seconds)
    {
        while (_moving && Elapsed + StepSeconds <= seconds)
        {
            Step();
        }
    }

    public void Finish()
    {
        while (_moving)
        {
            Step();
        }
    }

    private static bool IsFree(IReadOnlyList<BilliardsBall> balls, Vector2 spot)
    {
        foreach (var ball in balls)
        {
            if (!ball.Pocketed && Vector2.DistanceSquared(ball.Position, spot) < RackSpacing * RackSpacing)
                return false;
        }

        return true;
    }

    private void Step()
    {
        _steps++;

        for (var ball = 0; ball < BallCount; ball++)
        {
            if (!Pocketed[ball])
                Roll(ball);
        }

        for (var first = 0; first < BallCount; first++)
        {
            if (Pocketed[first])
                continue;

            for (var second = first + 1; second < BallCount; second++)
            {
                if (!Pocketed[second])
                    Collide(first, second);
            }
        }

        _moving = false;
        for (var ball = 0; ball < BallCount; ball++)
        {
            if (_steps >= MaxSteps)
                _velocities[ball] = Vector2.Zero;

            _moving |= _velocities[ball] != Vector2.Zero;
        }
    }

    private void Roll(int ball)
    {
        var velocity = _velocities[ball];
        var speed = velocity.Length();
        if (speed == 0f)
            return;

        var position = Positions[ball] + velocity * StepSeconds;
        var slowed = speed - (RollingDeceleration + Drag * speed) * StepSeconds;
        velocity = slowed < StopSpeed ? Vector2.Zero : velocity * (slowed / speed);

        foreach (var (pocket, radius) in Pockets)
        {
            if (Vector2.DistanceSquared(position, pocket) >= radius * radius)
                continue;

            Positions[ball] = pocket;
            Pocketed[ball] = true;
            PocketOrder.Add(ball);
            _velocities[ball] = Vector2.Zero;
            Contacts++;
            return;
        }

        if ((position.X < BallRadius && velocity.X < 0f) || (position.X > Length - BallRadius && velocity.X > 0f))
        {
            velocity.X *= -CushionBounce;
            Contacts++;
        }

        if ((position.Y < BallRadius && velocity.Y < 0f) || (position.Y > Width - BallRadius && velocity.Y > 0f))
        {
            velocity.Y *= -CushionBounce;
            Contacts++;
        }

        position.X = Math.Clamp(position.X, BallRadius, Length - BallRadius);
        position.Y = Math.Clamp(position.Y, BallRadius, Width - BallRadius);

        Positions[ball] = position;
        _velocities[ball] = velocity;
    }

    private static float ImpactRewind(Vector2 delta, Vector2 approach)
    {
        var speedSquared = approach.LengthSquared();
        var closing = Vector2.Dot(delta, approach);
        if (speedSquared == 0f || closing >= 0f)
            return 0f;

        var root = MathF.Sqrt(closing * closing - speedSquared * (delta.LengthSquared() - Reach * Reach));
        return MathF.Min((closing + root) / speedSquared, StepSeconds);
    }

    private void Collide(int first, int second)
    {
        var delta = Positions[second] - Positions[first];
        if (delta.LengthSquared() >= Reach * Reach)
            return;

        var approach = _velocities[second] - _velocities[first];
        var rewind = ImpactRewind(delta, approach);
        Positions[first] -= _velocities[first] * rewind;
        Positions[second] -= _velocities[second] * rewind;

        delta = Positions[second] - Positions[first];
        var distance = delta.Length();
        var normal = distance > 0f ? delta / distance : Vector2.UnitX;
        var overlap = MathF.Max(Reach - distance, 0f);
        Positions[first] -= normal * (overlap / 2f);
        Positions[second] += normal * (overlap / 2f);

        var closing = -Vector2.Dot(approach, normal);
        if (closing > 0f)
        {
            var impulse = normal * (closing * (1f + BallBounce) / 2f);
            _velocities[first] -= impulse;
            _velocities[second] += impulse;
            Contacts++;
        }

        Positions[first] += _velocities[first] * rewind;
        Positions[second] += _velocities[second] * rewind;
    }
}
