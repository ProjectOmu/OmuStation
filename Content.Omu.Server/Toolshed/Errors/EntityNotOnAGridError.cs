using System.Diagnostics;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Toolshed.Errors;

[Virtual]
public class EntityNotOnAGridError(EntityUid triggeringEnt) : IConError
{
    protected readonly EntityUid TriggeringEnt = triggeringEnt;

    public virtual FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromUnformatted(
            $"The entity {PrettyPrint.PrintUserFacingWithType(TriggeringEnt, out var _)} is not on a grid!");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
