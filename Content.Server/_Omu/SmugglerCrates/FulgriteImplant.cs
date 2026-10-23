using Content.Server.Implants;
using Content.Server.Popups;
using Content.Shared.Implants;
using Content.Shared.NPC.Systems;
using Content.Shared.Power.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Omu.Lazarus;

/// <summary>
/// Lazarus Implant, gets implanted, overrides factions then self deletes
/// </summary>
public sealed class FulgriteSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FulgriteImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
    }
    private void OnImplantImplanted(Entity<FulgriteImplantComponent> ent, ref ImplantImplantedEvent args)   //Doing this in C# since i want to unhardcode it and have it be changeable in yaml. Also its a challenge and i want to try
    {
        foreach (var (componentName, entry) in ent.Comp.ComponentsToAdd)        //Loop for all components
        {
            if (_componentFactory.TryGetRegistration(componentName, out var reg))   //Does component exist
            {
                if (EntityManager.HasComponent(args.Implanted, reg))    //If yes, we need to override/update fields
                {
                    var existing = EntityManager.GetComponent(args.Implanted, reg.Type);
                    var node = _serialization.WriteValue(reg.Type, existing);
                    foreach (var field in entry.Mapping)
                    {


                    }
                }

                var component = _componentFactory.GetComponent(componentName);
                EntityManager.AddComponent(args.Implanted, component);
            }
        }
    }

    private void ChangeDataField(Entity<FulgriteImplantComponent> ent, ref ImplantImplantedEvent args)
    {


    }
}

[RegisterComponent]
public sealed partial class FulgriteImplantComponent : Component
{
    [DataField]
    public ComponentRegistry ComponentsToAdd = new();

    [DataField]
    public string ActionGranted = "FulgriteZap";
}
