// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Omu.Common.CCVar;
using Content.Omu.Server.Entities.Heretic;
using Content.Server._DV.CartridgeLoader.Cartridges;
using Content.Shared._DV.NanoChat;
using Content.Omu.Shared.Library;
using Content.Server.Atmos.Components;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Access.Systems;
using Content.Shared.Administration;
using Content.Shared.Atmos;
using Content.Shared.Administration.Logs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Library;

/// <summary>
///     The library console ("book inventory management console"): search the archive and print books from it,
///     upload books digitised by a nearby scanner, and keep track of the books lent out.
///     Ported from tgstation's /obj/machinery/computer/libraryconsole(/bookmanagement).
/// </summary>
public sealed class LibraryConsoleSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LibraryRoomSystem _room = default!;
    [Dependency] private readonly NanoChatCartridgeSystem _nanoChat = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;
    [Dependency] private readonly BookbinderSystem _binder = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly LibraryAccessSystem _libraryAccess = default!;
    [Dependency] private readonly LibraryScannerSystem _scanner = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LibraryConsoleComponent, NewLinkEvent>(OnLinkChanged);
        SubscribeLocalEvent<LibraryConsoleComponent, PortDisconnectedEvent>(OnLinkRemoved);
        SubscribeLocalEvent<LibraryConsoleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LibraryConsoleComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<LibraryConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<LibraryConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<LibraryConsoleComponent, ItemSlotInsertAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<LibraryConsoleComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<LibraryConsoleComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<LibraryConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryConsoleRefreshEvent>(OnRefresh);

        SubscribeLocalEvent<LibraryConsoleComponent, LibrarySearchMessage>(OnSearch);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryClearSearchMessage>(OnClearSearch);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryPrintBookMessage>(OnPrintBook);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryPrintBlankMessage>(OnPrintBlank);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryRequestAcceptMessage>(OnRequestAccept);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryRequestDenyMessage>(OnRequestDeny);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryRefreshInventoryMessage>(OnRefreshInventory);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryCheckoutMessage>(OnCheckout);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryCheckinMessage>(OnCheckin);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryRemoveBookMessage>(OnRemoveBook);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryRegisterBookMessage>(OnRegisterBook);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryClearMissingMessage>(OnClearMissing);
        SubscribeLocalEvent<LibraryConsoleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryEjectIdMessage>(OnEjectId);
        SubscribeLocalEvent<LibraryConsoleComponent, LibrarySetCacheMessage>(OnSetCache);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryUploadMessage>(OnUpload);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryReviewMessage>(OnReview);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryReviewViewMessage>(OnReviewView);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryAdminArchiveRequestMessage>(OnAdminArchiveRequest);
        SubscribeLocalEvent<LibraryConsoleComponent, LibraryAdminDeleteMessage>(OnAdminDelete);

        _archive.ArchiveChanged += OnArchiveChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _archive.ArchiveChanged -= OnArchiveChanged;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.RefreshAfterCooldown && now >= component.NextPrintTime)
            {
                component.RefreshAfterCooldown = false;
                UpdateUi(uid, component);
            }

            // The books in the room at the start of the round make up the first inventory.
            if (!component.InitialScanDone && now >= component.InitialScanTime)
            {
                component.InitialScanDone = true;
                RegisterRoundStartBooks(uid, component);
            }

            if (component.InitialScanDone && now >= component.NextEvaluate)
            {
                component.NextEvaluate = now + TimeSpan.FromSeconds(5);
                EvaluateBooks(uid, component);
                NotifyOverdueLoans(component);
            }
        }
    }

    private void OnMapInit(EntityUid uid, LibraryConsoleComponent component, MapInitEvent args)
    {
        // Give shelves and the other round start spawns a moment to fill up first.
        component.InitialScanTime = _timing.CurTime + TimeSpan.FromSeconds(10);
    }

    #region NanoChat

    /// <summary>
    ///     Messages the owner of an ID card, on behalf of the library.
    /// </summary>
    private void SendChat(EntityUid? card, string locId)
    {
        if (card is not { } uid || TerminatingOrDeleted(uid) || !TryComp<NanoChatCardComponent>(uid, out var chat))
            return;

        _nanoChat.DeliverAnonymousMessage(
            (uid, chat),
            LibraryConstants.NanoChatSenderNumber,
            Loc.GetString("library-nanochat-sender"),
            Loc.GetString(locId));
    }

    /// <summary>
    ///     Tells borrowers whose book is past its return date, once per loan. Loans without a date never get here.
    /// </summary>
    private void NotifyOverdueLoans(LibraryConsoleComponent component)
    {
        var now = _timing.CurTime;
        foreach (var loan in component.Loans)
        {
            if (loan.Indefinite || loan.OverdueNotified || now < loan.DueTime)
                continue;

            loan.OverdueNotified = true;
            SendChat(loan.BorrowerCard, "library-nanochat-overdue");
        }
    }

    #endregion

    #region Book requests

    /// <summary>
    ///     Raised when the requests of a library console changed, so the request consoles can show the new status.
    /// </summary>
    public event Action<EntityUid>? RequestsChanged;

    /// <summary>
    ///     Adds a request that came from a request console. <paramref name="failure"/> is a locale id that says why
    ///     it was refused.
    /// </summary>
    public bool TryAddRequest(
        EntityUid uid,
        LibraryConsoleComponent component,
        LibraryBookRequest request,
        out string failure)
    {
        failure = string.Empty;

        if (component.Requests.Count(r => r.Status == LibraryRequestStatus.Pending) >= LibraryConstants.MaxPendingRequests)
        {
            failure = "library-request-full";
            return false;
        }

        var fromSource = component.Requests.Where(r => r.Source == request.Source && r.Status == LibraryRequestStatus.Pending)
            .ToList();
        if (fromSource.Count >= LibraryConstants.MaxRequestsPerConsole)
        {
            failure = "library-request-too-many";
            return false;
        }

        if (request.Kind == LibraryRequestKind.Archive &&
            fromSource.Any(r => r.Kind == LibraryRequestKind.Archive && r.ArchiveId == request.ArchiveId))
        {
            failure = "library-request-duplicate";
            return false;
        }

        request.Id = component.NextRequestId++;
        request.Status = LibraryRequestStatus.Pending;
        component.Requests.Add(request);
        TrimResolvedRequests(component);

        if (_power.IsPowered(uid))
            _popup.PopupEntity(Loc.GetString("library-console-new-request"), uid);

        NotifyRequests(uid, component);
        return true;
    }

    /// <summary>
    ///     Takes back a request of a request console, or dismisses an answered one.
    /// </summary>
    public bool TryRemoveRequest(EntityUid uid, LibraryConsoleComponent component, int id, EntityUid source)
    {
        if (component.Requests.RemoveAll(r => r.Id == id && r.Source == source) == 0)
            return false;

        NotifyRequests(uid, component);
        return true;
    }

    /// <summary>
    ///     The requests that were sent from one request console, in the order they were made.
    /// </summary>
    public List<LibraryRequestEntry> GetRequestEntries(LibraryConsoleComponent component, EntityUid? source)
    {
        return component.Requests
            .Where(r => source == null || r.Source == source)
            .OrderBy(r => r.Status != LibraryRequestStatus.Pending)
            .ThenBy(r => r.Id)
            .Select(r => new LibraryRequestEntry(r.Id, r.Kind, r.ArchiveId, r.Title, r.Author, r.Cover, r.Requester, r.Status))
            .ToList();
    }

    private void TrimResolvedRequests(LibraryConsoleComponent component)
    {
        while (component.Requests.Count(r => r.Status != LibraryRequestStatus.Pending) > LibraryConstants.MaxResolvedRequests)
        {
            var oldest = component.Requests.First(r => r.Status != LibraryRequestStatus.Pending);
            component.Requests.Remove(oldest);
        }
    }

    private void NotifyRequests(EntityUid uid, LibraryConsoleComponent component)
    {
        UpdateUi(uid, component);
        RequestsChanged?.Invoke(uid);
    }

    private void OnRequestAccept(EntityUid uid, LibraryConsoleComponent component, LibraryRequestAcceptMessage args)
    {
        AnswerRequest(uid, component, args.Id, args.Actor, LibraryRequestStatus.Accepted);
    }

    private void OnRequestDeny(EntityUid uid, LibraryConsoleComponent component, LibraryRequestDenyMessage args)
    {
        AnswerRequest(uid, component, args.Id, args.Actor, LibraryRequestStatus.Denied);
    }

    private void AnswerRequest(
        EntityUid uid,
        LibraryConsoleComponent component,
        int id,
        EntityUid actor,
        LibraryRequestStatus status)
    {
        if (!_power.IsPowered(uid) || !_libraryAccess.IsAllowed(uid, actor, true))
            return;

        var request = component.Requests.FirstOrDefault(r => r.Id == id && r.Status == LibraryRequestStatus.Pending);
        if (request == null)
            return;

        request.Status = status;
        TrimResolvedRequests(component);
        SendChat(request.RequesterCard, status == LibraryRequestStatus.Accepted
            ? "library-nanochat-request-ready"
            : "library-nanochat-request-denied");
        NotifyRequests(uid, component);
    }

    #endregion

    private void OnArchiveChanged()
    {
        var query = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            UpdateUi(uid, component);
            SendModerationToAll(uid);
        }
    }

    private void OnInit(EntityUid uid, LibraryConsoleComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, LibraryConsoleComponent.IdSlotId, component.IdSlot);
    }

    private void OnRemove(EntityUid uid, LibraryConsoleComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.IdSlot);
    }

    private void OnSlotChanged(EntityUid uid, LibraryConsoleComponent component, ContainerModifiedMessage args)
    {
        if (!component.Initialized || args.Container.ID != component.IdSlot.ID)
            return;

        UpdateUi(uid, component);
    }

    private void OnInsertAttempt(EntityUid uid, LibraryConsoleComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.User is { } user && !_libraryAccess.IsAllowed(uid, user))
            args.Cancelled = true;
    }

    private void OnEjectAttempt(EntityUid uid, LibraryConsoleComponent component, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is { } user && !_libraryAccess.IsAllowed(uid, user))
            args.Cancelled = true;
    }

    private void OnUiOpenAttempt(EntityUid uid, LibraryConsoleComponent component, ActivatableUIOpenAttemptEvent args)
    {
        if (!_libraryAccess.IsAllowed(uid, args.User, args.Silent))
            args.Cancel();
    }

    private void OnEjectId(EntityUid uid, LibraryConsoleComponent component, LibraryEjectIdMessage args)
    {
        _itemSlots.TryEjectToHands(uid, component.IdSlot, args.Actor, true);
    }

    /// <summary>
    ///     The name on the ID that was put into the console, if there is one.
    /// </summary>
    private bool TryGetBorrowerName(LibraryConsoleComponent component, out string name)
    {
        name = string.Empty;
        if (component.IdSlot.Item is not { } item || !_idCard.TryFindIdCard(item, out var card))
            return false;

        name = LibraryText.CleanLine(card.Comp.FullName, LibraryConstants.MaxBorrowerLength);
        return name.Length > 0;
    }

    private void OnUiOpened(EntityUid uid, LibraryConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        // Looking at the shelves is done when the console is used, like tgstation's dynamic inventory load.
        EvaluateBooks(uid, component);
        UpdateUi(uid, component);
        SendModeration(uid, args.User);
    }

    private void OnRefresh(EntityUid uid, LibraryConsoleComponent component, LibraryConsoleRefreshEvent args)
    {
        UpdateUi(uid, component);
    }

    #region Archive

    private void OnSearch(EntityUid uid, LibraryConsoleComponent component, LibrarySearchMessage args)
    {
        if (!TryRequest(uid, component))
            return;

        component.SearchTitle = LibraryText.CleanLine(args.Title, LibraryConstants.MaxTitleLength);
        component.SearchAuthor = LibraryText.CleanLine(args.Author, LibraryConstants.MaxAuthorLength);
        component.SearchCategory = LibraryConstants.IsSearchCategory(args.Category)
            ? args.Category
            : LibraryConstants.AnyCategory;
        component.SearchId = args.Id is > 0 ? args.Id : null;
        component.SearchPage = Math.Max(args.Page, 0);

        UpdateUi(uid, component);
    }

    private void OnClearSearch(EntityUid uid, LibraryConsoleComponent component, LibraryClearSearchMessage args)
    {
        // The captain just walked in on your browsing, quick! delete it!
        if (!TryRequest(uid, component))
            return;

        component.SearchTitle = string.Empty;
        component.SearchAuthor = string.Empty;
        component.SearchCategory = LibraryConstants.AnyCategory;
        component.SearchId = null;
        component.SearchPage = 0;

        UpdateUi(uid, component);
    }

    private void OnPrintBook(EntityUid uid, LibraryConsoleComponent component, LibraryPrintBookMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (!_archive.Available)
        {
            Say(uid, component, "library-console-archive-severed");
            return;
        }

        if (_timing.CurTime < component.NextPrintTime)
        {
            Say(uid, component, "library-console-printer-busy");
            return;
        }

        if (!_archive.TryGet(args.Id, out _))
        {
            Say(uid, component, "library-console-print-error");
            return;
        }

        // The book binder does the printing, the console only sends the signal.
        if (!TryGetBookbinder(uid, out var binder))
        {
            Say(uid, component, "library-console-no-bookbinder");
            return;
        }

        if (!_binder.TryStartPrint(binder.Owner, binder.Comp, args.Id, args.Actor, out var failure))
        {
            Say(uid, component, failure);
            return;
        }

        component.NextPrintTime = _timing.CurTime + component.PrintCooldown;
        component.RefreshAfterCooldown = true;
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     Prints an empty book. It does not need the archive, and the book is not in it.
    /// </summary>
    private void OnPrintBlank(EntityUid uid, LibraryConsoleComponent component, LibraryPrintBlankMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (_timing.CurTime < component.NextPrintTime)
        {
            Say(uid, component, "library-console-printer-busy");
            return;
        }

        if (!TryGetBookbinder(uid, out var binder))
        {
            Say(uid, component, "library-console-no-bookbinder");
            return;
        }

        var cover = LibraryCovers.Sanitize(args.Cover);
        var title = LibraryText.CleanLine(args.Title, LibraryConstants.MaxTitleLength);
        if (!_binder.TryStartPrint(binder.Owner, binder.Comp, 0, args.Actor, out var failure, cover, title))
        {
            Say(uid, component, failure);
            return;
        }

        component.NextPrintTime = _timing.CurTime + component.PrintCooldown;
        component.RefreshAfterCooldown = true;
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     The book binder that was linked to this console's "binder" port with a network configurator.
    /// </summary>
    private bool TryGetBookbinder(EntityUid console, out Entity<BookbinderComponent> binder)
    {
        return TryGetLinked(console, LibraryConstants.BinderSourcePort, out binder);
    }

    /// <summary>
    ///     The book scanner that was linked to this console's "scanner" port with a network configurator.
    /// </summary>
    private bool TryGetScanner(EntityUid console, out Entity<LibraryScannerComponent> scanner)
    {
        return TryGetLinked(console, LibraryConstants.ScannerSourcePort, out scanner);
    }

    private bool TryGetLinked<T>(EntityUid console, string port, out Entity<T> linked) where T : IComponent
    {
        if (TryComp<DeviceLinkSourceComponent>(console, out var source))
        {
            foreach (var sink in _deviceLink.GetLinkedSinks((console, source), port))
            {
                if (TryComp<T>(sink, out var comp))
                {
                    linked = (sink, comp);
                    return true;
                }
            }
        }

        linked = default;
        return false;
    }

    private void OnLinkChanged(EntityUid uid, LibraryConsoleComponent component, NewLinkEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnLinkRemoved(EntityUid uid, LibraryConsoleComponent component, PortDisconnectedEvent args)
    {
        UpdateUi(uid, component);
    }

    #endregion

    #region Upload

    private void OnSetCache(EntityUid uid, LibraryConsoleComponent component, LibrarySetCacheMessage args)
    {
        if (!_power.IsPowered(uid) || !TryGetScanner(uid, out var scanner))
            return;

        _scanner.SetCacheDetails(scanner, args.Title, args.Author);
        UpdateUi(uid, component);
    }

    private void OnUpload(EntityUid uid, LibraryConsoleComponent component, LibraryUploadMessage args)
    {
        if (!TryRequest(uid, component))
            return;

        if (!_archive.Available)
        {
            Say(uid, component, "library-console-archive-severed");
            return;
        }

        if (!_cfg.GetCVar(OmuCVars.LibraryUploadsEnabled))
        {
            Say(uid, component, "library-console-uploads-disabled");
            return;
        }

        if (!TryGetScanner(uid, out var scanner))
        {
            Say(uid, component, "library-console-no-scanner");
            return;
        }

        if (scanner.Comp.Cache is not { } book)
        {
            Say(uid, component, "library-console-no-cache");
            return;
        }

        if (string.IsNullOrWhiteSpace(book.Title))
        {
            Say(uid, component, "library-console-no-title");
            return;
        }

        if (string.IsNullOrWhiteSpace(book.Author))
        {
            Say(uid, component, "library-console-no-author");
            return;
        }

        if (string.IsNullOrWhiteSpace(book.Content))
        {
            Say(uid, component, "library-console-no-content");
            return;
        }

        // Books with the same text can't be uploaded again, whatever title or author they are given.
        if (_archive.TryFindDuplicate(book.Content, out var duplicateId, out var duplicatePending))
        {
            // The id of a book under review is not given out, players can't see it yet.
            _popup.PopupEntity(duplicatePending
                ? Loc.GetString("library-console-already-pending")
                : Loc.GetString("library-console-already-archived", ("id", duplicateId)), uid);
            return;
        }

        if (_archive.PendingCount >= LibraryConstants.MaxPendingBooks)
        {
            Say(uid, component, "library-console-review-full");
            return;
        }

        var category = LibraryConstants.IsUploadCategory(args.Category)
            ? args.Category
            : LibraryConstants.DefaultUploadCategory;

        var user = args.Actor;
        var username = TryComp<ActorComponent>(user, out var actor) ? actor.PlayerSession.Name : string.Empty;

        // Uploads wait for an admin to accept them, players can't see the book until then.
        if (_archive.Add(book, category, username, Name(user), args.Cover, pending: true) is not { } id)
        {
            Say(uid, component, "library-console-upload-error");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} (user {username}) submitted the book #{id} \"{book.Title}\" by {book.Author} ({book.Content.Length} signs) to the library archive for review");

        _chat.SendAdminAlert(Loc.GetString("library-admin-alert-submitted",
            ("user", username),
            ("character", Name(user)),
            ("title", book.Title),
            ("count", _archive.PendingCount)));

        Say(uid, component, "library-console-upload-complete");
    }

    #endregion

    #region Admin review

    private bool IsReviewer(EntityUid actor)
    {
        return _admin.HasAdminFlag(actor, AdminFlags.Admin) || _admin.HasAdminFlag(actor, AdminFlags.Moderator);
    }

    private List<LibraryPendingEntry> BuildPending()
    {
        return _archive.GetPending()
            .Select(b => new LibraryPendingEntry(
                b.Id,
                b.Title,
                b.Author,
                b.Category,
                b.UploaderUser,
                b.UploaderCharacter,
                b.Content.Length))
            .ToList();
    }

    /// <summary>
    ///     Sends the review queue to one user, only if they are an admin. Other players never receive it.
    /// </summary>
    private void SendModeration(EntityUid uid, EntityUid actor)
    {
        if (!IsReviewer(actor))
            return;

        _ui.ServerSendUiMessage(uid, LibraryConsoleUiKey.Key, new LibraryModerationMessage(BuildPending()), actor);
    }

    private void SendModerationToAll(EntityUid uid)
    {
        if (!_ui.IsUiOpen(uid, LibraryConsoleUiKey.Key))
            return;

        foreach (var actor in _ui.GetActors(uid, LibraryConsoleUiKey.Key))
        {
            SendModeration(uid, actor);
        }
    }

    private static LibraryPendingEntry ToAdminEntry(LibraryArchiveEntry b)
    {
        return new LibraryPendingEntry(b.Id, b.Title, b.Author, b.Category, b.UploaderUser, b.UploaderCharacter, b.Content.Length);
    }

    /// <summary>
    ///     Sends the text of a book to the admin that asked for it, so it can be read in its own window.
    /// </summary>
    private void OnReviewView(EntityUid uid, LibraryConsoleComponent component, LibraryReviewViewMessage args)
    {
        if (!IsReviewer(args.Actor) || !_archive.TryGetAny(args.Id, out var entry) || entry.Deleted)
            return;

        _ui.ServerSendUiMessage(uid, LibraryConsoleUiKey.Key,
            new LibraryReviewTextMessage(ToAdminEntry(entry), entry.Content, entry.Pending), args.Actor);
    }

    private void SendAdminArchive(EntityUid uid, EntityUid actor, string filter, int page)
    {
        filter = LibraryText.CleanLine(filter, LibraryConstants.MaxTitleLength);
        var books = _archive.SearchAdmin(filter, ref page, out var pageCount);
        _ui.ServerSendUiMessage(uid, LibraryConsoleUiKey.Key,
            new LibraryAdminArchiveMessage(books.Select(ToAdminEntry).ToList(), filter, page, pageCount), actor);
    }

    private void OnAdminArchiveRequest(EntityUid uid, LibraryConsoleComponent component, LibraryAdminArchiveRequestMessage args)
    {
        if (!IsReviewer(args.Actor))
            return;

        SendAdminArchive(uid, args.Actor, args.Filter, args.Page);
    }

    private void OnAdminDelete(EntityUid uid, LibraryConsoleComponent component, LibraryAdminDeleteMessage args)
    {
        if (!IsReviewer(args.Actor) || !_archive.TryGet(args.Id, out var entry))
            return;

        var title = entry.Title;
        var author = entry.Author;
        var uploader = entry.UploaderUser;

        // Soft delete: the book vanishes for players, but the file stays so it can be restored with libraryrestore.
        if (!_archive.Delete(args.Id, Name(args.Actor)))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} deleted the library book #{args.Id} \"{title}\" by {author}, uploaded by {uploader}, from the archive");

        SendAdminArchive(uid, args.Actor, args.Filter, args.Page);
    }

    private void OnReview(EntityUid uid, LibraryConsoleComponent component, LibraryReviewMessage args)
    {
        if (!IsReviewer(args.Actor) || !_archive.TryGetPending(args.Id, out var entry))
            return;

        var admin = args.Actor;
        var title = entry.Title;
        var author = entry.Author;
        var uploader = entry.UploaderUser;
        var character = entry.UploaderCharacter;

        if (args.Accept)
        {
            if (!_archive.Approve(args.Id))
            {
                Say(uid, component, "library-review-accept-failed");
                return;
            }

            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(admin):player} accepted the library book #{args.Id} \"{title}\" by {author}, uploaded by {uploader} ({character})");
        }
        else
        {
            if (!_archive.Decline(args.Id, Name(admin)))
                return;

            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(admin):player} declined the library book #{args.Id} \"{title}\" by {author}, uploaded by {uploader} ({character}). It was deleted.");
        }

        // The archive change refreshes the consoles, this also updates the queue of every open admin tab.
        SendModerationToAll(uid);
    }

    #endregion

    #region Inventory and checkouts

    /// <summary>
    ///     Librarians and command remove a book's entry from the inventory list. The book itself stays where it
    ///     is. Its loan, if it has one, goes with it. Both are found by the book's entity, not by its title.
    /// </summary>
    private void OnRemoveBook(EntityUid uid, LibraryConsoleComponent component, LibraryRemoveBookMessage args)
    {
        if (!_power.IsPowered(uid) || !_libraryAccess.IsAllowed(uid, args.Actor, true))
            return;

        if (!component.Registered.Remove(args.Id, out var entry))
            return;

        var loans = component.Loans.RemoveAll(loan => loan.Book == entry.Book);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} removed {ToPrettyString(entry.Book)} \"{entry.Title}\" from the library inventory ({loans} loan(s) cleared)");

        UpdateUi(uid, component);
    }

    private void OnRefreshInventory(EntityUid uid, LibraryConsoleComponent component, LibraryRefreshInventoryMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        EvaluateBooks(uid, component);
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     Finds the archive book a physical book is a copy of. Only books of the archive can be part of the
    ///     inventory: printed from it or a guidebook, and at the start of the round the original books of the
    ///     library that are in it. Print jobs and blank books are never accepted.
    /// </summary>
    private bool TryGetArchiveEntry(EntityUid book, out LibraryArchiveEntry entry, bool allowTextMatch = false)
    {
        entry = default!;
        LibraryArchiveEntry found;

        // Books bound by the book binder are never part of the inventory, whatever their text is.
        if (TryComp<LibraryBookComponent>(book, out var binderInfo) && binderInfo.PrintJob)
            return false;

        if (TryComp<LibraryBookComponent>(book, out var info) && info.ArchiveId is { } archiveId
            && _archive.TryGet(archiveId, out found) && !found.Pending)
        {
            entry = found;
            return true;
        }

        if (_room.IsGuidebook(book) && MetaData(book).EntityPrototype is { } proto
            && _archive.TryGetBySeed($"guidebook:{proto.ID}", out found))
        {
            entry = found;
            return true;
        }

        // Only the books that stood in the library when the round started are recognised by their text, and only
        // the original ones. A printed or blank book never is: writing the text of an archive book into it does
        // not make it a book of the archive.
        if (allowTextMatch && !HasComp<LibraryBookComponent>(book)
            && TryComp<PaperComponent>(book, out var paper)
            && _archive.TryFindDuplicate(paper.Content, out var id, out var pending) && !pending
            && _archive.TryGet(id, out found))
        {
            entry = found;
            return true;
        }

        return false;
    }

    private bool IsRegistered(LibraryConsoleComponent component, EntityUid book)
    {
        foreach (var registered in component.Registered.Values)
        {
            if (registered.Book == book)
                return true;
        }

        return false;
    }

    private void Register(LibraryConsoleComponent component, EntityUid book)
    {
        GetBookInfo(book, out var title, out var author);
        var id = component.NextBookId++;
        component.Registered[id] = new LibraryRegisteredBook
        {
            Id = id,
            Book = book,
            Title = title,
            Author = author,
            InRoom = true,
        };
    }

    private void RegisterRoundStartBooks(EntityUid uid, LibraryConsoleComponent component)
    {
        foreach (var book in _room.FindRoomBooks(uid)
                     .OrderBy(b => Name(b), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(b => b.Id))
        {
            if (component.Registered.Count >= LibraryConstants.MaxInventory)
                break;

            if (!IsRegistered(component, book) && TryGetArchiveEntry(book, out _, allowTextMatch: true))
                Register(component, book);
        }

        UpdateUi(uid, component);
    }

    /// <summary>
    ///     Scanning a book adds it to the inventory. Putting it on a shelf or the floor never does.
    /// </summary>
    private void OnRegisterBook(EntityUid uid, LibraryConsoleComponent component, LibraryRegisterBookMessage args)
    {
        if (!_power.IsPowered(uid) || !_libraryAccess.IsAllowed(uid, args.Actor, true))
            return;

        if (!TryGetScanner(uid, out var scanner) || scanner.Comp.BookSlot.Item is not { } book)
        {
            Say(uid, component, "library-console-register-no-book");
            return;
        }

        if (!_room.IsLibraryBook(book))
        {
            Say(uid, component, "library-console-register-not-book");
            return;
        }

        if (IsRegistered(component, book))
        {
            Say(uid, component, "library-console-register-already");
            return;
        }

        if (component.Registered.Count >= LibraryConstants.MaxInventory)
        {
            Say(uid, component, "library-console-register-full");
            return;
        }

        if (!TryGetArchiveEntry(book, out _))
        {
            Say(uid, component, "library-console-register-not-archived");
            return;
        }

        Register(component, book);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} added {ToPrettyString(book)} \"{Name(book)}\" to the library inventory");

        Say(uid, component, "library-console-register-ok");
        EvaluateBooks(uid, component);
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     Compares the inventory with the room. A book that leaves the library room starts a countdown. Carrying it around
    ///     inside the room is fine. If it is not checked out or returned before the countdown ends, it is marked missing.
    ///     Returning it removes the mark. Nothing is ever deleted automatically.
    /// </summary>
    private void EvaluateBooks(EntityUid uid, LibraryConsoleComponent component)
    {
        if (component.Registered.Count == 0)
            return;

        var now = _timing.CurTime;
        var delay = TimeSpan.FromSeconds(LibraryConstants.MissingDelaySeconds);
        var inRoom = new HashSet<EntityUid>(_room.FindRoomBooks(uid, true));
        var changed = false;
        var counting = false;

        foreach (var entry in component.Registered.Values)
        {
            var exists = !TerminatingOrDeleted(entry.Book);
            var here = exists && inRoom.Contains(entry.Book);
            var lent = component.Loans.Any(loan => loan.Book == entry.Book);

            if (entry.InRoom != here)
            {
                entry.InRoom = here;
                changed = true;
            }

            if (lent || here)
            {
                // Checked out books are accounted for. A book in the library room is not missing, whether it lies
                // on a shelf or somebody in the room carries it. A book that was already marked missing keeps
                // the mark, only a librarian clears it, on the console.
                if (entry.AbsentSince != null)
                {
                    entry.AbsentSince = null;
                    changed = true;
                }

                continue;
            }

            entry.AbsentSince ??= now;
            counting = true;

            if (!entry.Missing && now - entry.AbsentSince.Value >= delay)
            {
                entry.Missing = true;
                changed = true;
            }
        }

        // The countdowns on an open console are shown, so they are sent along.
        if ((changed || counting) && _ui.IsUiOpen(uid, LibraryConsoleUiKey.Key))
            UpdateUi(uid, component);
    }

    /// <summary>
    ///     The "missing" mark only goes away when a librarian clears it here, so leaving and coming back to
    ///     the library does not reset it.
    /// </summary>
    private void OnClearMissing(EntityUid uid, LibraryConsoleComponent component, LibraryClearMissingMessage args)
    {
        if (!_power.IsPowered(uid) || !_libraryAccess.IsAllowed(uid, args.Actor, true))
            return;

        if (!component.Registered.TryGetValue(args.Id, out var entry) || !entry.Missing)
            return;

        entry.Missing = false;
        entry.AbsentSince = null;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} cleared the missing mark of {ToPrettyString(entry.Book)} \"{entry.Title}\"");

        // Still out of the library: the countdown starts again at the next check.
        EvaluateBooks(uid, component);
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     Books that are checked out past their return date, with the borrower. Loans without a return date never
    ///     show up here. For the universal pinpointer.
    /// </summary>
    public List<(EntityUid Book, string Borrower)> GetOverdueBooks()
    {
        var now = _timing.CurTime;
        var result = new List<(EntityUid, string)>();
        var query = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (query.MoveNext(out _, out var component))
        {
            foreach (var loan in component.Loans)
            {
                if (!loan.Indefinite && now >= loan.DueTime && !TerminatingOrDeleted(loan.Book))
                    result.Add((loan.Book, loan.Borrower));
            }
        }

        return result;
    }

    /// <summary>
    ///     Every registered book that exists and is marked missing, over all consoles. For the universal pinpointer.
    /// </summary>
    public List<EntityUid> GetMissingBooks()
    {
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (query.MoveNext(out _, out var component))
        {
            foreach (var entry in component.Registered.Values)
            {
                if (entry.Missing && !TerminatingOrDeleted(entry.Book) && !result.Contains(entry.Book))
                    result.Add(entry.Book);
            }
        }

        return result;
    }

    private void OnCheckout(EntityUid uid, LibraryConsoleComponent component, LibraryCheckoutMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        // Only books of the inventory that are in the library or on somebody in it can be checked out.
        if (!component.Registered.TryGetValue(args.Id, out var entry) || TerminatingOrDeleted(entry.Book))
            return;

        var book = entry.Book;

        if (!entry.InRoom)
        {
            Say(uid, component, "library-console-not-here");
            return;
        }

        GetBookInfo(book, out var title, out var author);

        // Books are checked out to the name on the ID that was put into the console.
        if (!component.IdSlot.HasItem)
        {
            Say(uid, component, "library-console-no-id");
            return;
        }

        if (!TryGetBorrowerName(component, out var borrower))
        {
            Say(uid, component, "library-console-id-no-name");
            return;
        }

        // One loan per book, the loan belongs to this exact book.
        if (component.Loans.Any(loan => loan.Book == book))
        {
            Say(uid, component, "library-console-already-lent");
            return;
        }

        // 0 minutes means no return date.
        var indefinite = args.Minutes == 0;
        var minutes = Math.Clamp(args.Minutes, LibraryConstants.MinLoanMinutes, LibraryConstants.MaxLoanMinutes);

        component.Loans.Add(new LibraryLoan
        {
            Id = component.NextLoanId++,
            Book = book,
            Title = title,
            Author = author,
            Borrower = borrower,
            DueTime = indefinite ? TimeSpan.Zero : _timing.CurTime + TimeSpan.FromMinutes(minutes),
            Indefinite = indefinite,
            BorrowerCard = component.IdSlot.Item is { } idItem && _idCard.TryFindIdCard(idItem, out var borrowerCard)
                ? borrowerCard.Owner
                : null,
        });

        // Checked out in time: no longer counting down.
        entry.AbsentSince = null;
        entry.Missing = false;

        UpdateUi(uid, component);
    }

    private void OnCheckin(EntityUid uid, LibraryConsoleComponent component, LibraryCheckinMessage args)
    {
        if (!_power.IsPowered(uid))
            return;

        if (component.Loans.RemoveAll(loan => loan.Id == args.LoanId) > 0)
            UpdateUi(uid, component);
    }

    private void GetBookInfo(EntityUid book, out string title, out string author)
    {
        title = LibraryText.CleanLine(Name(book), LibraryConstants.MaxTitleLength);
        author = TryComp<LibraryBookComponent>(book, out var info)
            ? LibraryText.CleanLine(info.Author, LibraryConstants.MaxAuthorLength)
            : string.Empty;
    }

    #endregion

    /// <summary>
    ///     Checks power and the anti-spam cooldown for requests that hit the archive (the tgstation
    ///     <c>prevent_db_spam</c>).
    /// </summary>
    private bool TryRequest(EntityUid uid, LibraryConsoleComponent component)
    {
        if (!_power.IsPowered(uid))
            return false;

        if (_timing.CurTime < component.NextRequestTime)
        {
            Say(uid, component, "library-console-cooldown");
            return false;
        }

        component.NextRequestTime = _timing.CurTime + component.RequestCooldown;
        return true;
    }

    private void Say(EntityUid uid, LibraryConsoleComponent component, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), uid);
    }

    private void UpdateUi(EntityUid uid, LibraryConsoleComponent component)
    {
        var now = _timing.CurTime;

        var results = _archive.Search(
            component.SearchTitle,
            component.SearchAuthor,
            component.SearchCategory,
            component.SearchId,
            ref component.SearchPage,
            out var pageCount);

        var inventory = new List<LibraryInventoryEntry>();
        foreach (var book in component.Registered.Values
                     .OrderByDescending(b => b.Missing)
                     .ThenBy(b => b.Title, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(b => b.Id))
        {
            var status = LibraryBookStatus.Present;
            var secondsLeft = 0;
            if (component.Loans.Any(loan => loan.Book == book.Book))
            {
                status = LibraryBookStatus.CheckedOut;
            }
            else if (book.Missing)
            {
                status = LibraryBookStatus.Missing;
            }
            else if (book.AbsentSince is { } since)
            {
                status = LibraryBookStatus.Taken;
                var left = TimeSpan.FromSeconds(LibraryConstants.MissingDelaySeconds) - (now - since);
                secondsLeft = (int) Math.Max(Math.Ceiling(left.TotalSeconds), 0);
            }

            inventory.Add(new LibraryInventoryEntry(book.Id, book.Title, book.Author, status, secondsLeft, book.InRoom));
        }

        var loans = new List<LibraryLoanEntry>(component.Loans.Count);
        foreach (var loan in component.Loans)
        {
            var due = loan.Indefinite ? 0f : (float) Math.Max(Math.Round((loan.DueTime - now).TotalMinutes, 1), 0);
            loans.Add(new LibraryLoanEntry(loan.Id, loan.Title, loan.Author, loan.Borrower, due, !loan.Indefinite && due <= 0, loan.Indefinite));
        }

        var hasScanner = TryGetScanner(uid, out var scanner);
        var cache = hasScanner ? scanner.Comp.Cache : null;

        var hasBinder = TryGetBookbinder(uid, out var binder);
        var binderReady = hasBinder && _binder.CanPrint(binder.Owner, binder.Comp);

        var state = new LibraryConsoleBoundUserInterfaceState(
            _archive.Available,
            _cfg.GetCVar(OmuCVars.LibraryUploadsEnabled),
            now >= component.NextPrintTime && binderReady,
            component.SearchTitle,
            component.SearchAuthor,
            component.SearchCategory,
            component.SearchId,
            component.SearchPage,
            pageCount,
            results,
            inventory,
            loans,
            hasScanner,
            hasScanner && scanner.Comp.BookSlot.Item != null,
            cache != null,
            cache?.Title ?? string.Empty,
            cache?.Author ?? string.Empty,
            cache?.Content.Length ?? 0,
            component.IdSlot.HasItem,
            TryGetBorrowerName(component, out var borrowerName) ? borrowerName : string.Empty,
            hasBinder,
            hasBinder ? _binder.GetSheets(binder.Owner) : 0,
            hasBinder ? binder.Comp.PrintSheets : 0,
            GetRequestEntries(component, null));

        _ui.SetUiState(uid, LibraryConsoleUiKey.Key, state);
    }
}
