ghost-respawn-time-left = You must wait { $time } { $time ->
        [one] minute
       *[other] minutes
    } before returning to the round.

ghost-respawn-max-players = This function is not available when there are greater than { $players } players on the server.
ghost-respawn-window-title = Rules for returning to the round:
ghost-respawn-window-rules-footer = By using the respawn feature, you [color=#ff7700]agree[/color] [color=#ff0000]not to transfer[/color] the knowledge of your past character to a new one.
ghost-respawn-same-character = You cannot rejoin a shift using the same character. Change it in the character settings.
ghost-respawn-same-character-slightly-changed-name = You cannot rejoin a shift with the same character, and a slightly different name.
ghost-respawn-log-character-almost-same = Player { $player } { $try ->
    [true] joined
    *[false] tried to join
} the round after the respawn with a similar name. Past name: { $oldName }, current: { $newName }.

ghost-respawn-log-return-to-lobby = { $userName } returned to the lobby.