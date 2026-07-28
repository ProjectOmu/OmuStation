using Robust.Shared.IoC;

namespace Content.Omu.Shared.IoC;

internal static class SharedOmuContentIoC
{
    internal static void Register()
    {
        var instance = IoCManager.Instance!;
    }
}
