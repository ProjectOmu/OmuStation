// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client._Impstation.Thaven;
using Content.Client.Silicons.Laws.Ui;
using Content.Omu.Shared.Arcade;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Omu.Client.Arcade;

public sealed class PolybiusFalseLawsSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly List<(RichTextLabel Label, FormattedMessage RealLaw)> _falseLaws = new();
    private TimeSpan _restoreTime;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_falseLaws.Count > 0)
        {
            if (_timing.CurTime >= _restoreTime)
                RestoreRealLaws();

            return;
        }

        if (!TryComp<PolybiusFalseLawsComponent>(_player.LocalEntity, out var falseLaws)
            || falseLaws.AlertsSeen == falseLaws.Alerts)
            return;

        var labels = FindLawLabels();
        if (labels.Count == 0)
            return;

        falseLaws.AlertsSeen = falseLaws.Alerts;
        ShowFalseLaws(falseLaws, labels);
    }

    private List<RichTextLabel> FindLawLabels()
    {
        if (_ui.WindowRoot.Children.OfType<ThavenMoodsMenu>().FirstOrDefault() is { } moods)
        {
            return moods.FindControl<BoxContainer>("MoodDisplayContainer").Children
                .OfType<MoodDisplay>()
                .SelectMany(mood => new[]
                {
                    mood.FindControl<RichTextLabel>("MoodNameLabel"),
                    mood.FindControl<RichTextLabel>("MoodDescLabel"),
                })
                .ToList();
        }

        if (_ui.WindowRoot.Children.OfType<SiliconLawMenu>().FirstOrDefault() is not { } menu)
            return [];

        return menu.FindControl<BoxContainer>("LawDisplayContainer").Children
            .OfType<LawDisplay>()
            .Select(law => law.FindControl<RichTextLabel>("LawLabel"))
            .ToList();
    }

    private void ShowFalseLaws(PolybiusFalseLawsComponent falseLaws, List<RichTextLabel> labels)
    {
        var words = _prototype.Index(falseLaws.Words).Values.ToList();
        _random.Shuffle(words);

        for (var i = 0; i < labels.Count; i++)
        {
            if (labels[i].GetFormattedMessage() is not { } realLaw)
                continue;

            _falseLaws.Add((labels[i], realLaw));
            labels[i].SetMessage(Loc.GetString(words[i % words.Count]));
        }

        _restoreTime = _timing.CurTime + falseLaws.FlashDuration;
    }

    private void RestoreRealLaws()
    {
        foreach (var (label, realLaw) in _falseLaws)
        {
            label.SetMessage(realLaw);
        }

        _falseLaws.Clear();
    }
}
