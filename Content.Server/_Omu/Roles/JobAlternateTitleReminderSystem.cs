// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat.Managers;
using Content.Shared._Omu.Roles;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Roles;

public sealed class JobAlternateTitleReminderSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly JobAlternateTitleSystem _alternateTitles = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_prototypes.TryIndex<JobPrototype>(ev.JobId, out var job))
            return;

        if (_alternateTitles.GetTitle(ev.Profile, job.ID) is not { } title)
            return;

        var message = Loc.GetString("job-alt-title-reminder",
            ("altTitle", title),
            ("jobName", job.LocalizedName));
        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, default, false, ev.Player.Channel);
    }
}
