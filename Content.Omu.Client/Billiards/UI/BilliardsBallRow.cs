// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Omu.Shared.Billiards;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Billiards.UI;

public sealed class BilliardsBallRow : Control
{
    private const float Radius = BilliardsSimulation.BallRadius;

    private static readonly ProtoId<ShaderPrototype> Shader = "BilliardsBalls";
    private static readonly Vector2 Hidden = new(-10f, -10f);

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;
    private readonly Vector2[] _balls = new Vector2[BilliardsSimulation.BallCount];
    private readonly Vector2[] _spinAxis = new Vector2[BilliardsSimulation.BallCount];
    private readonly Vector2[] _spinTurn = new Vector2[BilliardsSimulation.BallCount];
    private readonly List<int> _shown = [];

    public BilliardsBallRow()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototype.Index(Shader).InstanceUnique();
        _shader.SetParameter("origin", Vector2.Zero);
        _shader.SetParameter("flipped", 1f);
        _shader.SetParameter("cuePull", -1f);
        _shader.SetParameter("guideLength", 0f);
        _shader.SetParameter("meterPower", -1f);
        _shader.SetParameter("stage", 0f);
        _shader.SetParameter("background", 0f);
        Array.Fill(_spinTurn, Vector2.UnitY);
        _shader.SetParameter("spinAxis", _spinAxis);
        _shader.SetParameter("spinTurn", _spinTurn);
        MinHeight = Radius * 2f * BilliardsTableControl.PixelsPerUnit;
    }

    public void SetBalls(int balls)
    {
        _shown.Clear();
        for (var ball = BilliardsSimulation.CueBall + 1; ball < BilliardsSimulation.BallCount; ball++)
        {
            if ((balls & (1 << ball)) != 0)
                _shown.Add(ball);
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _shader.Dispose();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (_shown.Count == 0)
            return;

        var extent = Size / BilliardsTableControl.PixelsPerUnit;
        var step = _shown.Count > 1 ? MathF.Min(Radius * 2f, (extent.X - Radius * 2f) / (_shown.Count - 1)) : 0f;

        Array.Fill(_balls, Hidden);
        for (var slot = 0; slot < _shown.Count; slot++)
        {
            _balls[_shown[slot]] = new Vector2(Radius + slot * step, extent.Y / 2f);
        }

        _shader.SetParameter("extent", extent);
        _shader.SetParameter("balls", _balls);
        handle.UseShader(_shader);
        handle.DrawTextureRect(Texture.White, PixelSizeBox);
        handle.UseShader(null);
    }
}
