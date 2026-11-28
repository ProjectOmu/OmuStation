// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Stylesheets;
using Content.Omu.Shared.Billiards;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Omu.Client.Billiards.UI;

public sealed class BilliardsPlate : PanelContainer
{
    public const string StyleClassPlate = "BilliardsPlate";
    public const string StyleClassPlateActive = "BilliardsPlateActive";
    public const string StyleClassPortrait = "BilliardsPortrait";
    public const string StyleClassLife = "BilliardsLife";
    public const string StyleClassLifeLost = "BilliardsLifeLost";

    private const float PlateWidth = 184f;
    private const float PipSize = 10f;
    private const float GroupBallSize = 15f;

    private readonly BilliardsPortrait _portrait;
    private readonly Label _name;
    private readonly Label _score;
    private readonly BoxContainer _lives;
    private readonly BilliardsBallRow _group;

    public BilliardsPlate()
    {
        SetWidth = PlateWidth;

        _portrait = new BilliardsPortrait();
        _name = new Label { ClipText = true, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        _score = new Label { StyleClasses = { StyleClass.LabelHeadingBigger } };
        _lives = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
            VerticalAlignment = VAlignment.Center,
        };
        _group = new BilliardsBallRow { SetWidth = GroupBallSize, VerticalAlignment = VAlignment.Center };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children =
            {
                new PanelContainer
                {
                    StyleClasses = { StyleClassPortrait },
                    VerticalAlignment = VAlignment.Center,
                    Children = { _portrait },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    SeparationOverride = 6,
                    Children = { _name, _group, _score, _lives },
                },
            },
        });
    }

    public void SetPlayer(BilliardsPlayer player, int groupBall, int lifeSlots, bool active, bool compact)
    {
        _portrait.SetEntity(player.Entity, compact);
        _name.Text = player.Name;
        _score.Text = player.Score.ToString();
        _score.Visible = lifeSlots == 0;
        _score.SetOnlyStyleClass(compact ? StyleClass.LabelHeading : StyleClass.LabelHeadingBigger);
        _group.SetBalls(groupBall > 0 ? 1 << groupBall : 0);
        _group.Visible = groupBall > 0;
        SetLives(player.Score, lifeSlots);
        SetOnlyStyleClass(active ? StyleClassPlateActive : StyleClassPlate);
    }

    private void SetLives(int remaining, int slots)
    {
        while (_lives.ChildCount > slots)
        {
            _lives.RemoveChild(_lives.ChildCount - 1);
        }

        while (_lives.ChildCount < slots)
        {
            _lives.AddChild(new PanelContainer { SetSize = new Vector2(PipSize) });
        }

        for (var index = 0; index < slots; index++)
        {
            _lives.GetChild(index).SetOnlyStyleClass(index < remaining ? StyleClassLife : StyleClassLifeLost);
        }

        _lives.Visible = slots > 0;
    }
}
