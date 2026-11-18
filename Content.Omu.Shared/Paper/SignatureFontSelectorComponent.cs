// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Shared.Paper;

[RegisterComponent]
public sealed partial class SignatureFontSelectorComponent : Component
{
    [DataField]
    public string? Font;
}
