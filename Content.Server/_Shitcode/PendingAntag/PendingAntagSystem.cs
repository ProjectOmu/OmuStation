// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Goobstation.PendingAntag;

public sealed class PendingAntagSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AntagSelectionSystem _selection = default!;

    public Dictionary<NetUserId, (AntagSelectionDefinition, Entity<AntagSelectionComponent>)> PendingAntags = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin)
            return;
        // Omu start - to allow certain jobs to roll antag even if their CanBeAntag is false.
        if (!PendingAntags.Remove(ev.Player.UserId, out var pendingAntag))
            return;

        if (ev.JobId == null) return;

        var jobFound = _prototypeManager.TryIndex<JobPrototype>(ev.JobId, out var jobProto);
        var jobCanBeAntag = _prototypeManager.Index<JobPrototype>(ev.JobId).CanBeAntag || jobFound && pendingAntag.Item1.JobAntagImmunityOverride?.Contains(jobProto.ID) == true;

        if (ev.JobId == null || !jobCanBeAntag)
            return;

        // Omu end
        _selection.TryMakeAntag(pendingAntag.Item2, ev.Player, pendingAntag.Item1, true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        PendingAntags.Clear();
    }
}