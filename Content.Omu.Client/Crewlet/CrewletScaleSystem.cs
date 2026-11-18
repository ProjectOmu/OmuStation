// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
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
            if (sprite.Scale != crewlet.Scale)
                _sprite.SetScale((uid, sprite), crewlet.Scale);
        }
    }
}
