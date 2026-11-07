// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Omu.Client.Library;

[UsedImplicitly]
public sealed class LibraryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private LibraryConsoleWindow? _window;

    private LibraryReviewWindow? _review;
    private string _archiveFilter = string.Empty;
    private int _archivePage;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LibraryConsoleWindow>();
        _window.OnSearch += (title, author, category, id, page) =>
            SendMessage(new LibrarySearchMessage(title, author, category, id, page));
        _window.OnClearSearch += () => SendMessage(new LibraryClearSearchMessage());
        _window.OnPrint += id => SendMessage(new LibraryPrintBookMessage(id));
        _window.OnPrintBlank += (cover, title) => SendMessage(new LibraryPrintBlankMessage(cover, title));
        _window.OnRequestAccept += id => SendMessage(new LibraryRequestAcceptMessage(id));
        _window.OnRequestDeny += id => SendMessage(new LibraryRequestDenyMessage(id));
        _window.OnRefreshInventory += () => SendMessage(new LibraryRefreshInventoryMessage());
        _window.OnCheckout += (id, minutes) => SendMessage(new LibraryCheckoutMessage(id, minutes));
        _window.OnRemoveBook += id => SendMessage(new LibraryRemoveBookMessage(id));
        _window.OnClearMissing += id => SendMessage(new LibraryClearMissingMessage(id));
        _window.OnRegisterBook += () => SendMessage(new LibraryRegisterBookMessage());
        _window.OnEjectId += () => SendMessage(new LibraryEjectIdMessage());
        _window.OnCheckin += id => SendMessage(new LibraryCheckinMessage(id));
        _window.OnSetCache += (title, author) => SendMessage(new LibrarySetCacheMessage(title, author));
        _window.OnUpload += (category, cover) => SendMessage(new LibraryUploadMessage(category, cover));
        _window.OnReview += (id, accept) => SendMessage(new LibraryReviewMessage(id, accept));
        _window.OnReviewView += id => SendMessage(new LibraryReviewViewMessage(id));
        _window.OnAdminArchive += (filter, page) => SendMessage(new LibraryAdminArchiveRequestMessage(filter, page));
        _window.OnAdminDelete += id =>
        {
            _archiveFilter = _window?.AdminFilter ?? string.Empty;
            SendMessage(new LibraryAdminDeleteMessage(id, _archiveFilter, _archivePage));
        };
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);

        switch (message)
        {
            case LibraryModerationMessage moderation:
                _window?.UpdateModeration(moderation);
                break;
            case LibraryAdminArchiveMessage archive:
                _archiveFilter = archive.Filter;
                _archivePage = archive.Page;
                _window?.UpdateAdminArchive(archive);
                break;
            case LibraryReviewTextMessage text:
                OpenReview(text);
                break;
        }
    }

    private void OpenReview(LibraryReviewTextMessage text)
    {
        _review?.Close();

        var review = new LibraryReviewWindow(text);
        var id = text.Book.Id;
        review.OnAccept += () => SendMessage(new LibraryReviewMessage(id, true));
        review.OnDecline += () => SendMessage(new LibraryReviewMessage(id, false));
        review.OnDelete += () => SendMessage(new LibraryAdminDeleteMessage(id, _archiveFilter, _archivePage));
        _review = review;
        review.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _review?.Close();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is LibraryConsoleBoundUserInterfaceState cast)
            _window?.UpdateState(cast);
    }
}
