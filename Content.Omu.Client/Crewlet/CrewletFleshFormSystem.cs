// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Robust.Client.GameObjects;

namespace Content.Omu.Client.Crewlet;

public sealed class CrewletFleshFormSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewletFleshFormComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CrewletFleshFormComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<CrewletFleshFormComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), false);
    }

    private void OnShutdown(Entity<CrewletFleshFormComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent) && TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetVisible((ent, sprite), true);
    }
}
