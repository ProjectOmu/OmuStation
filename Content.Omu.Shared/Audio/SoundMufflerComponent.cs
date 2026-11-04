// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Shared.Audio;

[RegisterComponent]
public sealed partial class SoundMufflerComponent : Component
{
    [DataField]
    public float Weight = 1.5f;
}
