namespace Content.Omu.Server.Entities.Hypnoflash;

[RegisterComponent]
public sealed partial class HypnoflashComponent : Component
{
    [DataField] public EntityUid? FlashUid = null; // the flash
}
