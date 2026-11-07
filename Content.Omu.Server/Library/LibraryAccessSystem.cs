// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Access.Systems;
using Content.Shared.Popups;

namespace Content.Omu.Server.Library;

/// <summary>
///     The library console, scanner and binder can only be used by people with a librarian or command ID.
///     The allowed access is the <c>AccessReader</c> on each machine's prototype.
/// </summary>
public sealed class LibraryAccessSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>
    ///     Whether <paramref name="user"/> carries an ID that is allowed to use <paramref name="machine"/>.
    ///     Tells the user when they are not allowed, unless <paramref name="silent"/>.
    /// </summary>
    public bool IsAllowed(EntityUid machine, EntityUid user, bool silent = false)
    {
        if (_access.IsAllowed(user, machine))
            return true;

        if (!silent)
            _popup.PopupEntity(Loc.GetString("library-access-denied"), machine, user);

        return false;
    }
}
