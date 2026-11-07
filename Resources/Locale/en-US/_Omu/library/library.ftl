# SPDX-License-Identifier: AGPL-3.0-or-later

library-book-examine-author = Written by [bold]{$author}[/bold].
library-printed-book-name = {$title}

# Scanner (machine feedback)
library-scanner-book-slot = Book
library-scanner-already-cached = This book is already in my internal cache.
library-scanner-not-a-book = That is not something I can scan.
library-scanner-unique = The pages of this book can not be scanned.
library-scanner-blank = This book is blank, there is nothing to scan.

# Scanner UI
library-scanner-ui-title = Book scanner
library-scanner-ui-scan = Scan
library-scanner-ui-clear = Clear cache
library-scanner-ui-eject = Eject book
library-scanner-ui-has-book = A book is loaded.
library-scanner-ui-no-book = Insert a book to scan it.
library-scanner-ui-cached = Cached: "{$title}" by {$author}
library-scanner-ui-no-cache = The cache is empty.

# Console (machine feedback)
library-console-archive-severed = Connection to the archive has been severed.
library-console-cooldown = Archive cables refreshing. Please wait a moment.
library-console-printer-busy = Printer currently unavailable, please wait a moment.
library-console-print-error = PRINTER ERROR! Failed to print document (0x0000000F).
library-console-printed = The printer hums as it produces a completely bound book. How did it do that?
library-console-uploads-disabled = Uploads to the archive are disabled.
library-console-no-scanner = No linked scanner detected. Link one with a network configurator.
library-console-no-cache = No cached book found. Aborting upload.
library-console-no-title = No title detected. Aborting.
library-console-no-author = No author detected. Aborting.
library-console-no-content = No content detected. Aborting.
library-console-already-archived = A book with the same text is already in the archive (#{$id}).
library-console-upload-error = Database error encountered uploading to the archive.
library-console-upload-complete = Upload sent to Central Command for review. It will be available once it is accepted.
library-console-already-pending = A book with the same text is already waiting for review.
library-console-review-full = Too many books are waiting for review. Please try again later.
library-review-accept-failed = The book could not be added to the archive.
library-admin-alert-submitted = Library: {$user} ({$character}) uploaded "{$title}" for review. {$count} waiting, use the Review tab of a library console.

# Review (admin tab and notice)
library-ui-review-notice = If the book you want/written is not available it may be under review or have been declined by centcomm, please be patient.
library-ui-tab-review = Review (Admin)
library-ui-review-empty = No books are waiting for review.
library-ui-review-title = #{$id} "{$title}" by {$author}
library-ui-review-uploader = Uploaded by user {$user} as {$character} - {$category}, {$length} signs
library-ui-review-open = Review
library-ui-review-window-title = Reviewing book #{$id}
library-ui-confirm = Confirm?
library-ui-remove-book = Remove entry
library-console-already-lent = That book is already checked out.
library-ui-admin-delete = Delete from archive
library-ui-admin-mode-pending = Pending review
library-ui-admin-mode-archive = Archive
library-ui-admin-archive-empty = No archived books match.
library-ui-review-accept = Accept
library-ui-review-decline = Decline and delete
library-console-unknown-borrower = N/A

# Console UI
library-console-ui-title = Book inventory management
library-ui-status-online = Archive connected
library-ui-status-offline = Archive unavailable
library-ui-archive-offline = Connection to the archive has been severed.
library-ui-tab-archive = Archive
library-ui-tab-inventory = Inventory
library-ui-tab-checkouts = Checkouts
library-ui-tab-upload = Upload
library-ui-title = Title
library-ui-author = Author
library-ui-id = ID
library-ui-search = Search
library-ui-clear = Clear
library-ui-print = Print
library-ui-refresh = Refresh
library-ui-apply = Apply
library-ui-upload = Send to CentComm
library-ui-borrower = Borrower name
library-ui-minutes = Minutes (0 = no limit):
library-ui-checkout = Check out
library-ui-checkin = Check in
library-ui-category-any = Any
library-ui-page = Page {$page} of {$pages}
library-ui-no-results = No books found.
library-ui-no-inventory = No books found in this room.
library-ui-no-inventory-match = No books match your search.
library-ui-inventory-search = Search title or author
library-ui-no-loans = Nothing is checked out.
library-ui-overdue = OVERDUE
library-ui-due-in = Due in {$minutes} min
library-ui-unknown = Unknown
library-ui-no-scanner = No scanner is linked. Link one with a network configurator.
library-ui-scanner-has-book = The scanner holds a book.
library-ui-scanner-empty = The scanner is empty.
library-ui-no-cache = Nothing has been scanned yet.
library-ui-cache-info = Scanned book, {$length} signs.

# Book binder
bookbinder-id-scanned = ID scanned, you will be credited as {$name}.
bookbinder-id-no-name = This ID has no name on it.
bookbinder-no-power = The book binder has no power.
bookbinder-busy = The book binder is busy. Please wait for the previous job to finish.
bookbinder-blank-paper = That paper is blank, there is nothing to bind.
bookbinder-no-id = No ID detected. Scan your ID if you would like to be credited for this book, otherwise insert the paper again.
bookbinder-unknown-author = unknown author
bookbinder-loaded = You load some paper into the book binder.
bookbinder-warming-up = The book binder begins to hum as it warms up its printing drums.
bookbinder-finished = The book binder whirs as it prints and binds a new book.
bookbinder-print-job = Print Job #{$number}

# Admin commands
cmd-librarylist-desc = Lists books in the library archive.
cmd-librarylist-help = Usage: librarylist [search text]
cmd-librarylist-none = No books found.
cmd-librarylist-entry = #{$id} "{$title}" by {$author} [{$category}] uploaded by {$uploader}{$deleted ->
    [true] {" "}(DELETED)
   *[false] {""}
}{$pending ->
    [true] {" "}(PENDING REVIEW)
   *[false] {""}
}
cmd-librarydelete-desc = Hides a book in the library archive.
cmd-librarydelete-help = Usage: librarydelete <id>
cmd-librarydelete-success = Deleted library book #{$id}.
cmd-librarydelete-failure = Could not delete library book #{$id}.
cmd-libraryrestore-desc = Restores a deleted book in the library archive.
cmd-libraryrestore-help = Usage: libraryrestore <id>
cmd-libraryrestore-success = Restored library book #{$id}.
cmd-libraryrestore-failure = Could not restore library book #{$id}.

