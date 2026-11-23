// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Content.Shared.Sprite;
using Robust.Client.GameObjects;

namespace Content.Omu.Client.Crewlet;

public sealed class CrewletScaleSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<CrewletComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var crewlet, out var sprite))
        {
            var scale = TryComp<ScaleVisualsComponent>(uid, out var visuals) ? visuals.Scale : crewlet.Scale;
            if (sprite.Scale != scale)
                _sprite.SetScale((uid, sprite), scale);
        }
    }
}
