using Content.Omu.Client.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.Omu.Client.Entry;

public sealed class EntryPoint : GameClient
{
    public override void PreInit()
    {
        base.PreInit();
    }

    public override void Init()
    {
        ContentOmuClientIoC.Register();

        IoCManager.BuildGraph();
        IoCManager.InjectDependencies(this);
    }
}
