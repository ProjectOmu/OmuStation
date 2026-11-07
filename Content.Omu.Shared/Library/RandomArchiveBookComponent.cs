// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Shared.Library;

/// <summary>
///     On map init the book takes the title, author and text of a random book from the library archive.
///     Used for map placed books and bookshelf fills, so the books lying around the station are the same
///     books a player can find (and print) in the library console.
/// </summary>
[RegisterComponent]
public sealed partial class RandomArchiveBookComponent : Component
{
}
