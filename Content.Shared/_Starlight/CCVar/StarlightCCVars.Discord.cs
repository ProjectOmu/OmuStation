using Robust.Shared.Configuration;

namespace Content.Shared._Starlight.CCVar;
public sealed partial class StarlightCCVars
{
    /// <summary>
    /// Discord Webhooks
    /// </summary>

    public static readonly CVarDef<string> DiscordAdminAutoLogWebhook =
        CVarDef.Create("discord.admin_autolog", string.Empty, CVar.SERVERONLY);
}
