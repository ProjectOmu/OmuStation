// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Omu.Shared.Arcade;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Client.Arcade;

public sealed class PolybiusHudFlickerSystem : EntitySystem
{
    private const double FlickerSlotSeconds = 0.5;
    private const double FlickerChance = 0.04;

    private static readonly ProtoId<SecurityIconPrototype> MindShieldIcon = "MindShieldIcon";
    private static readonly ProtoId<SecurityIconPrototype> BrokenMindShieldIcon = "MindShieldBrokenIcon";

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetStatusIconsEvent>(OnGetStatusIcons);
    }

    private void OnGetStatusIcons(ref GetStatusIconsEvent ev)
    {
        if (_player.LocalEntity is not { } viewer || !HasComp<PolybiusHudFlickerComponent>(viewer))
            return;

        var jobIndex = ev.StatusIcons.FindIndex(icon => icon is JobIconPrototype);
        if (jobIndex < 0)
            return;

        var slot = (long) (_timing.CurTime.TotalSeconds / FlickerSlotSeconds);
        var random = new System.Random(HashCode.Combine(GetNetEntity(ev.Uid).Id, slot));
        if (random.NextDouble() >= FlickerChance)
            return;

        if (random.Next(2) == 0)
        {
            var jobs = _prototype.EnumeratePrototypes<JobIconPrototype>().ToList();
            ev.StatusIcons[jobIndex] = jobs[random.Next(jobs.Count)];
            return;
        }

        ToggleMindShield(ev.StatusIcons, viewer);
    }

    private void ToggleMindShield(List<StatusIconData> icons, EntityUid viewer)
    {
        var shieldIndex = icons.FindIndex(icon => icon is SecurityIconPrototype security
            && (security.ID == MindShieldIcon.Id || security.ID == BrokenMindShieldIcon.Id));

        if (shieldIndex >= 0)
        {
            icons.RemoveAt(shieldIndex);
            return;
        }

        var hud = new RefreshEquipmentHudEvent<ShowMindShieldIconsComponent>(~SlotFlags.POCKET, false);
        RaiseLocalEvent(viewer, ref hud);

        if (hud.Active)
            icons.Add(_prototype.Index(MindShieldIcon));
    }
}
