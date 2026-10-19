// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Contraband;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Omu.Shared.Entities.Voodoo;

public abstract class SharedVoodooDollChameleonSystem : EntitySystem
{
    [Dependency] private readonly ContrabandSystem _contraband = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] protected readonly SharedUserInterfaceSystem UI = default!;

    private readonly Dictionary<EntProtoId, List<EntProtoId>> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoodooDollChameleonComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnGetVerbs(Entity<VoodooDollChameleonComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("voodoo-user-disguise"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => UI.TryToggleUi(ent.Owner, VoodooDollUiKey.Chameleon, user),
        });
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            _targets.Clear();
    }

    public List<EntProtoId> GetValidTargets(EntProtoId parent)
    {
        if (_targets.TryGetValue(parent, out var targets))
            return targets;

        targets = _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract && !proto.HideSpawnMenu && IsDescendant(proto, parent))
            .Select(proto => new EntProtoId(proto.ID))
            .ToList();
        _targets[parent] = targets;
        return targets;
    }

    private bool IsDescendant(EntityPrototype proto, EntProtoId parent)
    {
        return _proto.EnumerateAllParents<EntityPrototype>(proto.ID).Any(ancestor => ancestor.id == parent.Id);
    }

    protected EntityPrototype? GetDisguise(Entity<VoodooDollChameleonComponent> ent)
    {
        var proto = MetaData(ent).EntityPrototype;
        if (ent.Comp.Selected is { } selected)
            _proto.TryIndex(selected, out proto);

        return proto;
    }

    protected void UpdateVisuals(Entity<VoodooDollChameleonComponent> ent)
    {
        if (GetDisguise(ent) is not { } proto)
            return;

        var meta = MetaData(ent);
        UpdateSprite(ent, proto);
        _metaData.SetEntityName(ent, proto.Name, meta);
        _metaData.SetEntityDescription(ent, proto.Description, meta);

        if (TryComp<ItemComponent>(ent, out var item) && proto.TryGetComponent(out ItemComponent? otherItem, Factory))
            _item.CopyVisuals(ent, otherItem, item);

        if (proto.TryGetComponent(out ContrabandComponent? otherContraband, Factory))
            _contraband.CopyDetails(ent, otherContraband, EnsureComp<ContrabandComponent>(ent));
        else
            RemComp<ContrabandComponent>(ent);
    }

    protected virtual void UpdateSprite(EntityUid uid, EntityPrototype proto) { }
}
