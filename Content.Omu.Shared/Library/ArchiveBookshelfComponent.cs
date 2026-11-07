// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Shared.Library;

/// <summary>
///     Marks a bookshelf as a place where random archive books may be put when a station map loads.
///     The books are spread over all marked shelves of the station, not added to every shelf.
/// </summary>
[RegisterComponent]
public sealed partial class ArchiveBookshelfComponent : Component
{
}
