// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;

namespace Content.Omu.Shared.Library;

/// <summary>
///     Helpers for cleaning player-provided text before it is stored in the archive or shown in a console.
/// </summary>
public static class LibraryText
{
    /// <summary>
    ///     Cleans single line text (titles, authors, borrower names): strips control characters
    ///     (including newlines), trims and clamps the length.
    /// </summary>
    public static string CleanLine(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input) || maxLength <= 0)
            return string.Empty;

        var builder = new StringBuilder(Math.Min(input.Length, maxLength));
        foreach (var c in input)
        {
            if (char.IsControl(c))
                continue;

            builder.Append(c);
            if (builder.Length >= maxLength)
                break;
        }

        return TrimSplitSurrogate(builder.ToString()).Trim();
    }

    /// <summary>
    ///     Cleans multi line book content: strips control characters other than line breaks / tabs and
    ///     clamps the length.
    /// </summary>
    public static string CleanContent(string? input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || maxLength <= 0)
            return string.Empty;

        var builder = new StringBuilder(Math.Min(input.Length, maxLength));
        foreach (var c in input)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                continue;

            builder.Append(c);
            if (builder.Length >= maxLength)
                break;
        }

        return TrimSplitSurrogate(builder.ToString());
    }

    /// <summary>
    ///     Don't leave half of a surrogate pair behind when a length clamp cut one in two.
    ///     (Plain string operations on purpose, the sandbox does not allow StringBuilder.Length.)
    /// </summary>
    private static string TrimSplitSurrogate(string text)
    {
        if (text.Length > 0 && char.IsHighSurrogate(text[text.Length - 1]))
            return text.Substring(0, text.Length - 1);

        return text;
    }
}
