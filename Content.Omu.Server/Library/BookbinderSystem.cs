// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Library;
using Content.Server.Materials;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Logs;
using Content.Shared.Lathe;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Library;

/// <summary>
///     The book binder: swipe an ID to be credited as the author, feed it a written sheet of paper and out comes
///     a book. It also prints books from the library archive when a library console asks it to.
///     Every book costs sheets of paper, kept in the binder's material storage.
///     Ported from tgstation's /obj/machinery/bookbinder.
/// </summary>
public sealed class BookbinderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly LibraryAccessSystem _libraryAccess = default!;
    [Dependency] private readonly LibraryArchiveSystem _archive = default!;
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly RandomArchiveBookSystem _bookFill = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materials = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> BookTag = "Book";
    private static readonly ProtoId<MaterialPrototype> PaperMaterial = "Paper";

    private int? _sheetVolume;

    /// <summary>
    ///     How much material one sheet of paper is worth.
    /// </summary>
    private int SheetVolume => _sheetVolume ??= _materials.GetSheetVolume(_proto.Index(PaperMaterial));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookbinderComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BookbinderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BookbinderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BookbinderComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BookbinderComponent, MaterialAmountChangedEvent>(OnSheetsChanged);
        // Before the material storage, so paper pages and IDs are not mistaken for sheets of material.
        SubscribeLocalEvent<BookbinderComponent, InteractUsingEvent>(OnInteractUsing,
            before: [typeof(MaterialStorageSystem)]);
    }

    private void OnInit(EntityUid uid, BookbinderComponent component, ComponentInit args)
    {
        _container.EnsureContainer<ContainerSlot>(uid, BookbinderComponent.PaperContainerId);
    }

    private void OnMapInit(EntityUid uid, BookbinderComponent component, MapInitEvent args)
    {
        // Machines that were built by hand start without paper, the ones that spawned come with a little.
        if (component.StartingSheets > 0)
            _materials.TryChangeMaterialAmount(uid, PaperMaterial.Id, component.StartingSheets * SheetVolume);
    }

    private void OnExamined(EntityUid uid, BookbinderComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var max = TryComp<MaterialStorageComponent>(uid, out var storage) && storage.StorageLimit is { } limit
            ? limit / SheetVolume
            : 0;

        args.PushMarkup(Loc.GetString("bookbinder-examine-sheets",
            ("sheets", GetSheets(uid)),
            ("max", max),
            ("bind", component.BindSheets),
            ("print", component.PrintSheets)));
    }

    private void OnGetVerbs(EntityUid uid, BookbinderComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("bookbinder-eject-verb"),
            Disabled = GetSheets(uid) == 0,
            Act = () => EjectSheets(uid, user),
        });
    }

    /// <summary>
    ///     Takes all the sheets of paper out of the binder.
    /// </summary>
    private void EjectSheets(EntityUid uid, EntityUid user)
    {
        if (!_libraryAccess.IsAllowed(uid, user))
            return;

        var sheets = GetSheets(uid);
        if (sheets == 0)
            return;

        _materialStorage.EjectMaterial(uid, PaperMaterial.Id, coordinates: Transform(user).Coordinates);
        _popup.PopupEntity(Loc.GetString("bookbinder-ejected", ("sheets", sheets)), uid, user);
    }

    private void OnSheetsChanged(EntityUid uid, BookbinderComponent component, ref MaterialAmountChangedEvent args)
    {
        RefreshConsoles();
    }

    /// <summary>
    ///     Whole sheets of paper inside the binder.
    /// </summary>
    public int GetSheets(EntityUid uid)
    {
        return _materials.GetMaterialAmount(uid, PaperMaterial.Id) / SheetVolume;
    }

    private bool TryUseSheets(EntityUid uid, int sheets)
    {
        return _materials.TryChangeMaterialAmount(uid, PaperMaterial.Id, -sheets * SheetVolume);
    }

    public bool IsBusy(BookbinderComponent component)
    {
        return component.BindingEnd != null || component.PrintEnd != null;
    }

    /// <summary>
    ///     Whether the binder could print an archive book right now.
    /// </summary>
    public bool CanPrint(EntityUid uid, BookbinderComponent component)
    {
        return _power.IsPowered(uid) && !IsBusy(component) && GetSheets(uid) >= component.PrintSheets;
    }

    private void OnInteractUsing(EntityUid uid, BookbinderComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Putting paper in is for librarians, other things (tools and so on) are not our business.
        if (HasComp<MaterialComponent>(args.Used) && !HasComp<PaperComponent>(args.Used))
        {
            if (!_libraryAccess.IsAllowed(uid, args.User))
                args.Handled = true;

            // Otherwise the material storage takes the sheets.
            return;
        }

        // Scanning an ID credits the person as the author of the next book.
        if (_idCard.TryFindIdCard(args.Used, out var card))
        {
            args.Handled = true;

            if (!_libraryAccess.IsAllowed(uid, args.User))
                return;

            var name = LibraryText.CleanLine(card.Comp.FullName, LibraryConstants.MaxAuthorLength);
            if (string.IsNullOrEmpty(name))
            {
                _popup.PopupEntity(Loc.GetString("bookbinder-id-no-name"), uid, args.User);
                return;
            }

            component.ScannedAuthor = name;
            _audio.PlayPvs(component.ScanIdSound, uid);
            _popup.PopupEntity(Loc.GetString("bookbinder-id-scanned", ("name", name)), uid, args.User);
            return;
        }

        // Books are paper too, but they are not something that can be bound again.
        if (!HasComp<PaperComponent>(args.Used) ||
            HasComp<LibraryBookComponent>(args.Used) ||
            _tag.HasTag(args.Used, BookTag))
        {
            return;
        }

        args.Handled = true;

        if (!_libraryAccess.IsAllowed(uid, args.User))
            return;

        if (!_power.IsPowered(uid))
        {
            _popup.PopupEntity(Loc.GetString("bookbinder-no-power"), uid, args.User);
            return;
        }

        if (IsBusy(component))
        {
            _popup.PopupEntity(Loc.GetString("bookbinder-busy"), uid, args.User);
            return;
        }

        if (string.IsNullOrWhiteSpace(Comp<PaperComponent>(args.Used).Content))
        {
            _popup.PopupEntity(Loc.GetString("bookbinder-blank-paper"), uid, args.User);
            return;
        }

        if (GetSheets(uid) < component.BindSheets)
        {
            _popup.PopupEntity(Loc.GetString("bookbinder-need-sheets", ("sheets", component.BindSheets)), uid, args.User);
            return;
        }

        // Without an ID the first attempt only warns the user, trying again binds the book anonymously.
        if (component.ScannedAuthor == null)
        {
            component.ScannedAuthor = Loc.GetString("bookbinder-unknown-author");
            _popup.PopupEntity(Loc.GetString("bookbinder-no-id"), uid, args.User);
            return;
        }

        var container = _container.EnsureContainer<ContainerSlot>(uid, BookbinderComponent.PaperContainerId);
        if (!_container.Insert(args.Used, container))
            return;

        // The paper that was written on is bound together with the sheets from the binder's stock.
        TryUseSheets(uid, component.BindSheets);

        component.BindingEnd = _timing.CurTime + component.BindTime;
        SetRunning(uid, true);
        _audio.PlayPvs(component.BindSound, uid);
        _popup.PopupEntity(Loc.GetString("bookbinder-loaded"), uid, args.User);
        _popup.PopupEntity(Loc.GetString("bookbinder-warming-up"), uid);
        RefreshConsoles();
    }

    /// <summary>
    ///     Starts printing an archive book, asked for by a library console. The paper is used up right away.
    ///     <paramref name="failure"/> is a locale id that says why the job could not be started.
    /// </summary>
    public bool TryStartPrint(
        EntityUid uid,
        BookbinderComponent component,
        int archiveId,
        EntityUid user,
        out string failure,
        LibraryCover? blankCover = null,
        string blankTitle = "")
    {
        failure = string.Empty;

        if (!_power.IsPowered(uid))
        {
            failure = "bookbinder-no-power";
            return false;
        }

        if (IsBusy(component))
        {
            failure = "bookbinder-busy";
            return false;
        }

        if (GetSheets(uid) < component.PrintSheets || !TryUseSheets(uid, component.PrintSheets))
        {
            failure = "library-console-binder-no-paper";
            return false;
        }

        component.PrintEnd = _timing.CurTime + component.PrintTime;
        SetRunning(uid, true);
        component.PrintId = archiveId;
        component.PrintUser = user;
        component.PrintBlankCover = blankCover;
        component.PrintBlankTitle = blankTitle;

        _audio.PlayPvs(component.PrintSound, uid);
        _popup.PopupEntity(Loc.GetString("bookbinder-warming-up"), uid);
        RefreshConsoles();
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BookbinderComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            // Jobs are paused while the machine has no power.
            if (!_power.IsPowered(uid))
                continue;

            if (component.BindingEnd is { } bindEnd && now >= bindEnd)
                FinishBinding(uid, component);

            if (component.PrintEnd is { } printEnd && now >= printEnd)
                FinishPrint(uid, component);
        }
    }

    /// <summary>Plays the uniform printer's building animation while the binder works.</summary>
    private void SetRunning(EntityUid uid, bool running)
    {
        _appearance.SetData(uid, LatheVisuals.IsRunning, running);
    }

    private void FinishBinding(EntityUid uid, BookbinderComponent component)
    {
        component.BindingEnd = null;
        SetRunning(uid, false);
        var author = component.ScannedAuthor ?? string.Empty;
        component.ScannedAuthor = null;

        var container = _container.EnsureContainer<ContainerSlot>(uid, BookbinderComponent.PaperContainerId);
        if (container.ContainedEntity is not { } paperUid)
        {
            RefreshConsoles();
            return;
        }

        var content = TryComp<PaperComponent>(paperUid, out var paper) ? paper.Content : string.Empty;
        QueueDel(paperUid);

        var book = Spawn(component.BookPrototype, Transform(uid).Coordinates);
        _meta.SetEntityName(book, Loc.GetString("bookbinder-print-job", ("number", _random.Next(100, 1000))));

        var info = EnsureComp<LibraryBookComponent>(book);
        info.Author = LibraryText.CleanLine(author, LibraryConstants.MaxAuthorLength);
        info.PrintJob = true;
        Dirty(book, info);

        _paper.SetContent(book, content);

        _popup.PopupEntity(Loc.GetString("bookbinder-finished"), uid);
        RefreshConsoles();
    }

    private void FinishPrint(EntityUid uid, BookbinderComponent component)
    {
        component.PrintEnd = null;
        SetRunning(uid, false);
        var id = component.PrintId;
        var user = component.PrintUser;
        component.PrintUser = null;

        // An empty book: no archive entry, no text. It only gets into the archive through scanning and an upload
        // that an admin approves.
        if (component.PrintBlankCover is { } blankCover)
        {
            component.PrintBlankCover = null;
            var blankTitle = component.PrintBlankTitle;
            component.PrintBlankTitle = string.Empty;

            var blank = Spawn(component.BookPrototype, Transform(uid).Coordinates);
            _meta.SetEntityName(blank, blankTitle.Length > 0 ? blankTitle : Loc.GetString("library-blank-book-name"));
            EnsureComp<LibraryBookComponent>(blank);
            _bookFill.ApplyCover(blank, blankCover);

            _popup.PopupEntity(Loc.GetString("bookbinder-finished"), uid);
            _adminLog.Add(LogType.Action, LogImpact.Low,
                $"{ToPrettyString(user):player} printed a blank book from {ToPrettyString(uid)}");
            RefreshConsoles();
            return;
        }

        // The book may have been deleted by a moderator while it was printing, the paper is given back.
        if (!_archive.TryGet(id, out var entry))
        {
            _materials.TryChangeMaterialAmount(uid, PaperMaterial.Id, component.PrintSheets * SheetVolume);
            _popup.PopupEntity(Loc.GetString("library-console-print-error"), uid);
            RefreshConsoles();
            return;
        }

        // Guidebooks are real guidebook items, not paper books with text in them.
        if (entry.SpawnPrototype != null && _proto.HasIndex<EntityPrototype>(entry.SpawnPrototype))
        {
            Spawn(entry.SpawnPrototype, Transform(uid).Coordinates);
        }
        else
        {
            var book = Spawn(component.BookPrototype, Transform(uid).Coordinates);
            _bookFill.Fill(book, entry);
        }

        _popup.PopupEntity(Loc.GetString("bookbinder-finished"), uid);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} printed library book #{id} \"{entry.Title}\" by {entry.Author} from {ToPrettyString(uid)}");
        RefreshConsoles();
    }

    private void RefreshConsoles()
    {
        var consoles = EntityQueryEnumerator<LibraryConsoleComponent>();
        while (consoles.MoveNext(out var uid, out _))
        {
            RaiseLocalEvent(uid, new LibraryConsoleRefreshEvent());
        }
    }
}
