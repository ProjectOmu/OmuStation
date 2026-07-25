namespace Content.Shared._FarHorizons.Vampire;

public abstract partial class VampireConsumption : Component
{
    [DataField(required: true)] public string? Container;
    [DataField] public float Amount = 1;
    [DataField] public TimeSpan Duration = TimeSpan.FromSeconds(1);
}

[RegisterComponent]
public sealed partial class VampireDrinkableComponent : VampireConsumption;

[RegisterComponent]
public sealed partial class VampireBiteableComponent : VampireConsumption;
