using Content.Omu.Shared.Voidwalker.TemporarilyDisableCollision;
using Content.Shared.Movement.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.Voidwalker;

public sealed class SharedVoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidwalkerComponent, PreventCollideEvent>(OnPreventCollide);

        SubscribeLocalEvent<VoidwalkerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMoveSpeed);
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerSpacedStatusChangedEvent>(OnSpacedStatusChanged);

        SubscribeLocalEvent<VoidwalkerComponent, ShotAttemptedEvent>(OnShotAttempted); // someone should rly genericize these lol
    }

    private void OnPreventCollide(Entity<VoidwalkerComponent> entity, ref PreventCollideEvent args)
    {
        if (!_tag.HasAnyTag(args.OtherEntity, entity.Comp.PassableTags))
            return;

        args.Cancelled = true;
        EnsureComp<TemporarilyDisableCollisionComponent>(args.OtherEntity);

        entity.Comp.EntitiesPassed[args.OtherEntity] = _timing.CurTime + entity.Comp.EntityPassedAddedComponentsDuration;
        EntityManager.AddComponents(args.OtherEntity, entity.Comp.ComponentsAddedOnPass);
    }

    private void OnSpacedStatusChanged(Entity<VoidwalkerComponent> entity, ref VoidwalkerSpacedStatusChangedEvent args)
    {
        if (args.Spaced && entity.Comp.UnsettleDoAfterId == null) // if spaced and not currently performing unsettle, go invis
        {
            EnsureComp<StealthComponent>(entity); // Okay, this is a weird way to do this, but stealth literally doesn't work if you enable/disable it so IDK :shrug:
           _stealth.SetThermalsImmune(entity, args.Spaced); // tell me if you find a better way to fix this - delph
        }
        else
            RemComp<StealthComponent>(entity);

        _movement.RefreshMovementSpeedModifiers(entity);
        Dirty(entity);
    }

    private void OnRefreshMoveSpeed(Entity<VoidwalkerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = ent.Comp.IsInSpace ? 1f : ent.Comp.NonSpacedSpeedModifier;
        args.ModifySpeed(modifier, modifier);
        Dirty(ent);
    }

    private static void OnShotAttempted(Entity<VoidwalkerComponent> ent, ref ShotAttemptedEvent args) // no fun allowed or something
    {
        args.Cancel();
    }

}
