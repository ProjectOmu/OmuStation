// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Common.Paper;

public abstract class CommonSignatureFontSystem : EntitySystem
{
    public abstract string? GetFont(EntityUid signer, EntityUid pen);

    public abstract string? GetTraitFont(ComponentRegistry components);
}
