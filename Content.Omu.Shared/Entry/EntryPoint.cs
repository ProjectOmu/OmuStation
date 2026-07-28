using System;
using Content.Omu.Shared.IoC;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Entry;

public sealed class EntryPoint : GameShared
{
    public override void PreInit()
    {
        IoCManager.InjectDependencies(this);
        SharedOmuContentIoC.Register();
    }
}
