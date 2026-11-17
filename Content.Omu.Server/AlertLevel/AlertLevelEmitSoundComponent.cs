// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;

namespace Content.Omu.Server.AlertLevel;

[RegisterComponent, Access(typeof(AlertLevelEmitSoundSystem))]
public sealed partial class AlertLevelEmitSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier DefaultSound = default!;

    [DataField]
    public Dictionary<string, SoundSpecifier> Sounds = new();
}
