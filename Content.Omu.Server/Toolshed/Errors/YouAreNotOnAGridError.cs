using Robust.Shared.Utility;

namespace Content.Omu.Server.Toolshed.Errors;

public sealed class YouAreNotOnAGridError(EntityUid you) : EntityNotOnAGridError(you)
{
    public override FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromUnformatted($"You ({PrettyPrint.PrintUserFacingWithType(TriggeringEnt, out var _)}) are not on a grid!!");
    }
}
