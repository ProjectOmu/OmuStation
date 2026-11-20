using Content.Shared.Chat;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Chat;

public abstract class SharedCMChatSystem : EntitySystem
{
    public virtual void ChatMessageToMany(
        string message,
        string wrappedMessage,
        Filter filter,
        ChatChannel channel,
        EntityUid source = default,
        bool hideChat = false,
        Color? colorOverride = null,
        bool recordReplay = false,
        string? audioPath = null,
        float audioVolume = 0,
        NetUserId? author = null)
    {
    }
}