cmd-libraryshelves-desc = Lists every bookshelf with its position and how many books it holds.
cmd-libraryshelves-help = Usage: libraryshelves (use "tpto <uid>" to go to a shelf)
cmd-libraryshelves-entry = {$uid} on map {$map} at ({$x}, {$y}): {$books} books
cmd-libraryshelves-total = {$shelves} bookshelves holding {$books} books in total.
cmd-librarystockshelves-desc = Puts the round start library books on the station's bookshelves again.
cmd-librarystockshelves-help = Usage: librarystockshelves
cmd-librarystockshelves-none = No station bookshelves were found.
cmd-librarystockshelves-success = Put {$books} books on {$shelves} bookshelves of {$stations} station(s).

library-access-denied = Access denied. Only librarians and command staff may use this.
library-console-no-id = Insert the borrower's ID into the console first.
library-console-id-no-name = The inserted ID has no name on it.
library-ui-no-id = No ID inserted. Put the borrower's ID into the console to check out a book.
library-ui-id-inserted = Borrower: {$name}
library-ui-id-no-name = The inserted ID has no name on it.
library-ui-eject-id = Eject ID
library-ui-upload-warning = All books are sent to CentComm for review before being added to the archive. Books that are not up to Nanotrasen standarts can get you fire or banned from ever working in any Nanotrasen subsidiaries
library-ui-cover-title = Book cover
library-ui-cover-style = Cover:
library-ui-cover-decor = Decoration:
library-ui-cover-icon = Icon:
library-ui-cover-none = None
library-ui-cover-uncolored = Uncolored
library-console-id-slot = Borrower ID

library-console-no-bookbinder = No linked book binder detected. Link one with a network configurator.
library-console-binder-no-paper = The book binder does not have enough paper to print a book.
library-ui-binder-status = Book binder: {$sheets} sheets of paper ({$cost} per printed book)
library-ui-no-binder = No book binder linked, books can not be printed. Link one with a network configurator.
bookbinder-need-sheets = The book binder needs {$sheets} sheets of paper to bind a book.
bookbinder-examine-sheets = It holds [color=yellow]{$sheets}[/color] of {$max} sheets of paper. Binding a book uses {$bind}, printing one from the archive uses {$print}.

