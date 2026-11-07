// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Omu.Shared.Library;

public sealed class LibraryBookSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LibraryBookComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<LibraryBookComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || string.IsNullOrWhiteSpace(ent.Comp.Author))
            return;

        // The author is player input, it must never be interpreted as markup.
        args.PushMarkup(Loc.GetString("library-book-examine-author",
            ("author", FormattedMessage.EscapeText(ent.Comp.Author))));
    }
}
