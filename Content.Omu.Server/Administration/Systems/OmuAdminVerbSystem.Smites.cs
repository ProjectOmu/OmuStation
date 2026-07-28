using Content.Omu.Common.CCVar;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Info;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Administration.Systems;

public sealed partial class OmuAdminVerbSystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private void AddSmiteVerbs(GetVerbsEvent<Verb> args)
    {
        if (!SmitesAllowed(args))
            return;

        // If someone has the FUN flag they probably have the ADMIN flag
        if (_player.TryGetSessionByEntity(args.Target, out var session))
        {
            var rulesName = Loc.GetString("admin-smite-rules-name").ToLowerInvariant();
            Verb rules = new()
            {
                Text = rulesName,
                Category = VerbCategory.Smite,
                Icon = new SpriteSpecifier.Rsi(new ("Mobs/Species/Human/organs.rsi"), "brain"),
                Act = () =>
                {
                    var message = new SendRulesInformationMessage
                    {
                        PopupTime = _cfg.GetCVar(OmuCVars.RulesSmiteTime),
                        CoreRules = _cfg.GetCVar(CCVars.RulesFile),
                        ShouldShowRules = true,
                        AllowBypass = false
                    };
                    _net.ServerSendMessage(message, session.Channel);
                },
                Impact = LogImpact.Extreme,
                Message = string.Join(": ", rulesName, Loc.GetString("admin-smite-rules-description"))
            };
            args.Verbs.Add(rules);
        }
    }

    private bool SmitesAllowed(GetVerbsEvent<Verb> args)
    {
        if (!TryComp(args.User, out ActorComponent? actor) ||
            !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Fun) ||
            HasComp<MapComponent>(args.Target) || HasComp<MapGridComponent>(args.Target))
            return false;

        return true;
    }
}
