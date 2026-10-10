// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Goobstation.Common.Flash;

/// <summary>
/// Makes an entity impossible to protect from any flashes.
/// </summary>
[RegisterComponent]
public sealed partial class FlashVulnerableComponent : Component
{
    /// <summary>
    /// Flash vulnerability only triggers in this case when the user is using Night vision without appropriate Night vision goggles (Eg. Shadowkins own night vision).
    /// </summary>
    [DataField] public bool OnlyTriggerWhenNvIsOnWithoutNvGoggles; // Omu
}
