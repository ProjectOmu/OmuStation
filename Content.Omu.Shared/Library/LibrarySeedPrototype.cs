// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Library;

/// <summary>
///     Premade books and texts that are copied into the library archive when the archive does not have them
///     yet (and used as a fallback when the archive is unavailable). A seed book a moderator deleted is not
///     added again. Every seed book is also put on the station's bookshelves at round start.
/// </summary>
[Prototype]
public sealed partial class LibrarySeedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Author = "Unknown";

    [DataField]
    public string Category = LibraryConstants.DefaultUploadCategory;

    /// <summary>Premade book entities, their name is the title and their paper content the text.</summary>
    [DataField]
    public List<EntProtoId> Books = new();

    /// <summary>Premade texts that no book entity uses, such as short stories and essays.</summary>
    [DataField]
    public List<LibrarySeedText> Texts = new();
}

[DataDefinition]
public sealed partial class LibrarySeedText
{
    [DataField(required: true)]
    public string Title = string.Empty;

    [DataField]
    public string? Author;

    /// <summary>Locale key of the text.</summary>
    [DataField(required: true)]
    public string Text = string.Empty;
}
