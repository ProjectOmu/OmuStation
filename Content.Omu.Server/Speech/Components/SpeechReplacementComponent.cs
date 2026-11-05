// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Shared._Omu.Speech;

namespace Content.Omu.Server.Speech.Components;

[RegisterComponent]
public sealed partial class SpeechReplacementComponent : Component
{
    public const string Shield = "\u034F";

    [DataField(required: true)]
    public List<SpeechReplacement> Replacements = new();

    public Regex? Pattern;

    public readonly Dictionary<string, string> Lookup = new(StringComparer.OrdinalIgnoreCase);
}
