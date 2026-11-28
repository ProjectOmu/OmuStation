// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.UserInterface.Systems;
using Content.Omu.Shared.Billiards;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Client.Billiards.UI;

public sealed class BilliardsTableControl : Control
{
    public const float PixelsPerUnit = 240f;

    private const float FullPull = 0.6f;
    private const float Gutter = 0.6f;
    private const float TubeX = 2.40625f;
    private const float TubeTop = 1f - BilliardsSimulation.BallRadius;
    private const float FallAcceleration = 5f;

    private static readonly ProtoId<ShaderPrototype> Shader = "BilliardsBalls";
    private static readonly Vector2 Origin = new(-Gutter);
    private static readonly Vector2 Extent =
        new Vector2(BilliardsSimulation.Length, BilliardsSimulation.Width) + new Vector2(Gutter * 2f);

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly BilliardsTableSystem _billiards;
    private readonly ProgressColorSystem _progressColor;
    private readonly ShaderInstance _stage;
    private readonly ShaderInstance _shader;
    private readonly Vector2[] _drawn = new Vector2[BilliardsSimulation.BallCount];
    private readonly List<int> _returned = [];
    private readonly float[] _returnHeight = new float[BilliardsSimulation.BallCount];
    private readonly float[] _returnSpeed = new float[BilliardsSimulation.BallCount];

    private Vector2 _direction = Vector2.UnitX;
    private float _power;
    private Vector2? _pullStart;
    private bool _canAim;
    private bool _ballInHand;
    private bool _placing;
    private Vector2? _placeAt;
    private bool _aiming;
    private bool _returnFilled;

    public BilliardsTableControl()
    {
        IoCManager.InjectDependencies(this);

        _billiards = _entity.System<BilliardsTableSystem>();
        _progressColor = _entity.System<ProgressColorSystem>();
        _stage = CreateShader(true);
        _shader = CreateShader(false);
        MouseFilter = MouseFilterMode.Stop;
    }

    public event Action<Vector2, float>? OnAim;
    public event Action<Vector2, float>? OnShoot;
    public event Action<Vector2>? OnPlace;

    public EntityUid? Table { get; set; }

    public void SetTurn(bool canAim, bool ballInHand)
    {
        if (!canAim || !ballInHand)
            _placing = false;
        else if (!_ballInHand || !_canAim)
            BeginPlacing();

        if (!canAim)
        {
            _pullStart = null;
            _power = 0f;
        }

        _canAim = canAim;
        _ballInHand = ballInHand;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _stage.Dispose();
        _shader.Dispose();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Table is not { } uid || !_billiards.TryGetBalls(uid, out var balls))
            return;

        for (var ball = BilliardsSimulation.CueBall + 1; ball < balls.Length; ball++)
        {
            var pocketed = balls[ball].X < 0f;
            if (pocketed == _returned.Contains(ball))
                continue;

            if (!pocketed)
            {
                _returned.Remove(ball);
                continue;
            }

            _returned.Add(ball);
            _returnHeight[ball] = _returnFilled ? TubeTop : SlotHeight(_returned.Count - 1);
            _returnSpeed[ball] = 0f;
        }

        _returnFilled = true;

        for (var slot = 0; slot < _returned.Count; slot++)
        {
            var ball = _returned[slot];
            var rest = SlotHeight(slot);
            if (_returnHeight[ball] <= rest)
            {
                _returnHeight[ball] = rest;
                continue;
            }

            _returnSpeed[ball] += FallAcceleration * args.DeltaSeconds;
            _returnHeight[ball] = MathF.Max(rest, _returnHeight[ball] - _returnSpeed[ball] * args.DeltaSeconds);
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Table is not { } uid
            || !_billiards.TryGetBalls(uid, out var balls)
            || !_entity.TryGetComponent(uid, out BilliardsTableComponent? table))
        {
            return;
        }

        var aimed = _billiards.IsAiming((uid, table));
        var direction = table.AimDirection;
        var pull = aimed ? table.AimPower : -1f;
        var power = aimed ? table.AimPower : 0f;
        var guide = 0f;
        var struck = Vector2.Zero;

        if (_canAim && _placing)
        {
            pull = -1f;
            power = 0f;
        }
        else if (_canAim && _aiming)
        {
            direction = _direction;
            pull = _power;
            power = _power;
            guide = Guide(balls, direction, out struck);
        }

        handle.UseShader(_stage);
        handle.DrawTextureRect(Texture.White, PixelSizeBox);
        handle.UseShader(null);

        var scale = PixelSize / Extent;
        var center = PixelSize / 2f + new Vector2(0f, BilliardsTableSystem.Apron / 2f) * scale;
        var half = BilliardsTableSystem.SpriteSize / 2f * scale;
        if (_entity.GetComponentOrNull<SpriteComponent>(uid)?.Icon?.Default is { } texture)
            handle.DrawTextureRect(texture, new UIBox2(center - half, center + half));

