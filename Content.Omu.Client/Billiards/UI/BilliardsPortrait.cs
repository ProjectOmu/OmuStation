// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Billiards.UI;

public sealed class BilliardsPortrait : Control
{
    private const float FrameSize = 44f;
    private const float CompactFrameSize = 22f;
    private const float Zoom = 2.5f;
    private const float HeadLift = 8f;

    [Dependency] private readonly IEntityManager _entity = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;

    private NetEntity _subject;
    private float _zoom = Zoom;

    public BilliardsPortrait()
    {
        IoCManager.InjectDependencies(this);

        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<TransformSystem>();
        SetSize = new Vector2(FrameSize);
        RectClipContent = true;
    }

    public void SetEntity(NetEntity subject, bool compact)
    {
        _subject = subject;
        var size = compact ? CompactFrameSize : FrameSize;
        SetSize = new Vector2(size);
        _zoom = Zoom * size / FrameSize;
    }

    protected override void Draw(IRenderHandle handle)
    {
        base.Draw(handle);

        if (!_entity.TryGetEntity(_subject, out var uid)
            || !_entity.TryGetComponent(uid, out SpriteComponent? sprite)
            || !_entity.TryGetComponent(uid, out TransformComponent? xform))
        {
            return;
        }

        _sprite.ForceUpdate(uid.Value);

        var scale = _zoom * UIScale;
        var position = PixelSize / 2f + new Vector2(0f, HeadLift * scale);
        handle.DrawEntity(uid.Value,
            position,
            new Vector2(scale),
            Angle.Zero,
            Angle.Zero,
            Direction.South,
            sprite,
            xform,
            _transform);
    }
}
