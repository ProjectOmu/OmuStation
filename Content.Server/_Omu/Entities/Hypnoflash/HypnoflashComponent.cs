namespace Content.Server._Omu.Entities.Hypnoflash;

[RegisterComponent]
public sealed partial class HypnoflashComponent : Component
{
    [DataField] public EntityUid? HolderUid = null; //who holds the flash
    [DataField] public EntityUid? FlashUid = null; // the flash
}
