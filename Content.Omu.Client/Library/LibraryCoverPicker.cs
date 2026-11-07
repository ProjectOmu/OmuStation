// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Omu.Shared.Library;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Omu.Client.Library;

/// <summary>
///     Style, decor and icon pickers (each with a color) and a live preview of the book. Used by the upload tab
///     and the blank book tab of the library console.
/// </summary>
public sealed class LibraryCoverPicker : BoxContainer
{
    private static readonly ResPath BookRsi = new("/Textures/Objects/Misc/books.rsi");

    private readonly SpriteSystem _sprite;
    private readonly OptionButton _style = new();
    private readonly OptionButton _styleColor = new();
    private readonly OptionButton _decor = new();
    private readonly OptionButton _decorColor = new();
    private readonly OptionButton _icon = new();
    private readonly OptionButton _iconColor = new();

    private readonly TextureRect _previewPaper = PreviewLayer();
    private readonly TextureRect _previewCover = PreviewLayer();
    private readonly TextureRect _previewDecor = PreviewLayer();
    private readonly TextureRect _previewIcon = PreviewLayer();

    public LibraryCoverPicker()
    {
        _sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
        Orientation = LayoutOrientation.Horizontal;

        FillOptions(_style, LibraryCovers.Styles);
        FillOptions(_decor, LibraryCovers.Decors);
        FillOptions(_icon, LibraryCovers.Icons);
        foreach (var picker in new[] { _styleColor, _decorColor, _iconColor })
        {
            foreach (var (id, _) in LibraryCovers.Colors)
            {
                picker.AddItem(ColorName(id));
            }
        }

        // A nice default look to start from.
        var start = LibraryCovers.Default(3);
        _style.SelectId(Array.IndexOf(LibraryCovers.Styles, start.Style));
        _styleColor.SelectId(LibraryCovers.IndexOfColor(start.StyleColor));
        _decor.SelectId(Array.IndexOf(LibraryCovers.Decors, start.Decor));
        _decorColor.SelectId(LibraryCovers.IndexOfColor(start.DecorColor));
        _icon.SelectId(Array.IndexOf(LibraryCovers.Icons, start.Icon));
        _iconColor.SelectId(LibraryCovers.IndexOfColor(start.IconColor));

        foreach (var picker in new[] { _style, _styleColor, _decor, _decorColor, _icon, _iconColor })
        {
            picker.OnItemSelected += args =>
            {
                args.Button.SelectId(args.Id);
                UpdatePreview();
            };
        }

        var preview = new Control
        {
            MinSize = new Vector2(136, 136),
            VerticalAlignment = VAlignment.Top,
            Children = { _previewPaper, _previewCover, _previewDecor, _previewIcon },
        };

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Children =
            {
                Row(Loc.GetString("library-ui-cover-style"), _style, _styleColor),
                Row(Loc.GetString("library-ui-cover-decor"), _decor, _decorColor),
                Row(Loc.GetString("library-ui-cover-icon"), _icon, _iconColor),
            },
        });
        AddChild(preview);

        UpdatePreview();
    }

    /// <summary>
    ///     The cover that is currently picked.
    /// </summary>
    public LibraryCover Cover => new(
        LibraryCovers.Styles[_style.SelectedId],
        LibraryCovers.Colors[_styleColor.SelectedId].Id,
        LibraryCovers.Decors[_decor.SelectedId],
        LibraryCovers.Colors[_decorColor.SelectedId].Id,
        LibraryCovers.Icons[_icon.SelectedId],
        LibraryCovers.Colors[_iconColor.SelectedId].Id);

    /// <summary>
    ///     Picks the options of a cover, for example one that somebody asked for.
    /// </summary>
    public void SetCover(LibraryCover cover)
    {
        Select(_style, Array.IndexOf(LibraryCovers.Styles, cover.Style));
        Select(_styleColor, LibraryCovers.IndexOfColor(cover.StyleColor));
        Select(_decor, Array.IndexOf(LibraryCovers.Decors, cover.Decor));
        Select(_decorColor, LibraryCovers.IndexOfColor(cover.DecorColor));
        Select(_icon, Array.IndexOf(LibraryCovers.Icons, cover.Icon));
        Select(_iconColor, LibraryCovers.IndexOfColor(cover.IconColor));
        UpdatePreview();
    }

    private static void Select(OptionButton button, int id)
    {
        if (id >= 0)
            button.SelectId(id);
    }

    private static void FillOptions(OptionButton button, string[] states)
    {
        foreach (var state in states)
        {
            button.AddItem(StateName(state));
        }
    }

    private static string StateName(string state)
    {
        if (state == LibraryCovers.Blank)
            return Loc.GetString("library-ui-cover-none");

        var name = state;
        foreach (var prefix in new[] { "cover_", "decor_", "icon_" })
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                name = name.Substring(prefix.Length);
                break;
            }
        }

        name = name.Replace('_', ' ');
        return name.Length == 0 ? state : name.Substring(0, 1).ToUpperInvariant() + name.Substring(1);
    }

    private static string ColorName(string id)
    {
        if (id == LibraryCovers.Uncolored)
            return Loc.GetString("library-ui-cover-uncolored");

        return id.Substring(0, 1).ToUpperInvariant() + id.Substring(1);
    }

    private static TextureRect PreviewLayer()
    {
        return new TextureRect
        {
            Stretch = TextureRect.StretchMode.KeepCentered,
            TextureScale = new Vector2(4, 4),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
    }

    private void UpdatePreview()
    {
        var cover = Cover;

        SetPreviewLayer(_previewPaper, "paper", Color.White);
        SetPreviewLayer(_previewCover, cover.Style, LibraryCovers.GetColor(cover.StyleColor));
        SetPreviewLayer(_previewDecor, cover.Decor, LibraryCovers.GetColor(cover.DecorColor));
        SetPreviewLayer(_previewIcon, cover.Icon, LibraryCovers.GetColor(cover.IconColor));
    }

    private void SetPreviewLayer(TextureRect layer, string state, Color color)
    {
        layer.Texture = _sprite.Frame0(new SpriteSpecifier.Rsi(BookRsi, state));
        layer.Modulate = color;
    }

    private static Control Row(string label, OptionButton style, OptionButton color)
    {
        return new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Children =
            {
                new Label { Text = label, MinWidth = 90 },
                style,
                color,
            },
        };
    }
}