library-port-name-binder = Binder
library-port-description-binder = Link this to the console port of a book binder so it prints this console's books.
library-port-name-scanner = Scanner
library-port-description-scanner = Link this to the console port of a book scanner so this console can upload what it scans.
library-port-name-console = Console
library-port-description-console = Link this to the binder or scanner port of a library console.
bookbinder-eject-verb = Eject paper
bookbinder-ejected = The book binder pushes out {$sheets} sheets of paper.

# Book pinpointers
library-pinpointer-ui-title = Book pinpointer
library-pinpointer-ui-header-room = Books in this room: {$count}
library-pinpointer-ui-header-station = Books on the station: {$count}
library-pinpointer-ui-search = Search title or author
library-pinpointer-ui-track = Track
library-pinpointer-ui-no-match = No books match your search.
library-pinpointer-ui-more = Showing {$shown} of {$total} books, search to narrow the list.
library-pinpointer-tracking = The pinpointer locks onto "{$title}".
library-pinpointer-off = The pinpointer switches off.
library-pinpointer-lost = The pinpointer loses the book and switches off.
library-pinpointer-no-books-room = The pinpointer finds no books in this room.
library-pinpointer-no-books-station = The pinpointer finds no books on the station.

# Registered inventory, missing books
library-ui-register-book = Add scanned book
library-ui-status-present = In library
library-ui-status-taken = Taken, missing in { $seconds }s
library-ui-status-missing = MISSING
library-ui-status-checked-out = Checked out
library-console-not-here = That book is not in the library right now.
library-console-register-ok = Book added to the inventory.
library-console-register-no-book = Put a book in the linked scanner first.
library-console-register-not-book = That is not a book.
library-console-register-already = That book is already in the inventory.
library-console-register-full = The inventory is full.
library-console-register-not-archived = Only books of the archive can be added. Upload it and have it approved first.
library-pinpointer-ui-mode-all = All books
library-pinpointer-ui-mode-missing = Missing
library-pinpointer-ui-header-missing = Missing books: { $count }

# Manual missing clear, indefinite loans, overdue list
library-ui-clear-missing = Clear missing
library-ui-indefinite = No return date
library-pinpointer-ui-mode-overdue = Overdue
library-pinpointer-ui-header-overdue = Overdue books: { $count }
library-pinpointer-ui-borrowed-by = { $title } (taken by { $borrower })

# Blank books
library-ui-tab-blank = Blank book
library-ui-print-blank = Print blank book
library-ui-blank-info = Prints an empty book with the cover you pick, using the same paper as an archive book. It is not part of the archive. Write in it, then scan it and upload it from the Upload tab. CentComm has to approve the upload before it can be printed from the archive or added to the inventory.
library-blank-book-name = blank book

# Blank book title
library-ui-blank-title = Title of the book, for example Lina's Diary

# Book requests, library console side
library-ui-tab-requests = Requests
library-ui-no-requests = Nobody has asked for a book.
library-ui-request-blank = Blank book: { $title }
library-ui-request-archive = #{ $id } { $title } by { $author }
library-ui-request-fill = Fill in
library-ui-request-accept = Accept
library-ui-request-deny = Deny
library-ui-request-accepted = Accepted
library-ui-request-denied = Denied
library-console-new-request = A new book request arrived.

# Book request console
library-request-ui-title = Book requests
library-request-ui-tab-archive = Archive book
library-request-ui-tab-blank = Blank book
library-request-ui-linked = Requests go to the library.
library-request-ui-not-linked = This console is not linked to a library console.
library-request-ui-blank-info = Ask for a blank book. Write the title you want and pick a cover, the librarian takes care of the rest.
library-request-ui-send = Send request
library-request-ui-request = Request
library-request-sent = Your request was sent.
library-request-no-console = This console is not linked to a library console.
library-request-cooldown = Please wait a moment before the next request.
library-request-no-title = Write a title for the book first.
library-request-unknown-book = That book is not in the archive.
library-request-duplicate = You already asked for that book.
library-request-too-many = You have too many requests waiting.
library-request-full = The library has too many requests waiting.
library-port-name-requests = Requests
library-port-description-requests = Link this to the request inbox port of a library console, so it receives the book requests.
library-port-name-request-inbox = Request inbox
library-port-description-request-inbox = Link the requests port of a book request console to this.

# NanoChat messages from the library
library-nanochat-sender = Library
library-nanochat-overdue = Your book is overdue, please return it back to the library.
library-nanochat-request-ready = Your book request is ready, either come to library or request someone to pick it up on your behalf.
library-nanochat-request-denied = Your book request was denied.
