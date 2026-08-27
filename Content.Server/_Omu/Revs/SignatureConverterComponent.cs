namespace Content.Server._Omu.Revs;

[RegisterComponent, Access(typeof(SignatureConverterSystem))]
public sealed partial class SignatureConverterComponent : Component
{
    [DataField]
    public float Amount = 20f;
}
