using Content.Omu.Common.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.Omu.Common.Entry;

// EntryPoint is marked as GameShared for module registration purposes.
public sealed class EntryPoint : GameShared
{
    public override void PreInit()
    {
        IoCManager.InjectDependencies(this);
        CommonOmuContentIoC.Register();
    }
}
