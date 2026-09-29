using Content.Server.Implants;
using Content.Server.Popups;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Lazarus;

/// <summary>
/// Lazarus Implant, gets implanted, overrides factions then self deletes
/// </summary>
public sealed class LazarusSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private readonly SubdermalImplantSystem _implant = null!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LazarusImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
    }
    private void OnImplantImplanted(Entity<LazarusImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        _popupSystem.PopupEntity(Loc.GetString("lazarus-implant-destruction"), args.Implanted);

        if (HasComp<HumanoidAppearanceComponent>(args.Implanted)) //fail implant - no overriding people :3
        {
            _implant.ForceRemove(args.Implant, ent);    //Delete after use
            return;
        }

        _factions.ClearFactions(args.Implanted);
        _factions.AddFaction(args.Implanted, ent.Comp.Faction);
        _implant.ForceRemove(args.Implant, ent);    //Delete after use
    }
}

[RegisterComponent]
public sealed partial class LazarusImplantComponent : Component
{
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "NanoTrasen";

}
