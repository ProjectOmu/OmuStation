using Content.Omu.Client.Eye;
using Content.Omu.Shared.Traits;
using Content.Shared.Flash.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Omu.Client.Traits;

/// <summary>
///     Handles giving the player the photophobia shader / overlay.
/// </summary>
public sealed class PhotophobiaSystem : SharedPhotophobiaSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    private PhotophobiaOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PhotophobiaComponent, ComponentInit>(OnPhotophobiaInit);
        SubscribeLocalEvent<PhotophobiaComponent, ComponentShutdown>(OnPhotophobiaShutdown);

        SubscribeLocalEvent<PhotophobiaComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PhotophobiaComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<FlashImmunityComponent, GotEquippedEvent>(OnSunglassesEquipped);
        SubscribeLocalEvent<FlashImmunityComponent, GotUnequippedEvent>(OnSunglassesUnequipped);

        _overlay = new();
    }

    /// <summary>
    ///     Enables the photophobia shader when the player is added to their body.
    /// </summary>
    private void OnPlayerAttached(Entity<PhotophobiaComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    ///     Disables the photophobia shader when the player is removed from their body.
    /// </summary>
    private void OnPlayerDetached(Entity<PhotophobiaComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlayMan.RemoveOverlay(_overlay);
    }

    /// <summary>
    ///     Enables the photophobia shader when the player is given a photophobia component.
    /// </summary>
    private void OnPhotophobiaInit(Entity<PhotophobiaComponent> ent, ref ComponentInit args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    ///     Disables the photophobia shader when the player's photophobia component is removed.
    /// </summary>
    private void OnPhotophobiaShutdown(Entity<PhotophobiaComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            _overlayMan.RemoveOverlay(_overlay);
    }

    /// <summary>
    ///     Disables the photophobia shader when sunglasses are worn.
    /// </summary>
    private void OnSunglassesEquipped(Entity<FlashImmunityComponent> ent, ref GotEquippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee && args.SlotFlags != SlotFlags.POCKET)
            _overlayMan.RemoveOverlay(_overlay);
    }

    /// <summary>
    ///     Reenables the photophobia shader when sunglasses are unequipped.
    /// </summary>
    private void OnSunglassesUnequipped(Entity<FlashImmunityComponent> ent, ref GotUnequippedEvent args)
    {
        if (_player.LocalEntity == args.Equipee && args.SlotFlags != SlotFlags.POCKET)
            _overlayMan.AddOverlay(_overlay);
    }

}
