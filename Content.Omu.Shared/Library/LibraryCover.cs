// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Library;

/// <summary>
///     How a book from the archive looks: a cover style, a decoration and an icon, each with a color.
///     The style, decor and icon are sprite states of <c>Objects/Misc/books.rsi</c>, the colors are
///     ids from <see cref="LibraryCovers.Colors"/>.
/// </summary>
[Serializable, NetSerializable]
public record struct LibraryCover(
    string Style,
    string StyleColor,
    string Decor,
    string DecorColor,
    string Icon,
    string IconColor);

/// <summary>
///     The covers a player can pick when uploading a book, and the default cover of books that were never given one.
/// </summary>
public static class LibraryCovers
{
    /// <summary>Sprite state that draws nothing, used for "no decoration" and "no icon".</summary>
    public const string Blank = "overlay_null";

    /// <summary>Color id that leaves the sprite as drawn.</summary>
    public const string Uncolored = "none";

    public static readonly string[] Styles =
    [
        "cover_base",
        "cover_old",
        "cover_strong",
    ];

    public static readonly string[] Decors =
    [
        Blank,
        "decor_wingette",
        "decor_wingette_flat",
        "decor_wingette_circle",
        "decor_bottom",
        "decor_diagonal",
        "decor_middle",
        "decor_spine",
        "decor_vertical_middle",
    ];

    public static readonly string[] Icons =
    [
        Blank,
        "icon_apple",
        "icon_atmos",
        "icon_aurora",
        "icon_banana",
        "icon_bar",
        "icon_biohazard",
        "icon_borg",
        "icon_briefcase",
        "icon_bucket",
        "icon_cabbage",
        "icon_chemical",
        "icon_corner",
        "icon_diamond",
        "icon_dna",
        "icon_eye",
        "icon_fish",
        "icon_glow",
        "icon_hacking",
        "icon_ian",
        "icon_law",
        "icon_letter_N",
        "icon_letter_P",
        "icon_lightning",
        "icon_magic",
        "icon_magic_fireball",
        "icon_magic_forcewall",
        "icon_magic_knock",
        "icon_magnifier",
        "icon_manifesto",
        "icon_medical",
        "icon_medical_cross",
        "icon_mount",
        "icon_nuclear",
        "icon_origami",
        "icon_pentagramm",
        "icon_planet",
        "icon_possum",
        "icon_question",
        "icon_scmmd",
        "icon_skull",
        "icon_stars",
        "icon_stars2",
        "icon_stunbaton",
        "icon_temple",
        "icon_text",
        "icon_text2",
        "icon_text3",
        "icon_time",
        "icon_tree",
        "icon_wrench",
    ];

    public static readonly (string Id, Color Color)[] Colors =
    [
        (Uncolored, Color.White),
        ("black", Color.FromHex("#2b2b2e")),
        ("gray", Color.FromHex("#6d6d73")),
        ("white", Color.FromHex("#e6e6e6")),
        ("red", Color.FromHex("#a6161d")),
        ("orange", Color.FromHex("#cf792d")),
        ("yellow", Color.FromHex("#d9b53f")),
        ("green", Color.FromHex("#3f7d3c")),
        ("teal", Color.FromHex("#2f8f8a")),
        ("blue", Color.FromHex("#2f5fa8")),
        ("navy", Color.FromHex("#1f2f5c")),
        ("purple", Color.FromHex("#6e3f91")),
        ("pink", Color.FromHex("#c9658f")),
        ("brown", Color.FromHex("#6b4a2f")),
        ("tan", Color.FromHex("#b89868")),
    ];

    public static Color GetColor(string id)
    {
        foreach (var (colorId, color) in Colors)
        {
            if (colorId == id)
                return color;
        }

        return Color.White;
    }

    public static int IndexOfColor(string id)
    {
        for (var i = 0; i < Colors.Length; i++)
        {
            if (Colors[i].Id == id)
                return i;
        }

        return 0;
    }

    /// <summary>
    ///     A cover that is always the same for the same archive id, for books nobody picked a cover for.
    /// </summary>
    public static LibraryCover Default(int id)
    {
        var n = Math.Abs(id);
        return new LibraryCover(
            Styles[n % Styles.Length],
            Colors[1 + n * 7 % (Colors.Length - 1)].Id,
            Decors[n * 5 % Decors.Length],
            Colors[1 + (n * 11 + 3) % (Colors.Length - 1)].Id,
            Icons[1 + n * 13 % (Icons.Length - 1)],
            Uncolored);
    }

    /// <summary>
    ///     Replaces everything in a cover that is not a known option, so clients can't send arbitrary sprite states.
    /// </summary>
    public static LibraryCover Sanitize(LibraryCover cover)
    {
        var fallback = Default(0);
        return new LibraryCover(
            Pick(Styles, cover.Style, fallback.Style),
            PickColor(cover.StyleColor, fallback.StyleColor),
            Pick(Decors, cover.Decor, Blank),
            PickColor(cover.DecorColor, fallback.DecorColor),
            Pick(Icons, cover.Icon, Blank),
            PickColor(cover.IconColor, Uncolored));
    }

    private static string Pick(string[] options, string? value, string fallback)
    {
        return value != null && Array.IndexOf(options, value) >= 0 ? value : fallback;
    }

    private static string PickColor(string? value, string fallback)
    {
        foreach (var (id, _) in Colors)
        {
            if (id == value)
                return value;
        }

        return fallback;
    }
}
