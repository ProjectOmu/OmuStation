// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Omu.Shared.Library;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Omu.Client.Library;

/// <summary>
///     Separate window where an admin reads a whole book. Books waiting for review can be accepted or declined
///     here, archived books can be deleted. Every action needs a second click to confirm.
/// </summary>
public sealed class LibraryReviewWindow : DefaultWindow
{
    public readonly int BookId;
    public readonly bool Pending;

    public event Action? OnAccept;
    public event Action? OnDecline;
    public event Action? OnDelete;

    public LibraryReviewWindow(LibraryReviewTextMessage message)
    {
        BookId = message.Book.Id;
        Pending = message.Pending;

        Title = Loc.GetString("library-ui-review-window-title", ("id", message.Book.Id));
        MinSize = new Vector2(420, 380);
        SetSize = new Vector2(480, 520);

        var text = new RichTextLabel { HorizontalExpand = true };
        text.SetMessage(FormattedMessage.FromUnformatted(message.Content));

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        if (message.Pending)
        {
            var accept = MakeConfirm("library-ui-review-accept");
            accept.OnPressed += _ =>
            {
                OnAccept?.Invoke();
                Close();
            };

            var decline = MakeConfirm("library-ui-review-decline");
            decline.OnPressed += _ =>
            {
                OnDecline?.Invoke();
                Close();
            };

            buttons.AddChild(accept);
            buttons.AddChild(decline);
        }
        else
        {
            var delete = MakeConfirm("library-ui-admin-delete");
            delete.OnPressed += _ =>
            {
                OnDelete?.Invoke();
                Close();
            };

            buttons.AddChild(delete);
        }

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new Thickness(4),
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("library-ui-review-title",
                        ("id", message.Book.Id),
                        ("title", message.Book.Title),
                        ("author", string.IsNullOrWhiteSpace(message.Book.Author)
                            ? Loc.GetString("library-ui-unknown")
                            : message.Book.Author)),
                    ClipText = true,
                },
                new Label
                {
                    Text = Loc.GetString("library-ui-review-uploader",
                        ("user", message.Book.UploaderUser),
                        ("character", message.Book.UploaderCharacter),
                        ("category", message.Book.Category),
                        ("length", message.Book.Length)),
                    FontColorOverride = Color.Gray,
                    ClipText = true,
                },
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HorizontalExpand = true,
                    HScrollEnabled = false,
                    Children = { text },
                },
                buttons,
            },
        });
    }

    public static ConfirmButton MakeConfirm(string textId)
    {
        return new ConfirmButton
        {
            Text = Loc.GetString(textId),
            ConfirmationText = Loc.GetString("library-ui-confirm"),
            ResetTime = TimeSpan.FromSeconds(3),
        };
    }
}
