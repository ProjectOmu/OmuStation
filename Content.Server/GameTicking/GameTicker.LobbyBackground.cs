// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking.Prototypes;
using Content.Shared.Random.Helpers; // Omu - weighted lobby backgrounds
using Robust.Shared.Random;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

// Goobstation - this file is heavily modified to add credits for lobby backgrounds
public sealed partial class GameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private Dictionary<ProtoId<LobbyBackgroundPrototype>, float> _lobbyBackgrounds = []; // Omu - weighted lobby backgrounds

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        foreach (var prototype in _prototypeManager.EnumeratePrototypes<LobbyBackgroundPrototype>())
        {
            if (!WhitelistedBackgroundExtensions.Contains(prototype.Background.Extension))
            {
                _sawmill.Warning($"Lobby background '{prototype.ID}' has an invalid extension '{prototype.Background.Extension}' and will be ignored.");
                continue;
            }

            // Omu start - weighted lobby backgrounds
            if (prototype.Weight <= 0f)
                continue;

            _lobbyBackgrounds[prototype.ID] = prototype.Weight;
            // Omu end
        }

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground() {
        LobbyBackground = _lobbyBackgrounds.Any() ? _robustRandom.Pick(_lobbyBackgrounds) : (ProtoId<LobbyBackgroundPrototype>?) null;
    }
}
