using Content.Omu.Server.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.Omu.Server.Entry;

public sealed class EntryPoint : GameServer
{
    public override void Init()
    {
        base.Init();

        ServerOmuContentIoC.Register();

        IoCManager.BuildGraph();
    }
}
