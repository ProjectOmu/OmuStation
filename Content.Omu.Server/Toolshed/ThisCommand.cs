using Robust.Shared.Map;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;

namespace Content.Omu.Server.Toolshed;

[ToolshedCommand]
public sealed class ThisCommand : ToolshedCommand
{
    [CommandImplementation("grid")]
    public EntityUid? ThisGrid(IInvocationContext ctx)
    {
        return CheckCommonPitfalls(ctx, null, invoker => Transform(invoker).GridUid);
    }

    [CommandImplementation("map")]
    public int ThisMap(IInvocationContext ctx)
    {
        return CheckCommonPitfalls(ctx, (int) MapId.Nullspace, invoker => (int) Transform(invoker).MapID);
    }

    private T CheckCommonPitfalls<T>(IInvocationContext ctx, T fail_value, Func<EntityUid, T> and_then_do_this)
    {
        if (ctx.Session is null)
        {
            ctx.ReportError(new NotForServerConsoleError());
            return fail_value;
        }

        if (ctx.Session.AttachedEntity is { } invoker)
        {
            return and_then_do_this(invoker);
        }
        else
        {
            return fail_value;
        }
    }
}
