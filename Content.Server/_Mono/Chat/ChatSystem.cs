using Content.Shared.Chat;

namespace Content.Server._Mono.Chat;

public sealed class CheckTargetedSpeechEvent : EntityEventArgs    //Mono
{
    public List<InGameICChatType> ChatTypeIgnore = new();
    public List<EntityUid> Targets = new();
}                    //Mono
