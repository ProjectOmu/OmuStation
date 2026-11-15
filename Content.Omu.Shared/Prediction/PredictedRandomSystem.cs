// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Common.Prediction;
using Content.Shared.Random.Helpers;

namespace Content.Omu.Shared.Prediction;

public sealed class PredictedRandomSystem : CommonPredictedRandomSystem
{
    public override System.Random GetRandom(EntityUid uid, int salt = 0)
    {
        var comp = EnsureComp<PredictedRandomComponent>(uid);
        var seed = SharedRandomExtensions.HashCodeCombine(GetNetEntity(uid).Id, comp.Rolls++, salt);
        Dirty(uid, comp);
        return new System.Random(seed);
    }
}
