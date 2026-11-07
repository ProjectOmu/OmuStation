// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Library;

/// <summary>
///     Marks a paper-based book as having library metadata. Books printed by the library console and
///     bound by the book binder carry this, the library scanner reads it when digitising a book.
///     Books without this component can still be scanned, they just have an unknown author.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LibraryBookComponent : Component
{
    /// <summary>
    ///     Who wrote the book. Empty means unknown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Author = string.Empty;

    /// <summary>
    ///     Unique books can not be scanned into the archive (the tgstation "unique" book flag).
    /// </summary>
    [DataField]
    public bool Unique;

    /// <summary>
    ///     The archive book this is a copy of, set when it was printed from the archive. Books that were bound
    ///     by hand have none. A console only accepts books into its inventory that are in the archive.
    /// </summary>
    [DataField]
    public int? ArchiveId;

    /// <summary>
    ///     Bound by the book binder from a written sheet. Print jobs can never be part of a console's inventory,
    ///     even when their text is in the archive.
    /// </summary>
    [DataField]
    public bool PrintJob;
}
