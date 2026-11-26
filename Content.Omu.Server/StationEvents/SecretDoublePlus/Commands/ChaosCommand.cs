using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Console;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;

public abstract class ChaosCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    protected bool TryParseChaos(IConsoleShell shell, string[] args, [NotNullWhen(true)] out float? result)
    {
        result = null;
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return false;
        }

        if (!float.TryParse(args[0], out var chaos))
        {
            shell.WriteError(Loc.GetString("shell-unparsable-number"));
            return false;
        }

        result = chaos;
        return true;
    }

    protected bool TryGetComponent(IConsoleShell shell, [NotNullWhen(true)] out SecretDoublePlusComponent? component)
    {
        component = null;
        var query = _entities.EntityQueryEnumerator<SecretDoublePlusComponent>();
        if (!query.MoveNext(out var found))
        {
            shell.WriteError(Loc.GetString("secret-double-plus-component-not-found"));
            return false;
        }

        component = found;
        return true;
    }
}
