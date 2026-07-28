// SPDX-FileCopyrightText: 2021 ShadowCommander <10494922+ShadowCommander@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Vera Aguilera Puerto <gradientvera@outlook.com>
// SPDX-FileCopyrightText: 2022 mirrorcult <lunarautomaton6@gmail.com>
// SPDX-FileCopyrightText: 2022 wrexbe <81056464+wrexbe@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 0x6273 <0x40@keemail.me>
// SPDX-FileCopyrightText: 2024 AJCM-git <60196617+AJCM-git@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 LordCarve <27449516+LordCarve@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Info;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server.Info;

[AdminCommand(AdminFlags.Admin)]
public sealed class ShowRulesCommand : LocalizedCommands
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override string Command => "showrules";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Omustation - Rules smite - Added allow bypass argument
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var seconds = _configuration.GetCVar(CCVars.RulesWaitTime);

        // Omustation - Rules smite - Changed == to >=
        if (args.Length >= 2 && !float.TryParse(args[1], out seconds))
        {
            shell.WriteError(Loc.GetString("cmd-showrules-invalid-seconds", ("seconds", args[1])));
            return;
        }

        if (!_player.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError(Loc.GetString("shell-target-player-does-not-exist"));
            return;
        }

        // Omustation - Rules smite
        var allowBypass = true; // Default to true
        if (args.Length >= 3 && !bool.TryParse(args[2], out allowBypass))
        {
            shell.WriteError(Loc.GetString("shell-argument-must-be-boolean"));
            return;
        }

        var coreRules = _configuration.GetCVar(CCVars.RulesFile);
        var message = new SendRulesInformationMessage
            { PopupTime = seconds, CoreRules = coreRules, ShouldShowRules = true, AllowBypass = allowBypass }; // Omustation - Rules smite
        _net.ServerSendMessage(message, player.Channel);
    }

    // Omustation - Rules smite - Completely rewritten
    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(CompletionHelper.SessionNames(players: _player)),
            3 => CompletionResult.FromOptions(CompletionHelper.Booleans),
            _ => CompletionResult.Empty,
        };
    }
}
