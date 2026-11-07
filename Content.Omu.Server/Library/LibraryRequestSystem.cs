// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Library;

/// <summary>
///     The book request console: people look through the archive or ask for a blank book, and the request goes
///     to the library console it is linked to. The librarian accepts or denies it there.
/// </summary>
public sealed class LibraryRequestSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;
    [Dependency] private readonly LibraryConsoleSystem _consoles = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LibraryRequestConsoleComponent, NewLinkEvent>(OnLinkChanged);
        SubscribeLocalEvent<LibraryRequestConsoleComponent, PortDisconnectedEvent>(OnLinkRemoved);
        SubscribeLocalEvent<LibraryRequestConsoleComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<LibraryRequestConsoleComponent, LibraryRequestSearchMessage>(OnSearch);
        SubscribeLocalEvent<LibraryRequestConsoleComponent, LibraryRequestArchiveMessage>(OnRequestArchive);
        SubscribeLocalEvent<LibraryRequestConsoleComponent, LibraryRequestBlankMessage>(OnRequestBlank);

        _archive.ArchiveChanged += OnArchiveChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _archive.ArchiveChanged -= OnArchiveChanged;
    }

    private void OnArchiveChanged()
    {
        var query = EntityQueryEnumerator<LibraryRequestConsoleComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            UpdateUi(uid, component);
        }
    }

    private void OnLinkChanged(EntityUid uid, LibraryRequestConsoleComponent component, NewLinkEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnLinkRemoved(EntityUid uid, LibraryRequestConsoleComponent component, PortDisconnectedEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnUiOpened(EntityUid uid, LibraryRequestConsoleComponent component, AfterActivatableUIOpenEvent args)
    {
        UpdateUi(uid, component);
    }

    /// <summary>
    ///     The library console that was linked to this console's "requests" port with a network configurator.
    /// </summary>
    private bool TryGetLibraryConsole(EntityUid uid, out Entity<LibraryConsoleComponent> console)
    {
        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
        {
            foreach (var sink in _deviceLink.GetLinkedSinks((uid, source), LibraryConstants.RequestSourcePort))
            {
                if (TryComp<LibraryConsoleComponent>(sink, out var comp))
                {
                    console = (sink, comp);
                    return true;
                }
            }
        }

        console = default;
        return false;
    }

    private void OnSearch(EntityUid uid, LibraryRequestConsoleComponent component, LibraryRequestSearchMessage args)
    {
        if (!_power.IsPowered(uid) || _timing.CurTime < component.NextSearchTime)
            return;

        component.NextSearchTime = _timing.CurTime + component.SearchCooldown;
        component.SearchTitle = LibraryText.CleanLine(args.Title, LibraryConstants.MaxTitleLength);
        component.SearchAuthor = LibraryText.CleanLine(args.Author, LibraryConstants.MaxAuthorLength);
        component.SearchPage = Math.Max(args.Page, 0);

        UpdateUi(uid, component);
    }

    private void OnRequestArchive(EntityUid uid, LibraryRequestConsoleComponent component, LibraryRequestArchiveMessage args)
    {
        if (!TryStartRequest(uid, component, out var console))
            return;

        // Only books that are in the archive (not under review, not deleted) can be asked for.
        if (!_archive.TryGet(args.Id, out var entry) || entry.Pending)
        {
            Say(uid, "library-request-unknown-book");
            return;
        }

        var request = new LibraryBookRequest
        {
            Kind = LibraryRequestKind.Archive,
            ArchiveId = entry.Id,
            Title = LibraryText.CleanLine(entry.Title, LibraryConstants.MaxTitleLength),
            Author = LibraryText.CleanLine(entry.Author, LibraryConstants.MaxAuthorLength),
            Cover = LibraryCovers.Default(entry.Id),
            Requester = RequesterName(args.Actor),
            RequesterCard = FindCard(args.Actor),
            Source = uid,
        };

        Send(uid, component, console, request);
    }

    private void OnRequestBlank(EntityUid uid, LibraryRequestConsoleComponent component, LibraryRequestBlankMessage args)
    {
        if (!TryStartRequest(uid, component, out var console))
            return;

        var title = LibraryText.CleanLine(args.Title, LibraryConstants.MaxTitleLength);
        if (title.Length == 0)
        {
            Say(uid, "library-request-no-title");
            return;
        }

        var request = new LibraryBookRequest
        {
            Kind = LibraryRequestKind.Blank,
            Title = title,
            Cover = LibraryCovers.Sanitize(args.Cover),
            Requester = RequesterName(args.Actor),
            RequesterCard = FindCard(args.Actor),
            Source = uid,
        };

        Send(uid, component, console, request);
    }

    private bool TryStartRequest(
        EntityUid uid,
        LibraryRequestConsoleComponent component,
        out Entity<LibraryConsoleComponent> console)
    {
        console = default;

        if (!_power.IsPowered(uid))
            return false;

        if (_timing.CurTime < component.NextRequestTime)
        {
            Say(uid, "library-request-cooldown");
            return false;
        }

        if (!TryGetLibraryConsole(uid, out console))
        {
            Say(uid, "library-request-no-console");
            return false;
        }

        return true;
    }

    private void Send(
        EntityUid uid,
        LibraryRequestConsoleComponent component,
        Entity<LibraryConsoleComponent> console,
        LibraryBookRequest request)
    {
        if (!_consoles.TryAddRequest(console.Owner, console.Comp, request, out var failure))
        {
            Say(uid, failure);
            return;
        }

        component.NextRequestTime = _timing.CurTime + component.RequestCooldown;
        Say(uid, "library-request-sent");
        UpdateUi(uid, component);
    }

    private EntityUid? FindCard(EntityUid actor)
    {
        return _idCard.TryFindIdCard(actor, out var card) ? card.Owner : null;
    }

    private string RequesterName(EntityUid actor)
    {
        return LibraryText.CleanLine(Name(actor), LibraryConstants.MaxBorrowerLength);
    }

    private void Say(EntityUid uid, string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), uid);
    }

    private void UpdateUi(EntityUid uid, LibraryRequestConsoleComponent component)
    {
        var linked = TryGetLibraryConsole(uid, out var console);

        var results = _archive.Search(
            component.SearchTitle,
            component.SearchAuthor,
            LibraryConstants.AnyCategory,
            null,
            ref component.SearchPage,
            out var pageCount);

        _ui.SetUiState(uid, LibraryRequestConsoleUiKey.Key, new LibraryRequestConsoleBoundUserInterfaceState(
            linked,
            _archive.Available,
            component.SearchTitle,
            component.SearchAuthor,
            component.SearchPage,
            pageCount,
            results));
    }
}
