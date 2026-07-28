using Robust.Shared.IoC;

namespace Content.Omu.Common.IoC;

internal static class CommonOmuContentIoC
{
    internal static void Register()
    {
        var instance = IoCManager.Instance!;
    }
}
