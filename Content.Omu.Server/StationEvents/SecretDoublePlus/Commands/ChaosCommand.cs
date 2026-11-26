using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;

public abstract class ChaosCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IEntityManager _entities = default!;

    protected bool TryParseChaos(IConsoleShell shell, string[] args, [NotNullWhen(true)] out float? result)
    {
        result = null;
        if (args.Length != 1)
        {
            shell.WriteError("Incorrect number of arguments, pass one value");
            return false;
        }

        if (!float.TryParse(args[0], out var chaos))
        {
            shell.WriteError("Unparseable chaos value");
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
            shell.WriteError("SecretDoublePlus component not found");
            return false;
        }

        component = found;
        return true;
    }

    protected void LogMessage(ICommonSession? player, string message)
    {
        var playerString = player != null ? $"{player:Player}" : "Server";
        message = $"{playerString} {message}";

        _adminLogger.Add(LogType.SecretDoublePlus, LogImpact.Medium, $"{message}");
        _chat.SendAdminAnnouncement("SecretDoublePlus " + message);
    }
}
