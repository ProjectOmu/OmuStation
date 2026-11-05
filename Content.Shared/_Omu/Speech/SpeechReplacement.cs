// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Omu.Speech;

[DataDefinition, Serializable, NetSerializable]
public sealed partial record SpeechReplacement
{
    [DataField(required: true)]
    public string Word = string.Empty;

    [DataField(required: true)]
    public string Replacement = string.Empty;
}
