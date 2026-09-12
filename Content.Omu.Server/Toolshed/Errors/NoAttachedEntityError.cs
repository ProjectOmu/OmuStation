using System.Diagnostics;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Toolshed.Errors;

public sealed class NoAttachedEntityError : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromUnformatted(
            "You must be attached to an entity to use this.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
