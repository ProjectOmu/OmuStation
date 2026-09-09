using Content.Shared.Module;

namespace Content.Omu.Shared.Module;

public sealed class OmuModPack : ModulePack
{
    public override string PackName => "Omu";

    public override IReadOnlySet<RequiredAssembly> RequiredAssemblies { get; } = new HashSet<RequiredAssembly>
    {
        RequiredAssembly.Client("Content.Omu.Client"),
        RequiredAssembly.Server("Content.Omu.Server"),
        RequiredAssembly.Shared("Content.Omu.Common"),
    };
}
