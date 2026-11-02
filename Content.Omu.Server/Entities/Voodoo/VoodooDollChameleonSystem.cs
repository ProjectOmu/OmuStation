// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Entities.Voodoo;
using Content.Shared.Sound.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Omu.Server.Entities.Voodoo;

public sealed class VoodooDollChameleonSystem : SharedVoodooDollChameleonSystem
{
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoodooDollChameleonComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VoodooDollChameleonComponent, VoodooDollChameleonSelectedMessage>(OnSelected);
    }

    private void OnMapInit(Entity<VoodooDollChameleonComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Selected != null)
        {
            UpdateVisuals(ent);
            UpdateSounds(ent);
        }

        UpdateUi(ent);
    }

    private void OnSelected(Entity<VoodooDollChameleonComponent> ent, ref VoodooDollChameleonSelectedMessage args)
    {
        if (args.Selected.Id == MetaData(ent).EntityPrototype?.ID)
            ent.Comp.Selected = null;
        else if (GetValidTargets(ent.Comp.Parent).Contains(args.Selected))
            ent.Comp.Selected = args.Selected;
        else
            return;

        UpdateVisuals(ent);
        UpdateSounds(ent);
        UpdateUi(ent);
        Dirty(ent);
    }

    private void UpdateSounds(Entity<VoodooDollChameleonComponent> ent)
    {
        if (GetDisguise(ent) is not { } proto)
            return;

        CopySound<EmitSoundOnCollideComponent>(ent, proto);
        CopySound<EmitSoundOnLandComponent>(ent, proto);
        CopySound<EmitSoundOnThrowComponent>(ent, proto);
        CopySound<EmitSoundOnPickupComponent>(ent, proto);
        CopySound<EmitSoundOnDropComponent>(ent, proto);
    }

    private void CopySound<T>(EntityUid uid, EntityPrototype proto) where T : Component, new()
    {
        RemComp<T>(uid);
        if (!proto.TryGetComponent(out T? source, Factory))
            return;

        var sound = AddComp<T>(uid);
        _serialization.CopyTo(source, ref sound, notNullableOverride: true);
        Dirty(uid, sound);
    }

    private void UpdateUi(Entity<VoodooDollChameleonComponent> ent)
    {
        UI.SetUiState(ent.Owner, VoodooDollUiKey.Chameleon,
            new VoodooDollChameleonBoundUserInterfaceState(ent.Comp.Parent, ent.Comp.Selected));
    }
}
