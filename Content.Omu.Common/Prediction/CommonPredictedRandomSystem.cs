// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Omu.Common.Prediction;

public abstract class CommonPredictedRandomSystem : EntitySystem
{
    public abstract System.Random GetRandom(EntityUid uid, int salt = 0);
}
