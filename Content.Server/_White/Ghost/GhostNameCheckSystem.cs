using System.Linq;
using Content.Server.Administration.Systems;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using Content.Server.Chat.Managers;

namespace Content.Server._White.Ghost;

public sealed class GhostNameCheckSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    /// <summary>
    ///     Checks to see whether the player character's current name is similar to the name of a character whom they previously played in the current shift.
    ///     The goal is to make sure the player isn't respawning as the same character which died earlier in the shift.
    /// </summary>
    /// <returns>
    ///     False if the current character's name is identical, or similar, to any previously-played character's name.
    /// </returns>
    public bool CheckGhostReturnToRound(ICommonSession player, HumanoidCharacterProfile character, out bool checkAvoid)
    {
        checkAvoid = false;

        var allPlayerMinds = EntityQuery<MindComponent>().Where(mind => mind.OriginalOwnerUserId == player.UserId);

        foreach (var mind in allPlayerMinds)
        {
            // If the player is playing a character with the exact same name as a previously played character.
            if (mind.CharacterName == character.Name)
                return false;

            // If the previously played character has no name, we don't have to do the following check.
            if (mind.CharacterName == null)
                continue;

            // Calculate the similarity, as a percentage, between the names of the current player character and the previously played character.
            var similarity = CalculateStringSimilarity(mind.CharacterName, character.Name);
            if (similarity >= 85f) // Omustation - changed this to an if statement. Why use a switch statement here? It just makes the whole thing harder to read.
            {
                _chatManager.SendAdminAlert(Loc.GetString("ghost-respawn-log-character-almost-same",
                    ("player", player.Name), ("try", false), ("oldName", mind.CharacterName),
                    ("newName", character.Name)));

                checkAvoid = true;
                return false;
            }
            else if (similarity >= 50f)
            {
                _chatManager.SendAdminAlert(Loc.GetString("ghost-respawn-log-character-almost-same",
                    ("player", player.Name), ("try", true), ("oldName", mind.CharacterName),
                    ("newName", character.Name)));
            }
        }

        return true;
    }

    /// <summary>
    ///     Calculates the similarity between two strings.
    /// </summary>
    /// <returns>
    ///     The string similarity, as a percentage.
    /// </returns>
    private float CalculateStringSimilarity(string str1, string str2)
    {
        var minLength = Math.Min(str1.Length, str2.Length);
        var matchingCharacters = 0;

        for (var i = 0; i < minLength; i++)
        {
            if (str1[i] == str2[i])
                matchingCharacters++;
        }

        float maxLength = Math.Max(str1.Length, str2.Length);
        var similarityPercentage = matchingCharacters / maxLength * 100;

        return similarityPercentage;
    }
}
