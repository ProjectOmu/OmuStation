// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using Content.Server.RandomMetadata;
using Content.Shared.Paper;
using Content.Shared.Sprite;

namespace Content.Omu.Server.Library;

/// <summary>
///     Fills books with <see cref="RandomArchiveBookComponent"/> with a random book from the library archive.
/// </summary>
public sealed class RandomArchiveBookSystem : EntitySystem
{
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After RandomMetadataSystem, which gives the book a random blank-book name on the same event.
        SubscribeLocalEvent<RandomArchiveBookComponent, MapInitEvent>(OnMapInit,
            after: [typeof(RandomMetadataSystem)]);
    }

    private void OnMapInit(Entity<RandomArchiveBookComponent> ent, ref MapInitEvent args)
    {
        if (_archive.TryPickRandom(out var entry))
            Fill(ent.Owner, entry);
    }

    /// <summary>
    ///     Gives a book the title, author and text of an archive book.
    /// </summary>
    public void Fill(EntityUid book, LibraryArchiveEntry entry)
    {
        _meta.SetEntityName(book, Loc.GetString("library-printed-book-name", ("title", entry.Title)));

        var info = EnsureComp<LibraryBookComponent>(book);
        info.Author = entry.Author;
        info.ArchiveId = entry.Id;
        Dirty(book, info);

        _paper.SetContent(book, entry.Content);

        ApplyCover(book, entry.Cover ?? LibraryCovers.Default(entry.Id));
    }

    /// <summary>
    ///     Gives a book a fixed cover instead of its random one.
    /// </summary>
    public void ApplyCover(EntityUid book, LibraryCover cover)
    {
        if (!TryComp<RandomSpriteComponent>(book, out var random))
            return;

        random.Selected["cover"] = (cover.Style, LibraryCovers.GetColor(cover.StyleColor));
        random.Selected["decor"] = (cover.Decor, LibraryCovers.GetColor(cover.DecorColor));
        random.Selected["icon"] = (cover.Icon, LibraryCovers.GetColor(cover.IconColor));
        random.Selected["overlay"] = (LibraryCovers.Blank, null);
        Dirty(book, random);
    }
}
