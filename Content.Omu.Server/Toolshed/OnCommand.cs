using System.Linq;
using Content.Omu.Server.Toolshed.Errors;
using Robust.Shared.Map;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;

namespace Content.Omu.Server.Toolshed;

[ToolshedCommand]
public sealed class OnCommand : ToolshedCommand
{
    [CommandImplementation("grid")]
    public IEnumerable<EntityUid> OnGrid([PipedArgument] IEnumerable<EntityUid> ents, EntityUid grid , [CommandInverted] bool inverted)
    {
        return FilterGrid(ents, grid, inverted);
    }

    [CommandImplementation("thisgrid")]
    public IEnumerable<EntityUid> OnThisGrid(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> ents, [CommandInverted] bool inverted)
    {
        return Outer_OnThisX(ctx, ents, inverted, (ents, inverted, invoker)
            => (Transform(invoker).GridUid is { } filter_grid)
                ? FilterGrid(ents, filter_grid, inverted)
                : ReportErrorAndBail(ctx, new YouAreNotOnAGridError(invoker))
            );
    }

    [CommandImplementation("gridof")]
    public IEnumerable<EntityUid> OnGridOf(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> ents, EntityUid other_ent, [CommandInverted] bool inverted)
    {
        return (Transform(other_ent).GridUid is { } filter_grid)
            ? FilterGrid(ents, filter_grid, inverted)
            : ReportErrorAndBail(ctx, new EntityNotOnAGridError(other_ent))
            ;
    }

    [CommandImplementation("map")]
    public IEnumerable<EntityUid> OnMap([PipedArgument] IEnumerable<EntityUid> ents, int map, [CommandInverted] bool inverted)
    {
        return FilterMap(ents, new MapId(map), inverted);
    }

    [CommandImplementation("thismap")]
    public IEnumerable<EntityUid> OnThisMap(IInvocationContext ctx, [PipedArgument] IEnumerable<EntityUid> ents, [CommandInverted] bool inverted)
    {
        return Outer_OnThisX(ctx, ents, inverted, (ents, inverted, invoker) => FilterMap(ents, Transform(invoker).MapID, inverted));
    }

    [CommandImplementation("mapof")]
    public IEnumerable<EntityUid> OnMapOf([PipedArgument] IEnumerable<EntityUid> ents, EntityUid other_ent, [CommandInverted] bool inverted)
    {
        return FilterMap(ents, Transform(other_ent).MapID, inverted);
    }

    private IEnumerable<EntityUid> Outer_OnThisX(IInvocationContext ctx, IEnumerable<EntityUid> ents, bool inverted, Func<IEnumerable<EntityUid>, bool, EntityUid, IEnumerable<EntityUid>> inner)
    {
        if (ctx.Session is null) return ReportErrorAndBail(ctx, new NotForServerConsoleError());
        if (ctx.Session.AttachedEntity is { } invoker)
        {
            return inner(ents, inverted, invoker);
        }
        else return ReportErrorAndBail(ctx, new NoAttachedEntityError());
    }

    private IEnumerable<EntityUid> FilterMap(IEnumerable<EntityUid> ents, MapId map, bool inverted)
    {
        return FilterX(ents, e => Transform(e).MapID == map, inverted);
    }

    private IEnumerable<EntityUid> FilterGrid(IEnumerable<EntityUid> ents, EntityUid grid, bool inverted)
    {
        return FilterX(ents, e => Transform(e).GridUid == grid, inverted);
    }

    private IEnumerable<EntityUid> FilterX(IEnumerable<EntityUid> ents, Func<EntityUid, bool> cond, bool inverted)
    {
        return ents.Where(e => cond(e) ^ inverted);
    }

    private IEnumerable<EntityUid> ReportErrorAndBail(IInvocationContext ctx, IConError e)
    {
        ctx.ReportError(e);
        return default!;
    }
}
