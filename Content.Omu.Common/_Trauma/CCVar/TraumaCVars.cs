using Robust.Shared.Configuration;

namespace Content.Omu.Common._Trauma.CCVar;

[CVarDefs]
public sealed partial class TraumaCVars
{
    #region Gun prediction

    /// <summary>
    /// Distance used between projectile and lag-compensated target position for gun prediction.
    /// </summary>
    public static readonly CVarDef<float> GunLagCompRange =
        CVarDef.Create("trauma.gun_lag_comp_range", 0.6f, CVar.SERVER);

    #endregion
}