        Array.Copy(balls, _drawn, balls.Length);
        if (_canAim && _placing && _placeAt is { } placing)
            _drawn[BilliardsSimulation.CueBall] = placing;

        foreach (var ball in _returned)
        {
            _drawn[ball] = new Vector2(TubeX, _returnHeight[ball]);
        }

        if (!_billiards.TryApplyShader(uid, _shader, _drawn, direction, pull, guide))
            return;

        var meter = _progressColor.GetProgressColor(1f - power);
        _shader.SetParameter("guideNormal", struck);
        _shader.SetParameter("meterPower", power);
        _shader.SetParameter("meterColor", new Vector3(meter.R, meter.G, meter.B));

        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture.White, PixelSizeBox);
        handle.UseShader(null);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!_canAim || Table is not { } uid || !_billiards.TryGetBalls(uid, out var balls))
            return;

        var mouse = ToTable(args.RelativePixelPosition);
        if (_placing)
        {
            var spot = Vector2.Clamp(mouse,
                new Vector2(BilliardsSimulation.BallRadius),
                new Vector2(BilliardsSimulation.Length, BilliardsSimulation.Width)
                    - new Vector2(BilliardsSimulation.BallRadius));

            if (_entity.TryGetComponent(uid, out BilliardsTableComponent? table)
                && BilliardsSimulation.CanPlace(table.Balls, spot))
            {
                _placeAt = spot;
            }

            return;
        }

        if (_pullStart is { } start)
        {
            _power = Math.Clamp(Vector2.Dot(start - mouse, _direction) / FullPull, 0f, 1f);
        }
        else
        {
            var offset = mouse - balls[BilliardsSimulation.CueBall];
            if (offset.LengthSquared() < 0.0001f)
                return;

            _direction = Vector2.Normalize(offset);
        }

        _aiming = true;
        OnAim?.Invoke(_direction, _power);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (!_canAim)
            return;

        if (args.Function == EngineKeyFunctions.UIClick && _placing)
        {
            _placing = false;
            if (_placeAt is { } spot)
                OnPlace?.Invoke(spot);
        }
        else if (args.Function == EngineKeyFunctions.UIClick)
        {
            if (_pullStart == null)
            {
                _pullStart = ToTable(args.RelativePixelPosition);
                _aiming = true;
                OnAim?.Invoke(_direction, _power);
            }
            else if (_power >= SharedBilliardsTableSystem.MinShotPower)
            {
                OnShoot?.Invoke(_direction, _power);
                CancelPull();
            }
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick && _pullStart != null)
        {
            CancelPull();
        }
        else if (args.Function == EngineKeyFunctions.UIRightClick && _ballInHand && !_placing)
        {
            BeginPlacing();
        }
        else
        {
            return;
        }

        args.Handle();
    }

    private static float SlotHeight(int slot)
    {
        return BilliardsSimulation.BallRadius * (1 + slot * 2);
    }

    private static float Guide(Vector2[] balls, Vector2 direction, out Vector2 struck)
    {
        struck = Vector2.Zero;
        const float radius = BilliardsSimulation.BallRadius;
        var cue = balls[BilliardsSimulation.CueBall];

        var length = float.MaxValue;
        if (direction.X != 0f)
        {
            var edge = direction.X > 0f ? BilliardsSimulation.Length - radius : radius;
            length = MathF.Min(length, (edge - cue.X) / direction.X);
        }

        if (direction.Y != 0f)
        {
            var edge = direction.Y > 0f ? BilliardsSimulation.Width - radius : radius;
            length = MathF.Min(length, (edge - cue.Y) / direction.Y);
        }

        for (var ball = BilliardsSimulation.CueBall + 1; ball < balls.Length; ball++)
        {
            var offset = balls[ball] - cue;
            var along = Vector2.Dot(offset, direction);
            var gapSquared = radius * radius * 4f - (offset.LengthSquared() - along * along);
            if (along <= 0f || gapSquared <= 0f)
                continue;

            var reach = along - MathF.Sqrt(gapSquared);
            if (reach >= length)
                continue;

            length = reach;
            struck = Vector2.Normalize(offset - direction * reach);
        }

        return MathF.Max(length, 0f);
    }

    private ShaderInstance CreateShader(bool background)
    {
        var shader = _prototype.Index(Shader).InstanceUnique();
        shader.SetParameter("origin", Origin);
        shader.SetParameter("extent", Extent);
        shader.SetParameter("flipped", 1f);
        shader.SetParameter("stage", 1f);
        shader.SetParameter("background", background ? 1f : 0f);
        return shader;
    }

    private Vector2 ToTable(Vector2 pixelPosition)
    {
        var uv = new Vector2(pixelPosition.X / PixelWidth, 1f - pixelPosition.Y / PixelHeight);
        return Origin + uv * Extent;
    }

    private void BeginPlacing()
    {
        _placing = true;
        _placeAt = null;
        _pullStart = null;
        _power = 0f;
    }

    private void CancelPull()
    {
        _pullStart = null;
        _power = 0f;
        OnAim?.Invoke(_direction, _power);
    }
}
