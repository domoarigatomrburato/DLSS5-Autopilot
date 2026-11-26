# The app: screens, states and flows

What the person sees and does, screen by screen: the library, a game's page
and its settings, the dlss page, the video, remix and vr pages, the report and
before/after views, the log drawer, the help menu, the tray and its
notifications. Read from the reference's `core/ui/*` (`app.py`, `shell.py`,
`libpage.py`, `gamepage.py`, `dlsspage.py`, `remixpage.py`, `vidpage.py`,
`vrpage.py`, `setpanel.py`, `tray.py`, the `ctl_*.py` controllers),
`core/reportui.py`, `core/reportout.py`, `core/compareui.py`,
`core/sessioncard.py`, `core/lookout.py`, `core/dlssupdate.py` (its refusals and
restore rules, as the page shows them), the README's "What it does", "Using it",
"In the game" and "Settings worth knowing", and the four screenshots under
`docs/screenshots/`.

This spec fixes the **information and the flows**, not the look. The rewrite
uses Avalonia's Fluent theme; where the reference hand-built something (cover
glow, animated cards, a canvas-drawn menu, monospace type, a custom title bar
colour) it is listed once under "Only the reference's look" and is not a
requirement. The engine decisions behind each button (which route, what an
install writes, what a verdict says) are in the other specs; this one says how
they reach the person. Wording quoted below is the reference's and is content,
not law: the rewrite may improve it, but each message must still say what it
says here.

Where this spec departs from the reference on purpose it says
**Changed from the reference:** with one line of why. Anything not settled is in
"Open questions".

## What the user gets

### 1. The window

| Aspect | Behaviour | Evidence |
| --- | --- | --- |
| Title | "DLSS 5 Autopilot". | `shell.py:24` |
| First size | 1400x900 (at 100 % scale), but never more than 95 % of the screen's width and 90 % of its height. Minimum 900x620, but never more than 60 % / 50 % of the screen. | `shell.py:110-113` |
| Remembered | Size, position and maximised flag are saved when the window closes (and before a self-update restart) and restored on the next start. A minimised or hidden window saves nothing. A maximised window keeps the size it had before, for the day it is restored. | `shell.py:84-101`, `app.py:587-593` |
| Restore limits | Saved size is used only while 200 <= w, h and it fits the desktop; saved position only while at least 120 px of the width and the title bar's strip are on a connected display (an unplugged monitor must not strand the window). With nothing saved, or a bad value, the window opens maximised. | `shell.py:58-80` |
| Scaling | Per-monitor DPI aware. Layout follows the display scale (125 %, 150 %, 250 % all must be usable: the reference needed a fix for buttons hanging off the edge at 250 %). Text and controls never clip: a row that does not fit wraps or moves under, never off the right edge, and there is no sideways scroll. | `win.py:19-31`, `gamepage.py:463`, `libpage.py:101-106` |
| Page width | The content scrolls vertically only. | `shell.py:131` |
| One copy | A second launch does not open a second window: it brings the first to the front (restoring it if minimised, or showing it from the tray) and exits. If the mutex exists but no window does (still starting, or stuck) the second copy opens normally. Any failure of the check opens the window rather than nothing. | `app.py:619-676` |
| Icon | The app icon in the title bar and taskbar. | `win.py:44-69` |

Changed from the reference: the layout is Avalonia's own (resizable panels,
star sizing); the reference's `rail_shape` table of four hand-measured
layouts, its canvas redraw on every resize and its pixel arithmetic are not
requirements. The requirement is only that the navigation never overlaps
itself in a short window: it collapses to labels-less icons, and past that the
pages that do not fit go behind a "more" item (`shell.py:442-462`).

### 2. Navigation and the shell

- **Left rail**, top to bottom: **games** (the logo above it also goes to games),
  **dlss**, **video**, **remix**, **vr**; at the bottom the **watch** item
  (an eye, labelled "watching N" in green, or "watch off" in grey) and **help**
  (`shell.py:388-397`, `ctl_watch.py:84-87`). The active page is marked.
- **Hideable pages.** video, remix and vr can leave the rail; games and dlss
  cannot. Hide from a right-click on the rail item ("hide <page> from the
  sidebar", with a line saying where to bring it back) or from games > view >
  sidebar pages. Hiding the page that is open goes to games; back never lands on
  a hidden page. Stored as `rail_hidden` (`shell.py:395-432`).
- **Pages**: games (library), game, dlss, video, remix, vr. The game page is
  reached only by opening a game; it lights the games item.
- **Back stack** of the last 10 pages. Back goes to the previous page that still
  exists and is not hidden, otherwise to games. The rail and the game page's
  "games" button go straight to games and clear the stack (`shell.py:189-222`).
- **Esc closes things in a fixed order**, one per press: a question that is up
  (it answers as cancelled) -> an open menu or panel -> the log drawer -> the
  page's own back (on a game page, the open settings first) -> the previous
  page. A toast is not a menu: with only a toast up, Esc belongs to the search
  box and the page. In a text field Esc clears the field first. Backspace goes
  back unless the caret is in a text field (`shell.py:351-380`, `gamepage.py:1264`).
- **Status line** (bottom, one line): the last status ("ready" at start,
  "scan complete: N games", "install complete", "failed" ...) in the dim colour,
  or, while a job runs, the job's progress in the accent colour ("[3/9] name",
  "checking compatibility 4/16: game", "updating dlss in X..."). Long text is cut
  with an ellipsis; the full text is in the log (`shell.py:718-747`).
- **Banners** across the top, one per key, each with a message, optional action
  buttons and a close (x): update, crash, report, covers. A new banner with the
  same key replaces the old one (`shell.py:970-1016`).
- **Toast**: one at a time, bottom right, a title, an optional line, optional
  buttons, a close; goes by itself after 9 s (15 s for a watched game's answer).
  A button press dismisses it. A new toast replaces the one before at once
  (`shell.py:1019-1131`).
- **Questions** (`ask`, `info`, `error`, `ask_text`): modal, centred over the
  window, with a title, a text that can be long (the card grows to fit, never
  loses the end of the text), an ok button and optionally a cancel. Enter = ok,
  Esc or a click outside = cancel. A destructive question's ok button is styled
  as danger. A question raised by a worker's failure while another is open goes
  on top, and the one under it returns. Closing the window answers a pending
  question as cancelled (without it, quitting from the tray with a question up
  left a process behind). A question asked while the window is minimised or in
  the tray first brings the window back (`shell.py:1134-1432`).

  Changed from the reference: a danger question's **default** button is the safe
  one (keep / cancel), so Enter does not confirm a deletion; and a click outside
  does not cancel a question that carries typed text (the profile name). Why:
  Enter-confirms-danger and click-away-loses-input are the two ways the
  reference lets a stray key do harm.
- **Log drawer** and **status line**: see section 14.
- **Help** (rail, bottom): how it works (opens the README's route section in the
  browser), compatibility list (opens the published page), report a bug,
  suggest a feature (opens a prefilled GitHub issue with the label
  `enhancement`, title "idea: ", the version and the card), open the log file
  (the file selected in Explorer; if that fails, a dialog with its path),
  keyboard shortcuts (a dialog listing them, see section 13), and a disabled last
  line: `v<version>  .  <card>  .  driver <version>` or "graphics card not
  read" (`app.py:229-259`, `app.py:479-492`).

### 3. Start-up and first run

1. The window opens on **games**. Before anything is drawn, a worker removes the
   all-users Vulkan layer if the tool runs as administrator with no Vulkan
   install left, and says so in the status line and log (`app.py:56-75, 107-114`).
2. **Setting "read the library when the tool opens"** (games > view, default
   on). On: the saved library (`%LOCALAPPDATA%\dlss5-autopilot\library.json`) is
   shown at once; a game whose folder changed on disk since is re-read on a
   worker, its card says "reading..." meanwhile; installed games' "update (n)"
   is worked out on a worker. If there is no usable saved library but a library
   file exists (for example the version changed, which drops the saved library),
   a scan runs by itself. Off: no scan at all, "rescan" and "add a game" still
   work (`app.py:136-146`, `ctl_library.py:687-713`).
3. **A true first run** (no saved library, no library file): nothing is scanned.
   The games page shows a welcome instead of a grid:
   - "dlss 5 for the games you have" / "finds your games, reads each one, and
     picks the route that fits it.";
   - **find my games** (a full scan, primary) and **add a game**;
   - three lines of advice: works best on 64-bit DirectX 11/12; DirectX 9,
     OpenGL, Vulkan and 32-bit go through extra layers and fail far more often;
     never online - anti-cheat treats ReShade add-ons as tampering (in the
     warning colour);
   - while that first scan runs: "finding your games..." / "Steam, Epic, GOG,
     EA, Ubisoft, Xbox and the usual folders".

   The video player alone in the library is not a library: the welcome still
   shows (`libpage.py:315-399`, `ctl_library.py:149-152`).
4. In the background, shortly after start: the **update check** (section 8, self-update), the
   **shared results list** is fetched (feeds the status lines and the "others"
   block), **watching** starts for installed games, and after 5 s the **dlss
   read** (a day-old read only; it waits and retries every 15 s while a scan or a
   job runs) (`app.py:115-123`, `ctl_dlss.py:203-214`).
5. The first time a game has no cover on this PC, a banner asks once: "some
   games have no cover on this PC - look them up online? (sends their names to
   Steam's store)" with **look them up** and **no**. Nothing is sent before a
   yes. games > view > "look up covers online (sends game names)" switches it
   later (`ctl_library.py:647-678`).
6. Defaults of every stored setting are in section 14.

### 4. The library (games)

**Header.** Title "games"; a count line: "N games" (or "n of N games" when a
filter, search or architecture view hides some), "n with dlss 5" (installed
count), and, when the last scan stopped, why ("the scan stopped: <first line>",
or "the scan stopped - details in the log"). A search box (Ctrl+F; typing
anywhere starts it), **scan** (menu), **add a game** (beside it; only the icon
when the width is short) (`libpage.py:42-135, 143-171`).

**scan menu**: *rescan* (look for new games: asks the launchers what is
installed, reads only games not seen), *full rescan* (read every game again:
every launcher, game folder and emulator), *add a game*, and, when there is
something to do, *update all - N to install again* (section 5, installed games with
stale parts; see the bug note on the two "update all" buttons).

**Filter tabs**: `all`, `installed`, `working`, `needs a look`, `update`, each
with its count; the last three are hidden while their count is 0 (unless active).
`installed` includes installed, working, needs a look and update. Counts follow
the current architecture view and hidden state (a 32-bit game is not counted in
"installed" while the view shows 64-bit only) (`libpage.py:173-238`,
`ctl_library.py:414-494`).

**view menu**: sort (scan order, name, store, graphics api, route, status;
choosing the active column flips ascending/descending; stored as `games_sort`),
architecture (32 + 64-bit, 64-bit only, 32-bit only; `games_arch`), show/hide
hidden games (with their count), "read the library when the tool opens",
"look up covers online", sidebar pages. When a non-default view is active a
dim line says so ("64-bit only . hidden shown . by name (desc)"). Hidden games
that are not shown get a footer: "N hidden . view shows them" (`libpage.py:239-313,
639-712`).

**A game is a card**: cover, name, one status line. A dot before the name marks
installed games with a state worth a glance (update, working, needs a look); a
hidden game or one that cannot be set up is dimmed. Cover order (README and
`art.py`): the person's own pick, Steam's library art, Epic's catalog art or
Steam's store art found by the game's name (only after the consent above), the
executable's own icon, a placeholder glyph. A cover that has not loaded yet
never blocks the page; the grid redraws when covers land, at most once every
100 ms. Cards flow in as many columns as fit, at least two (`libpage.py:445-536`,
`ctl_library.py:497-586`).

Click or Enter opens the game. Right-click (or Shift+F10 or the Menu key) on a
card: *open*, *hide from the list / show in the list again*, *open folder*,
*choose cover...*, *choose background...*, *choose logo...*, *use the automatic
pictures* (only when one was chosen), and, for an installed game, *uninstall
dlss 5* (which opens the game and asks the uninstall question, section 8). Picture
chosen -> status "<game>: cover set"; a file that cannot be used -> an error
dialog naming why. Hiding stores the folder in `hidden_games`; a hidden game is
not read by the dlss page and not touched by its update all (`libpage.py:718-741`,
`ctl_library.py:589-646`, `ctl_dlss.py:55-68`).

Changed from the reference: the picture chooser accepts WebP as well as JPG,
PNG and BMP (the reference listed only what GDI+ draws, `ctl_library.py:605`);
the rewrite's imaging stack has no such limit.

**Empty results** (a library exists but nothing shows): `no game matches "<q>"`
with a "show all games" link and, only for a search, an "add a game - pick the
folder it is in" link (the moment a game the scanners do not know is being
looked for); `no games are '<filter>' right now`; `all N games are hidden`; `no
games to show with this view`. "show all games" resets filter, architecture,
hidden and search (`libpage.py:400-443`).

**add a game**: a folder picker ("select the game folder"). No executable in it
-> error "No executable was found in: <folder>". A folder (or executable) the
library already has -> that game opens instead, un-hidden, with the status "<name>
is already in the list". Otherwise the game is added at the top, remembered in
`manual_folders` (re-added by every full scan, which finds only what launchers
list), read on a worker, and opened. Source is "Manual" (`ctl_library.py:1020-1058`,
`ctl_library.py:56-91`).

**Scan** (rescan or full): refused while another job or a scan runs. While it
runs: "scanning your stores and game folders..." in the status line (then
"checking compatibility i/n: <game>"), a travelling bar under the filters, and
the cards already known stay. Done -> "scan complete: N games"; "N folders had
no executable" is kept in the scan note; games two stores report for the same
executable appear once; manual games survive; the saved library is rewritten;
the watcher list and the stale counts are refreshed. Failure (GitHub's rate
limit, a store that did not answer) -> the reason under the title and in the log
in the error colour; an unexpected crash also offers the crash banner
(`ctl_library.py:744-867`).

### 5. A game's one-line status (library card and filter)

The card's line is the first row of this table that matches. The same state
decides which filter tab the game is on. The dot colour is never the only
carrier: the words are always there.

| # | Condition | Line | Colour | Filter kind |
| --- | --- | --- | --- | --- |
| 1 | the game's compatibility row is not read yet | `reading...` | dim | ready |
| 2 | the row could not be read at all | `unreadable` | warning, dimmed | unsupported |
| 3 | the install is not supported for this executable (for example a protected executable whose bitness is unknown) | `choose architecture / api` when the executable carries a warning and no error, else `unsupported` | warning, dimmed | unsupported |
| 4 | not installed, and an anti-cheat is present | the anti-cheat's summary (for example `BattlEye`, `nProtect GameGuard`) | dim, dimmed | anticheat |
| 5 | installed, and the install record's graphics API differs from the API read now (and it is neither DXVK nor the Vulkan layer) | `reinstall - was <api>` with a dot | accent | update |
| 6 | installed, n >= 1 recorded parts need installing again | `update (n)` with a dot | accent | update |
| 7 | installed, last verdict worked | `working` (+ `. <fps> fps` when measured) with a dot | green | working |
| 8 | installed, last verdict did not work | `needs a look` with a dot | warning | look |
| 9 | installed, no verdict | `installed` | muted | installed |
| 10 | not installed | the reliability of the recommended route: `` (reliable), `beta`, `experimental` (often fails); when people shared results and some worked, `N of M worked for others` replaces it, in the accent | dim / accent | ready |

Rules behind it:

- Installed wins over anti-cheat: an installed game with an anti-cheat stays on
  the installed / working / update tabs (the reference once dropped it from them
  while update all still reinstalled it) (`ctl_library.py:320-325`).
- **n** in `update (n)` is the number of recorded parts (DLSS, ReShade, the
  neural runtime, OptiScaler ...) whose newest build differs, "a newer build, or
  the same version in a package that changed", plus the case "update or verify
  files removed files the tool wrote". It is computed off the UI thread at start,
  after a scan, an install, an uninstall and an update, and shown as a count only;
  the detail ("name: installed -> latest", or "(current is X, a different
  build)") is in `check versions` (`ctl_library.py:869-889`, `ctl_game.py:2011-2033`).
- The verdict on the card is the tool's own last reading of a session (from "did
  it work?" or the watcher), stored per install folder. Any install, uninstall,
  autopilot pass, library-wide update or DLSS swap on that folder deletes it:
  it described files that are gone (`ctl_library.py:260-262`, `ctl_game.py:1526`).
- `<fps>` is shown only for routes whose logs carry a frame rate or model cost
  (optiscaler and the feeder's 64-bit DirectX 11 path).
- "N of M worked for others" counts only successes ("never a count of failures");
  it appears only while the game is not installed.

Changed from the reference:

- **Three-valued verdict.** The reference stores worked / did not work only, so a
  session whose logs cannot say (the diagnosis "unsure": a route that logs no
  frames) is stored as failed and the card says `needs a look` (row 8), while the
  game page's session card correctly says "unsure" in amber and the share flow
  asks the person (`ctl_game.py:2571-2582`, `sessioncard.py:60-66`). Store
  worked / unsure / failed; row 8 is failed only; unsure reads `installed - not
  confirmed` (accent-neutral) and counts as installed, not as "needs a look".
- **Anti-cheat is a mark, not a state.** An installed game with an anti-cheat
  loses the anti-cheat information on its card (row 4 applies only to not
  installed). Add the anti-cheat's name as a separate badge on every card that
  has one, so the information the README promises ("marked in the library") holds
  for installed games too.

### 6. A game's page

Opening a game reads it on a worker (nothing walks a folder on the UI thread):
the page shows `reading the game...`, and **install, update, autopilot, settings**
stay disabled until the read lands (`gamepage.py:536`, `ctl_game.py:444-503`).
Reading a game again after an API, bitness or executable change keeps what the
person set since; entering a different game resets the settings to that game's
defaults, and an installed game opens on **what it was installed with** (its
route and settings, once per entry), so "update" and "install again" never move
a game to the recommended route or reset its dials. If the recorded route is no
longer offered, the page says so in the log and shows the recommended one
(`ctl_game.py:605-626`).

**Header**: back to games; **change picture** (cover, background, logo, or the
automatic ones); the name (hidden when the background art carries the game's own
logo); **chips**: graphics API (`DirectX 12`), bit width (`64-bit`, or `?-bit`),
store, and the route with its reliability (`feeder . stable`; green = stable,
amber = beta, warning = experimental) once read. The backdrop is the game's own
art and may arrive late; the page is plain until it does (`gamepage.py:152-231`).

**Status lines** (up to two lines each, the rest in the log), first match:

1. `reading the game...`.
2. The supported-check failure, in the warning colour.
3. `n installed part(s) need(s) installing again - press update` (accent) if
   stale parts; else
4. `last run: working` (+ `. <fps> fps`) green, or `last run: <what it said>`
   warning, from the stored verdict; else
5. `installed` (muted) if installed; else
6. `the last install was taken back out: <reason>` (warning) when the previous
   first install failed and was rolled back and left its reason.

**Notes** under it, at most 3 as callouts, then `N more notes in the log`. Each
is one headline (cut at the first ": " or " - " within 56 chars, else a word
near 56) plus the rest, with a "more" link that opens the whole text. Kinds:
error (red: not usable on this PC with the reason; an anti-cheat is present with
the files found; a swapped runtime), driver (amber: the driver warning, cut to
150 chars), warning (amber: a foreign NGX hook in the folder; known engine
quirks; a protected executable chosen), note (grey: RE Engine - REFramework goes
in first; the folder is already set up for another executable; the emulator's
renderer hint). What swaps put at risk comes first: with "keep the game's own
nvngx_dlss" off while the game has one, or a ray-reconstruction swap, the
"<file> is swapped: the game's own file is replaced - an online game's
anti-cheat can treat that as tampering, and a launcher that verifies its files
puts its own back. Keep the game's own for anything played online." note, and
the same as a log warning the moment the setting changes (`gamepage.py:262-443`,
`ctl_game.py:693-835, 1030-1033`).

**Actions** (the first is the one in focus):

| State | Buttons |
| --- | --- |
| not installed | **install** (primary), **autopilot** (tooltip: "experimental: installs, starts the game, checks it really loaded - and if not, tries the next route and tells you"), **settings** |
| installed | **play** (primary), **did it work?**, **install again**, **settings**, **uninstall** |
| installed with stale parts | **update (n)** (primary; "installs again on the same route and settings, with the current parts"), **play**, **did it work?**, **settings**, **uninstall** |
| video player card | installed: **open the player** (primary), **install again**, settings; not installed: **install** |
| a job on this game | one disabled primary button naming the job and its percent (`installing . 42%`, `autopilot running`, `stopping after this install`, `reading the logs`, `uninstalling`, `working out the plan`, `asking for versions`, `updating dlss files`, `installing a remix mod`), a progress line under it (the current step or the download), and, for autopilot, **stop** |
| a job on another game | one button "`<job>: <other game>`" that opens that game's page (and **stop** for autopilot) |

Disabled: install / update / install again / autopilot while the game is being
read or cannot be installed; play for 10 s after it has started (so a double
click does not start two); uninstall while any job runs (`gamepage.py:445-621`).

**Link row** (small actions): the DLSS line when this game's DLSS is behind NVIDIA
(`dlss 3.7.20 -> 310.9.1 and 2 more  update`, muted instead of accented and with
the anti-cheat's name or "kept as it is" appended when the row would ask first;
pressing it opens the dlss page with this game's update running, section 11);
`N of M worked with <route>` (scrolls to the others block); `watching this game`
(green) or `watch off` for an installed game; **before / after** (installed
only); **what will happen?**; **open folder** (`gamepage.py:623-702`).

**Settings** open in place under the actions, never in a panel that must be
closed first; Esc or the close link closes them first. They open themselves
when the executable is protected and its architecture must be chosen (section 7).

**The session result** (one block under the page; each action replaces it):

| Kind | Headline | Body | Buttons |
| --- | --- | --- | --- |
| installed | `installed` (`installed - not recorded` when the record could not be saved; `- N warnings` in amber when warnings) | the install's warnings (first 4, "N more in details"), then "in the game": the first 3 numbered steps for that route (`all steps and warnings` / `fewer` toggles the rest and the warnings) | details |
| failed | `it stopped` | the whole refusal or the last error line, wrapped (a refusal names what to pick instead in its last words) | report a bug, details |
| removed | `uninstalled - the game's own files are back` (amber with a warning when ReShade's all-users Vulkan layer is still registered: "start this tool once as administrator to remove it") | | details |
| diagnosis, working | the verdict (green) | the cost line when measured: `1.8 ms of model a frame . 75% work area` (optiscaler) or `61 fps . 100% work area . 4,200 frames` (feeder; the work area only when the add-on's own config confirmed it) | share the result, set the work area to N% (only when a tune was suggested), details |
| diagnosis, failed | the verdict (red); findings: up to 3 of the bad/warning ones with a cross or warn glyph | the reason why another route is offered | **try <route>** (primary; only when installed, no job running, and a next route is kept with the answer), share the result, set the work area to N%, report a bug, details |
| diagnosis, unsure | the verdict (amber: "the logs cannot tell", not a failure) | as failed | as failed |
| autopilot | the pass's summary; one check or cross per route tried | | details |
| running | `<game> is running` while the watcher saw it start and not yet close | | |

A "did it work?" pressed while the game runs outranks the running card; a
newer answer replaces an older one; **use <exe>** appears on a diagnosis when the
watcher saw the game run from a different executable than the one installed
beside (it re-targets the page; "press install to set it up beside the
executable that actually runs; the files beside the old one stay until you
uninstall there"). The block never shows a verdict about files that an install,
uninstall or update has since replaced (`gamepage.py:793-1021`,
`sessioncard.py`, `ctl_game.py:1504-1570, 1594-1614, 1842-1905, 2516-2570`).

**What worked for others** (below, only when the shared list has results for this
game): `N shared results . M worked . on driver 616.92: 3 of 4`; up to 5 routes,
each with a bar `ok of n`, the measured `61 fps at 75% (3 reports)` when
people reported one; and the footnote "counts, not a promise: a route that
worked for somebody else can still fail here, and one that failed for them can
work". Every value read from that list is defended: it is written by another
repository from text people typed (`gamepage.py:1087-1238`).

### 7. The settings panel (per game, per route)

Three groups: **setup**, **picture**, **extras**, then **profile** and a row of
links: **what will happen?**, **check versions**, **before / after**, **open
folder**, **reset to auto**. A setting is on the page exactly when it does
something for the route; a hidden setting never reaches the install. Everything
that "auto" resolves to is shown beside it ("auto . newest that works here",
"matches your card", "newest", "a free name", "Insert" / "Home"). Only a
setting the person changed is drawn as changed. Every control is disabled while
any job runs. Columns: three above 1060 px of content, two above 680, else one
(`setpanel.py`, `ctl_game.py:862-960`).

Visibility (a row is shown when its condition holds; routes: native, bridge,
feeder, renodx, upstream, standalone, uplift are the "ReShade routes"):

| Setting (label) | Shown when | Notes |
| --- | --- | --- |
| route | always | every route the game allows, `(recommended)` on the pick, `(not for this pc)` on what the card cannot run; changing it resets what the new route cannot use to its default |
| architecture | the executable is protected and unreadable | 64-bit / 32-bit / not set; remembered per folder |
| target exe | the folder has more than one candidate executable | the tool says "selected <exe>" in the log when it picks |
| graphics api | every game except the video player | auto / DirectX 8-12 / Vulkan / OpenGL; remembered per folder |
| loads as | optiscaler | proxy name; "try another if nothing loads" |
| reshade loads as | ReShade routes, API not Vulkan, not DirectX 8/9, not through DXVK | |
| motion vectors | feeder | the choices exclude LumeniteFX and VORT pairs that read 0 % on OpenGL, and the built-in estimator on DirectX 10 and Vulkan |
| dlss5 add-on | native, bridge, feeder, renodx | `use my file` link picks a local add-on |
| nvngx_dlssnr | always | auto matches the card |
| nvngx_dlss | not upstream/remix/uplift, and not while "keep the game's own" is on with the game's file beside the executable | |
| keep the game's own nvngx_dlss | not upstream/remix/uplift, not 32-bit | default on |
| ray reconstruction | the game ships `nvngx_dlssd.dll` and the route is a ReShade route | default "keep the game's own" |
| work area | optiscaler; the feeder on 64-bit DirectX 11 not through DXVK and not in helper mode | slider 25-100 % on optiscaler (default 75), 50-100 on the feeder (default 100), step 5; the hint says the cost (`75% . about 56% of the full-size cost` on optiscaler; `a little faster` / `faster, softer` on the feeder) |
| aim for | with the work area | off or 30...240 fps, global |
| dlss preset, hdr | feeder | |
| model preset, style | optiscaler | |
| model passes | optiscaler builds that run the model before the upscaler | auto, 2x, 3x |
| feeder build | feeder | newest release, newest pre-release, or an exact tag |
| optiscaler build | optiscaler | |
| frame generation (FSR 3.1, 2x) | optiscaler on DirectX 12 | |
| frame generation files | optiscaler on DirectX 12 | `add your own...` picks the files; the link `forget frame generation files` appears with a set chosen, asks first, deletes the tool's copy for every game |
| multi-frame generation 3x/4x | a ReShade route, RTX 40, and `nvngx_dlssg.dll` or `sl.dlss_g.dll` found in the folder | read once on entry |
| run through DXVK (Vulkan) | native, bridge, feeder, and DirectX 11 or a game known to close itself on ReShade | on by itself for DirectX 8/9 and known games |
| run DLSS in a helper process | feeder, 64-bit | |
| VR headset (OpenXR layer) | a ReShade route, 64-bit | |
| swap the Remix runtime | remix | |
| use the NVIDIA card (Windows setting) | the machine has two GPUs | default on |
| overlay key | not remix | route default / Home / Insert / End / Delete / Page Up / Page Down / Backspace / F9-F12 / Pause / Scroll Lock; **global** for every game; picking F10 on the standalone route says the key now has two jobs |

**Profiles**: a dropdown `(none)`, the built-ins Quality / Balanced / Performance
(three work-area steps that carry no route), and the person's own; **save as...**
(asks a name), **delete** (asks; built-ins cannot be deleted). Loading one
applies only the rows the current route shows; a profile that wants a route the
game does not offer keeps the current route and says so. **reset to auto** puts
every setting back to its default and the route back to the recommended one
(`ctl_game.py:2177-2358`).

Defaults: everything auto or off, except keep the game's own nvngx_dlss on, use
the NVIDIA card on, work area 100 (75 on optiscaler), feeder build "newest
release".

Changed from the reference: the override and target-fps controls are global
(stored once) while every other row is per page entry; the panel shows a small
"for every game" tag on those two so the mixed scope is visible
(`ctl_game.py:1005-1024`).

### 8. Actions and flows on a game

**Install** (`ctl_game.py:1384-1447, 1461-1570`).

1. Refused silently if a job runs, the game has not been read, or the install is
   not supported.
2. If an anti-cheat is present (the same list the library uses), ask:
   title "`<anti-cheat summary>` is in this game", text "<files found>. ReShade
   add-ons and anti-cheat do not coexist: the game may refuse to start, delete
   the files, or ban the account. Nobody but you carries that risk.", buttons
   **install anyway** (danger) / cancel. The same question guards autopilot
   ("go on anyway"). The same question guards **update** and **install again** (they are the same install). The dlss page's own swap has its own question (11).
3. The page goes busy (`installing . 0%`), the log gets `=== <game>: install
   (<route>) ===`. Progress: a step index (`[i/n] <step>`) and, inside a download,
   a percent; the button repaints at most every 100 ms.
4. Done: the log lists the files written, notes, warnings ("!!"), and what was
   left untouched; the verdict stored for the folder is deleted; the watcher and
   stale counts refresh; the result block shows "installed", the in-the-game
   steps for the route (keys and tabs per the README's "In the game" table,
   which must name the **overlay key the person chose**), and the log says either
   "play, then close the game - the answer comes up here by itself" (watching on)
   or "played it? come back and press 'did it work?'". If the person opened
   another page meanwhile the status line says "<game>: install complete" and the
   library refreshes.
5. Failure: the busy state ends; the log gets the message; the result block says
   "it stopped" with the detail; a first install that failed is rolled back and
   the page remembers why (status line 6). A disk-full failure names the drives
   (see the net spec).

**Autopilot** (experimental): asks "This is experimental. It will: 1. install the
<route> route 2. start <exe> (or: ask you to start the game - <why>) 3. read which
DLLs the running game loaded (it reads the process, it writes nothing into it)
4. if ours are not in it, wait for you to close the game, install the next route
and go round again. Routes it may try, in order: ... It installs the next one
without asking again, and stops after those or when you press stop." **go ahead /
cancel**. **stop** says "stopping when this install finishes" and finishes the
route in progress. A pass can wait for the person to close the game, so the
person may leave the page; its answer then belongs to its own game (log line
`<game>: <summary>`) and never overwrites another page's result. The order of
routes may be moved by shared results; the reason is logged. The pass is
remembered per game so a later "closed, still not working" offers the next route
(`ctl_game.py:1616-1800`).

**Try <route>**: from a watcher answer or a diagnosis; opens the game, waits up
to 10 s for the page to be read **and only while that game stays open**, then
continues the kept plan without asking again. Busy -> status "busy with <job> -
try <route> when it ends"; page not read in time -> "<game> could not be read
in time - press autopilot on its page to try <route>" (`ctl_watch.py:460-512`).

**Uninstall**: asks "Remove everything this tool put in <game>? The game's own
files are restored." **uninstall** (danger) / **keep**. Also from the library's
right-click menu for installed games. On completion: "uninstalled (N items)", the
page forgets the last session, the verdict and the kept autopilot plan are
deleted, and a leftover all-users Vulkan layer is said on the page and, when
another page is open, in the status line (`ctl_game.py:1802-1905`).

**play**: starts the game from the tool (the engine's launcher), starts watching
it, and says `started <exe>` (status and log) or why it could not ("it did not
start from here"). On an uninstalled game it only says so in the log and starts
it as it always starts (`ctl_game.py:1907-1952`).

**what will happen?**: runs the plan on a worker, opens the log drawer and writes
every file the install would write, back up and remove, with `cannot...` lines in
red and `warning`/`outside` lines in amber, ending "nothing is downloaded or
written by this preview". Writes nothing (`ctl_game.py:1955-1988`).

**check versions**: only for an installed game ("nothing is installed in this
folder yet" otherwise); opens the log and writes one line per component:
`[!!] name: installed -> latest`, `[--] name: installed (current is X, a different
build)`, `[ok] name: installed`, then the summary and "press update - your
settings and backups are kept" (`ctl_game.py:1990-2033`).

**did it work?**: reads the logs on a worker (the diagnosis spec), then the
frame rate or model cost where the route logs one, then Windows' own crash record
on a second worker. Result block and log as in the table above. A recorded
fault **outranks a log that stopped in a good place**: "It ran, and then the game
crashed - Windows recorded the fault in <module>."; "It started, and nothing
here recorded the session - Windows recorded the fault ..."; and a
"play once and check again" verdict becomes "ReShade loaded, and the game crashed
before the add-on got going". A fault recorded as the game closed on purpose (the
log ends with the unload) is shown but does not count against the result; a
fault from before the session began (by the log's own time stamps) belongs to an
earlier launch and is ignored. When the answer is not working the log adds
"stuck? 'report a bug' is under help ..." and the community's advice for another
route; it also prints, when a frame rate target is set, the suggested work area
(`ctl_game.py:2451-2640, 2660-2790`). A report made afterwards corrects the
verdict on screen too (the two answers must never disagree) (`app.py:400-477`).

**share the result**: needs a diagnosis on the page. A crash on record answers
"failed"; a verdict the logs cannot decide asks "The logs this report read cannot
show whether DLSS 5 ran - you watched the game. Did the DLSS 5 picture show in
the game?" **it showed / it did not**, and the result says the answer was the
person's. A final question lists exactly what the browser tab will carry (game
name and executable, route and build, API, whether the game ships DLSS/FSR/XeSS,
card, driver, tool version, whether it worked, the one-line verdict, the work
area/ms/fps when measured) and says "nothing is sent unless you post it. No
paths, no user name, nothing else." **open it / cancel**. Nothing is sent by the
tool (`ctl_game.py:2877-2976`).

**report a bug** (help menu, a failed result, the crash banner): a window "two
questions, then the report opens" over the main window: "did <game> start?" with
`it started and ran` / `it started, then closed itself` / `it never started`
(Ctrl+1..3), and "what happened?" (a text area, at least 3 words), **open the
report** (Ctrl+Enter; disabled until both are answered, a hint says which is
missing) / **cancel** (Esc). Then the report (version, card, driver, game, route,
the last diagnosis, the log tails, the faulting module of a Windows crash) is
saved to a file, put on the clipboard (read back to prove it), and a GitHub issue
URL is opened with as much of it as fits. A banner then says in one line what
actually happened (in the browser with everything / with its start and the whole
report on the clipboard / no browser, clipboard / neither - use the zip) with
**copy report** and **save zip** (the report with the logs it read, in
`...\dlss5-autopilot\reports\report-<game>-<time>.zip`; when saved, **show it**
selects it in Explorer). Works with no game open. Nothing is sent
(`app.py:261-390`, `reportui.py`, `reportout.py`).

**Self-update**: at start a worker asks for the newest release (a day's cache).
Newer -> banner "version X is out (you are on Y)" with **update now** (+ " -
downloading..." when it downloads by itself). When the downloaded build's hash
checked out: "version X downloaded and verified", **restart into it**. **update
now** in the exe asks "The new version is downloaded and the tool restarts into
it. The current build is kept beside it as .old.exe for the new one's first run,
so you can go back; it is removed when the new version starts a second time."
**update / not now**; a failure -> "update failed: <reason>" with **try again**;
run from source, it opens the releases page. `"auto_update": false` makes the
download manual. The window's place is saved before the restart. The download
shows in the status line as "update - N.N MB" and counts as a job (it blocks
installs while it runs) (`app.py:505-616`). The sums/verification rules are
the net spec's.

**Crash banner**: an unexpected exception in the UI or a worker logs the trace,
writes "!! internal error - see the log file (<path>)" and shows once per
session "something went wrong - the details are in the log file" with **report
it** (`app.py:162-194, 494-503`).

### 9. Jobs, progress and what runs off the UI thread

- **Off the UI thread, always**: reading a game (detection, anti-cheat, API,
  reads of the record), every scan and compatibility row, covers and backdrops,
  the stale-parts check, the dlss read and re-read, installs, uninstalls,
  previews, version checks, diagnoses and the watcher's reads, the Windows
  crash lookup, community fetches, downloads and renders, camera and screen
  listing, the VR loader walk, the remix presence walk and install, the update
  check and download, the self-update's old build cleanup, and process snapshots.
  The UI thread only draws and answers input. A worker never touches a control:
  it posts a message that the UI thread applies, and an answer always carries the
  game it is about (the page may show another game by then) (`app.py:1-7`,
  `ctl_game.py:1461`).
- **One writing job at a time.** The reference has a single flag that every
  install, uninstall, autopilot pass, update-all (library and dlss), remix mod
  install/removal, self-update download and "what will happen?" sets, and which
  every start of another job checks. A request made while another job runs is
  **refused with the reason**, never queued silently: the button is disabled and,
  where it is a press that could not be disabled (a menu entry, a "try" from a
  toast, the dlss page's scan), the status line says "another job is running -
  the dlss files wait for it" / "busy with <job> - try <route> when it ends". A
  game being written is never written by a second job: this includes the dlss
  page's swaps, the remix mod, the library's "update all", the self-update and
  an autopilot pass that waits for a game to close. Read-only work (scan reads,
  the dlss read, "check versions" reads, covers) runs beside jobs.
- **A watcher answer waits for the job**: when a watched game closes while a job
  is rewriting its folder, no answer is read from half an install; the log says
  "`<game>` closed while `<job>` runs on it - that job gives the answer"
  (`ctl_watch.py:129-153, 169-177`).
- A job ends in every case: a failure while drawing its answer still clears the
  busy state (the reference needed a list of message kinds to guarantee it).

Changed from the reference: the job state is explicit (which job, on which game,
which folders it writes, how far it is), not a boolean plus a string. A
pending request for a **busy game** is refused with the reason; the rewrite
**must** serialise per game folder, and whether jobs on different games may
run side by side is Open question 1 (the reference blocks everything at once).

### 10. The watcher, the tray and notifications

**Watching** (default on; the rail's eye): every installed game in the library is
watched while the tool runs. The watcher polls the process list every 4 s,
reads what the game has loaded 8 s after it starts, and reports start and close.
A run shorter than 20 s that never got its modules read (a crash at launch or a
launcher hop) gives no answer. Start: status `<game> started - watching`, log
`> <game> started - the watcher read what it loaded`, and the game's page shows
the running card. Close: the status says `<game> closed - reading its logs`, the
logs are read on a worker exactly as "did it work?" reads them, together with
Windows' crash record; the answer is stored as the game's verdict, shown on its
page if open, and announced (below) (`ctl_watch.py:155-430`, `lookout.py`).

The eye's menu: "watch installed games (N) while the tool is open" (toggle;
off stops the watcher and clears every "is running" card) and "keep watching in
the tray when the window is closed" (default off), with the explanation "when a
watched game closes, its logs are read and the answer shown".

**Announcement of a close**: the page and library refresh (soft), and one of:

- window visible: a toast titled `<game> closed`, text `working` /
  `it crashed in <module> - <why>` / the reason for another route / the first
  words of the verdict; accent green or red; buttons **try <route>** (primary,
  when a next route is kept) and **open** (primary when there is no route to
  try); 15 s;
- window in the tray: a Windows notification with the same title and text (the
  text gains " - open the tool to try <route>"), a warning icon when it did not
  work. Clicking the notification opens the window on that game and re-shows the
  toast 0.4 s later.

**Tray** (only when "keep watching in the tray" is on, watching is on and at
least one installed game is watched): closing the window hides it and leaves an
icon with the tip "DLSS 5 Autopilot - watching your games" and a notification
"still watching / N installed games - the answer comes here when one closes".
Left click or the notification opens the window (and goes to the pending game);
right click shows **open DLSS 5 Autopilot** and **stop watching and quit**. If the
icon cannot be created in 3 s the window quits normally. Otherwise (off, or
nothing to watch) closing the window quits: the watcher stops and the icon
leaves. The tray notifications are Windows' own and obey Focus Assist, so they
appear over a full-screen game only as Windows decides (`ctl_watch.py:519-575`,
`tray.py`).

### 11. The dlss page

Keeps the three NVIDIA runtimes current for every 64-bit game in the library,
with or without DLSS 5. Rail item 2. Everything below is for games that are not
hidden and are not the video player; 32-bit games are counted and skipped.

**Header**: title; a one-line purpose "NVIDIA's newest dlss files for your games;
the game's own stay beside them." with "online games: keep their own." in amber;
**update all (N)** (primary, only when N >= 1, disabled during a job or scan);
**check again** (only once a read exists; a link, disabled with a tooltip during
a job). A band: while a scan runs a sweep bar and `reading i of n . <game>`;
else `newest from nvidia: 310.9.1 . 16 games checked 4 min ago . 1 32-bit game
skipped . offline: newest from an earlier check`. Under it: `N games behind
NVIDIA's newest` (amber) with `. A to update now, B need a look first` when some
cannot be taken by update all; `every game is on NVIDIA's newest` (green); `the
newest builds could not be read - versions are shown as found` (dim) when
NVIDIA's list is unavailable (`dlsspage.py:62-270`).

**Empty states**: no library -> "no games yet" + **go to games**; scanning ->
"reading your games..."; never read -> "not checked yet" + **check my games**; only
32-bit games -> "no 64-bit games in the library"; none ship DLSS -> "none of the N
games checked ship NVIDIA DLSS files".

**A row** is one game that ships at least one runtime: cover thumbnail, name
(opens the game's page), a tag (the anti-cheat's name, `kept by you`, or
`running: <process>`), then one line **per build**, not per file (RE Engine keeps
a second copy of each runtime; identical builds collapse to `N copies`):

```
super resolution   310.1.0  ->  310.9.1
frame generation   310.1.0  ->  310.9.1
ray reconstruction missing   restore original puts the game's own back
```

Family names: super resolution (`nvngx_dlss.dll`), frame generation
(`nvngx_dlssg.dll`), ray reconstruction (`nvngx_dlssd.dll`). The line reads:

| Entry state | Text after the version |
| --- | --- |
| original (the game's own) behind NVIDIA | `<current> -> <newest>` |
| original, current | the version alone |
| no version could be read | `no version stamp` |
| updated by this page | `<current> [-> <newest>]   game's own <old version> kept` (or `game's own kept`; `the game's own backup is gone` in the warning colour when the backup file is missing) |
| swapped by a DLSS 5 install | `set by the dlss 5 install` (never offered an update here) |
| missing from where the game loads it | `missing   restore original puts the game's own back` |

"Behind" means the on-disk version is lower than NVIDIA's for that family; a file
this page wrote from that same build is current even when its stamp reads lower
than the SDK tag (NVIDIA versions each runtime on its own); an install's swap and
a missing file are never "behind". The right column of a row:

| Row state | Right column |
| --- | --- |
| behind, nothing in the way | **update** (primary); an amber bar on the row's left edge |
| behind, anti-cheat or kept by the person | **update** (secondary, warning glyph, tooltip "<anti-cheat or kept by you>: asks first") |
| behind, game running | **update** (secondary; tooltip "close the game first") and the row tag `running: <name>` |
| not behind, newest known | `newest` with a check |
| every runtime is a DLSS 5 swap | **game settings** (opens the game's page) |
| a job on this row | a button `working` / `42%` with a progress fill |
| a job (or scan) on another row | the buttons are disabled |

Also in the column: **keep its own** / **allow updates** (a game that is behind
and has no anti-cheat, or is already kept; tooltips "update all leaves this
game's dlss files as they are" / "update all takes this game again"; the
status line then says "<game>: update all will leave it as it is / update it
again"); **restore original** (when any file has a backup or was updated by this
page; "puts the game's own files back").

**Order**: fixed once per read (can be updated now first, then behind-but-needs-a-look,
then the rest, by name), then kept while jobs run so rows do not jump; new rows go
last (`ctl_dlss.py:128-151`).

**The reads and their staleness.** The page opens on the last read (kept in
`%LOCALAPPDATA%\dlss5-autopilot\dlss-scan.json`) at once and is usable while a
new read runs.

- Opening the page, or the background read 5 s after start, reads **again** when
  the last full read is older than 24 h, or when the library has 64-bit games
  not read yet. Otherwise it only refreshes which games are **running**, from one
  process snapshot (a game closed since must not stay "running").
- **check again** (and F5 on the page) reads every game fresh and re-asks NVIDIA.
- A read that is due only because of new or changed games reads **those**: games
  whose folder's newest modification time moved since their row was read, and
  games not seen; the rest keep their rows and the last full read's time.
- A game whose bitness the library has not read yet is neither checked nor
  skipped; it is read once known. The read waits for a library scan to end
  (two seconds' polling, once however often asked) and for a job to end (status
  "another job is running - the games are read when it ends").
- When NVIDIA's list cannot be fetched, the newest builds of the previous read are
  kept and the page says "offline: newest from an earlier check".
- After a DLSS 5 install, uninstall, autopilot pass, a library-wide update or
  any swap here, **that game is read again** on a queue of one worker (a game is
  queued once), and the row, the game's page line and the library card update.
  A row read again while a full read runs overrides what the full read read
  (it is newer). A DLSS 5 install over a file updated here shows as "set by the
  dlss 5 install" until uninstalled.
- A fresh read ends with the status "dlss: N games ship it, M behind nvidia's
  newest" and stores the order (`ctl_dlss.py:243-496`).

**update** on a row (`dlss_update`): refused while any job or read runs. If the
game ships an anti-cheat, ask "`<anti-cheat>` - update anyway?" with "<game> ships
<anti-cheat> (<files>). An anti-cheat can read a changed DLSS file as tampering,
and online that can mean a ban. The game's own files are kept either way." **update
anyway** (danger) / cancel. If it is only "kept as it is": "<game> is kept as it is
- update anyway?" "You marked this game to keep its own DLSS files, so update all
leaves it out. The game's own files are kept beside the new ones either way."
After a question the job state is checked again (timers run while a question is
open). The game's page link "dlss ... update" opens this page and does the same;
if a read is running the status says "<game>: the update starts when the games are
read" and it starts when the read ends.

**update all (N)**: takes **only** what may be taken: games that are behind, have
no anti-cheat, are not marked kept and are not running, not hidden, not 32-bit.
It asks "update dlss in N games?" naming up to 4 ("A, B, C, D and 3 more") and
"Each game's own files are kept beside the new ones; restore original puts them
back." **update / cancel**; it does not ask about anti-cheat because it never
takes those games. It then runs one game after another under the one job; a game
gone from the library is skipped; the status line follows ("updating dlss in
<game>..."), each row shows its percent and, afterwards, its own result line; the
end says "dlss: 5 updated, 2 failed - X, Y, Z". A failure does not stop the
others.

**restore original** (`restore`): refused while a job or read runs. Asks nothing
(it only puts the game's own file back). Rules, as the row and the result line
say them:

- It is driven by the record file (`dlss5-dlss-update.json` in the game folder)
  and by the backups on disk, not by what the last read saw: a runtime that is
  missing, unreadable or no longer 64-bit still gets its original back.
- The game must not be running ("<process> is running from this game's folder.
  Close it first.").
- Normal case: the backup (`<name>.dlss5-dlss-original`) is copied back whole,
  the backup is deleted, the record entry dropped; the line reads `<family>
  <updated version> -> <original version>`. If a file is in the way that is
  neither a build this page wrote nor the game's own, it is **kept** as
  `<name>.dlss5-dlss-displaced-<time>` and the result line names it - never
  deleted.
- DLSS 5 is installed over an updated file: restore hands the game's own file to
  the DLSS 5 install, which puts it back when DLSS 5 is uninstalled ("the game's
  own X comes back when DLSS 5 is uninstalled"). If the file changed since the
  update, nothing moves: "DLSS 5 was installed over a file that changed since the
  update - uninstall DLSS 5 first, then restore original".
- The backup is gone: the updated file stays, the entry is dropped, and the
  line says "the backup of the game's own <name> is gone - the <version> stays.
  Verify the game's files in its launcher to get the original back."
- The runtime and its backup are both gone: "<name> and its backup are both gone.
  Verify the game's files in its launcher to get it back."
- A launcher that verified its files and put the game's own file back: the
  update's record entry and the now-duplicate backup are dropped, nothing else
  moves.

**Refusals and their wording** (each is its own state, shown as the row's result
line in the warning colour, the reason first and what to do second; the log keeps
the whole path while the row shows it relative to the game folder):

| Refusal | Says |
| --- | --- |
| 32-bit | "Only 64-bit games are offered: NVIDIA publishes these runtimes for 64-bit Windows." |
| running | "<process> is running from this game's folder. Close it first - Windows does not let anything replace a file a running program has open." |
| anti-cheat (without the question's yes) | "<anti-cheat> is installed with this game. An anti-cheat can treat a changed DLSS file as tampering; for anything played online, keep the game's own." |
| read-only folder | "Windows refused to write into <folder> (<why>). Close the game and its launcher; if it is still refused, the folder needs administrator rights." (Xbox/Game Pass folders get the store's own hint) |
| offline | "NVIDIA's build list could not be read (raw.githubusercontent.com). Check the connection and try again." |
| nothing to do | "No 64-bit DLSS runtime of this game was found to update." |
| a file locked while swapping | "Windows refused to replace <path> (<why>). Nothing was changed. Close the game and its launcher." |

When part of an update went through before a failure the line says what was
updated, then why the rest was not; "nothing was changed" is never said after a
file did change. A game's own file is never deleted by this page: it exists on
disk at all times as the runtime, as the page's own backup, as the DLSS 5
install's backup or set aside under the displaced suffix. Rules of the swap
itself (hash, size, whole-file replace, record) are the dlss spec's.

### 12. Video, remix and vr pages, before/after

**video** (rail 3): header "video", "dlss 5 on a video file, a YouTube link, a
webcam or your screen". With no player set up: four tiles (a video file, a
YouTube link live or saved first for 4K, a webcam, your screen) and **set up the
player**, with "a portable <player>, in a folder you choose . F6 turns dlss 5 on and
off while it plays". The set-up question names the folder and says "Nothing is
written anywhere else"; **use this folder** / **choose another** opens a folder
picker. The player then appears as a card at the top of the library and installs
like a game. With a player:

- **open the player**, **neural rendering on/off (F6)** (says "The player is not
  running - open it and start a video first. F6 inside the player does the same
  thing." when it is not), **settings** (the player's game page); not installed:
  **install**.
- **a link**: a field ("the clipboard is used when empty"), **play**, **download,
  then play**, a "4K when downloading" toggle, **downloads folder**.
- **a file**: **open a video file...**.
- **render a file through dlss 5**: size (native, 2x, 4K), style (default, natural,
  cinematic), **pick a video and render it** (needs the player installed; says
  "this runs the model on every frame ..."), **rendered folder**; the result opens
  in the player.
- **webcam and screen**: a dropdown each ("looking...", "none found"; the screen
  list is every monitor and every visible window of at least 320x200 other than
  this one), **start**, **stop**, **look again**. Ten seconds after a start the
  tool reports whether the feed processed frames ("through DLSS 5: yes - N frames
  processed so far, motion alive") or why not.
- A job shows one busy button (`preparing`, `downloading`, `rendering` with a
  percent) and its line; other video actions wait for it. A failure is an error
  dialog with the last line, the log has the whole text.

**remix** (rail 4): "remix", "path-traced classics . dlss 5 runs inside the mod's
own renderer"; three steps (get the mod, rescan - the game comes up on the remix
route, install - that switches dlss 5 on inside the mod). **in your library**: each
game of yours that has a known mod, with its cover, the mod's name, the author's
note, **open page**, and: for a project that publishes a full zip, **download &
install** (on a game without the mod; `working...` while it runs; `installed` or
`retry` after; "already in" when the folder already holds a Remix runtime), and
**remove** only when the record beside the mod says this tool wrote it (asks "Take
<mod> back out of <game>? The N files this tool wrote are removed and anything they
replaced is put back. The game's own files and a DLSS 5 install in the same folder
are left alone." **remove / keep**); other projects: "install it from its page".
**already remix, nothing to install** and **community mods** are tiles that open
the project's page; **more projects** links. Remix install/remove run under the
single job; if a job is running the page says "Wait until the job that is running
now has finished, then press it again." The result goes to the log and the status
line, the page redraws, and the person is told to rescan; a half-removed mod
offers no "retry" (it would write over what is left) (`remixpage.py`).

**vr** (rail 5): "dlss 5 on what the headset shows, through reshade's openxr layer".
A card "the openxr layer": `not registered` (grey), `registered` (green, the
manifest path) with **remove the layer**, or `registered by ReShade itself`
(amber, "not a file this tool wrote, so it is reused and never changed"); the remove
question says "Take ReShade's OpenXR layer out of this user's registration? VR
games stop loading ReShade, and a DLSS 5 install in a game folder is left as it
is." and a layer the tool did not write is only explained, never touched. Then
"games that carry openxr's loader" (those with `openxr_loader.dll` or
`openxr_loader64.dll`, found by a walk with a 20 s budget; each links to the game's
page, where the VR box is; an empty result says that is not proof of no VR mode).
Then "what is proven: nothing, in a headset" (red) with the honest sentence from the
README and links to ReShade's VR notes and this repository's VR document.

**before / after**: a window of its own (it may stay open while the game runs; it
only reads files) titled "before / after - <game>", Esc closes. It shows the two
newest ReShade screenshots **taken within 5 minutes of each other**, side by side,
each with a header ("before"/"after"), the file name, the time and the size, with
**refresh**, **open folder**, **swap** (when it is not clear which is which) and
**export png** (writes a side-by-side image next to the screenshots,
"saved <name> next to the screenshots"; "export failed: <why>"). With no pair it
explains: "no screenshots found yet" or "N screenshots found, but none two taken
within 5 minutes of each other" and the four steps (switch neural rendering off -
F6 on the feeder, F5 on native and bridge; press ReShade's screenshot key - the
key and folder are read from the game's ReShade settings; switch it back on; press
again, then refresh). Images that are not PNG are not shown ("png only - set
ReShade to PNG").

Changed from the reference: JPG and BMP screenshots are shown too (the reference
cannot decode them and tells the person to change ReShade's format; the Avalonia
imaging stack can). Why: the instruction exists only to work around a toolkit
limit.

### 13. Keyboard and accessibility

**The reference's keys** (`app.py:249-259`, `libpage.py:766-810`, `shell.py`):

| Key | Does |
| --- | --- |
| arrows | move the selection through the library grid (left/right, up/down by a row), scrolling it into view |
| Enter | open the selected game; in the search box, select the first result |
| Down in the search box | go from the box to the results |
| any printable key on games | start typing in the search box |
| Ctrl+F | focus search |
| Ctrl+O | add a game |
| Shift+F10 or the Menu key | the selected card's menu |
| Esc | close in the order of 2 (a question, a menu, the log, back) |
| Backspace | back (not in a text field) |
| Ctrl+H | games |
| F5 (dlss page) | check again |
| Ctrl+1..3, Ctrl+Enter, Esc | the bug report window's answers, send and cancel |
| Enter / Esc | a question's ok / cancel |

The reference has **no** accessibility beyond that: the interface is drawn on a
canvas, so there is no focus order or Tab key, no visible focus ring, no screen
reader names or roles, menus cannot be driven by arrows, hover is the only
"selection", and type is 7-10 pt monospace.

Changed from the reference (requirements for the rewrite, as they come with
standard controls and cost nothing): every action is reachable with the keyboard
(Tab order, visible focus, Enter/Space activate, arrows in menus, lists and the
card grid, Esc as above); every control, status and card has an accessible name
and role (a card reads "<game>, <status>"); a status is never shown by colour
alone (the reference's dot plus the word is the model); text scales with the
system text size and display scale; the keys in the table above keep working.

**Only the reference's look** (not requirements): the monospace type, the amber
accent, the cover "glow" and heat animation, the hover lift, the gliding scroll
and drawer easing, the dithered thumb scrollbar, canvas-drawn menus, dropdowns and
toggles, the hand-coloured title bar, the logo in the rail, the exact pixel
layouts and the rail's four size shapes, the custom toast sliding in. The
information each carries (a status word, a progress percent, a state) stays.

### 14. The log drawer and the stored settings

**Log drawer**: opened by the "log" link at the bottom right (also opened by
"what will happen?", "check versions", a game page's "details" and its "N more
notes in the log"), titled "what the tool is doing", with **close**, **pop out**
(a separate window of the same text, kept in step), **open file** (the log file in
Explorer), **copy** (the drawer's text to the clipboard, status "the log is on the
clipboard"), **clear** (the view only). Lines are plain text with a level: normal,
head (the section titles `=== ... ===`), ok, warn, err; findings are prefixed
`[ok]`, `[!!]`, `[fail]`, `[--]`. It scrolls to the end as lines arrive. Its height
is its own: 32 % of the window clamped to 160-320 px by default, dragged by its
top edge between 90 px and 80 % of the window, double click on the edge resets;
the share (10-90 %) is stored as `log_share`. The log in the drawer is for this
session; the file `%LOCALAPPDATA%\dlss5-autopilot\autopilot.log` is the record.
Closing the drawer: its close link, Esc, a click on empty page.

Changed from the reference: log entries carry a level and a section as data, not
a text prefix, so the drawer, the bug report and the zip can each filter them
(`shell.py:776-782, 881-891`). The drawer opens itself only for the log-first
actions above, never on its own for a result that already has a block on the
page.

**Stored UI settings** (`%LOCALAPPDATA%\dlss5-autopilot\settings.json`; every
value is validated on read, since it is a file people edit, and a bad value falls
back to its default and never stops the window opening):

| Key | Holds | Default |
| --- | --- | --- |
| `scan_on_start` | read the library when the tool opens | on |
| `games_sort` | `[column, descending]` | scan order, ascending |
| `games_arch` | `all` / `32` / `64` | all |
| `hidden_games` | game folders set aside | none |
| `manual_folders` | folders added by hand | none |
| `online_art` | cover lookup consent (undecided / yes / no) | undecided |
| `rail_hidden` | hidden rail pages (video, remix, vr only) | none |
| `watch_games` | watcher on | on |
| `watch_background` | keep watching in the tray | off |
| `window` | `{w, h, x, y, zoomed}` | maximised |
| `log_share` | the drawer's height share | auto |
| `last_verdicts` | per install folder: worked/unsure/failed, what it said, fps, next route and why | none |
| `autopilot_plans` | per install folder: routes and tried | none |
| `dlss_hold` | folders "kept" on the dlss page | none |
| `overlay_key`, `target_fps` | global dials | route default, off |
| `bitness_override`, `api_override` | per folder, from the settings | none |
| `auto_update`, `update_checked_at`, `update_latest` | the update check | auto, - |

The library (`library.json`), the dlss read (`dlss-scan.json`) and the covers
live beside it. The saved library is dropped on a new version and re-read.

## What must never happen

- **A silent close in the middle of a job.** Closing the window (or quitting from
  the tray) never ends a running install, uninstall or swap without asking, and
  never leaves a partial install without its record and rollback.
- **A hidden risk.** An install or update into a game with an anti-cheat, a
  swap of the game's own DLSS file, or an uninstall happens only after the
  question that names it (the dlss page's update all never takes such a game).
  An anti-cheat question's wording says the person carries the risk.
- **Two writers in one game folder**, from any two of: install, uninstall,
  autopilot, library update, dlss update or restore, remix mod, a watcher answer
  read in the middle of one.
- **A swap or restore under a running game** (Windows would refuse; the tool must
  refuse first, with the process named).
- **A deleted game file.** Restore and update never delete a runtime they did not
  write; the game's own file is always on disk somewhere (see 11).
- **A stale answer shown as current.** A verdict, a "next route" offer, a DLSS row,
  an "update (n)" count or a settings panel never describes files that a later
  install, uninstall or update has replaced; answers carry their game and are
  dropped if the person is on another game's page.
- **A colour-only state**, and a state with no words in the log.
- **An action taken for the person**: nothing is sent to the network except the
  fetches the person's use needs and the cover lookup after consent; reports and
  shared results open in the browser and are sent only when the person posts them.
- **A secret or path in a shared result.** The shared result carries only what the
  confirmation lists.
- **A window the person cannot reach**: restore position only while the title bar
  is on a connected display; a question never opens off-screen.
- **A blocked UI thread**: no folder walk, hash, network or process query on it.
  A question never nests an event loop that a worker's failure can re-enter.

## Bugs and questionable choices in the reference not to port

1. **One boolean for every job.** `busy` plus an `action` string plus `job_game`
   patched on top; the remix page and the self-update set the flag by hand; a
   list of message kinds (`JOB_ENDS`) exists so a failing handler still clears
   it; a progress handler failing could clear it while the worker still wrote
   (`app.py:34-54`, `remixpage.py:fetch`, `app.py:566`). Replace with an explicit
   job model.
2. **Closing mid-job.** `on_close` never checks `busy`; every job is a daemon
   thread, so a close ends the process in the middle of a write
   (`ctl_watch.py:519-547`).
3. **Verdicts read as strings.** `startswith("Working")`, `endswith("play once and
   check again.")`, a chain table matching printed fragments (`ctl_game.py:232-236,
   2546, 2578, 2666`, `ctl_watch.py:316,335`, `verdicts.py`). The UI must read a typed verdict
   (state, reason, next step) from the diagnosis, never its sentence.
4. **"Unsure" stored as failed** on the card (`ctl_game.py:2571-2582`), see 5.
5. **The tray keeps one pending game.** Two games closing while hidden: only the last
   is opened to when the person returns (`ctl_watch.py:426`).
6. **Two things called "update all"**: the scan menu's (install again the games with
   stale parts, `ctl_library.py:915`) and the dlss page's (swap NVIDIA's runtimes,
   `ctl_dlss.py:559`), with different rules; the library one asks "update N
   games?", skips anti-cheat games and games with no record, but does **not** skip
   a running game and stops at the first lost record. Name them differently
   (reinstall outdated parts / update DLSS files) and skip running games in both.
7. **A confusing question.** The video set-up question's cancel button is "choose
   another", so Esc or a click outside opens a folder picker, and there is no way to
   cancel (`ctl_video.py:35-48`).
8. **The vr page never looks again.** `found` is read once per session and `shown`
   reads only when it is still None (`vrpage.py:shown`); a game installed later is
   not found until restart. Re-read when the library changes or the page opens
   after a rescan; keep the 20 s budget but resume instead of dropping the rest.
9. **A dlss read requested during a job is dropped**, except by the retry in the
   background path (`ctl_dlss.py:341-345`); the person pressing "check again" gets
   a status line and nothing else. Queue it and run it when the job ends.
10. **Modal waits as nested event loops** (`wait_variable`, a grab on a dim window)
    with five fixes for focus, painting and Tcl binding loss in the history
    (`shell.py:1161-1432`). Use awaitable dialogs.
11. **Canvas redraw of whole pages** on every state change, with soft/hard
    refresh variants and "paint in place" shortcuts (`gamepage.py:237-259`,
    `ctl_game.py:1467-1489`). Bind view models to controls instead.
12. **Hand-editing defence repeated at every read.** Each reader of
    `settings.json` re-validates its own key inline (`ctl_library.py:109-127`,
    `ctl_watch.py:48-55`, `gamepage.py:1121-1127`). Validate once at the store.
13. **Quiet failure paths**: `except Exception: pass` around the machine cleanup,
    the help menu's GPU read, the sort of picture requests and several `_on_*`
    handlers; failures must at least reach the log (`app.py:72-74`, `app.py:231-234`).
14. **Row state as a 6-tuple or `False`** (`ctl_library.py:155-181`): `False` on an
    exception and a tuple otherwise, unpacked by index in several files. Use a
    typed record.
15. **Fixed 10 s and 5 s timers as logic**: `play` stays disabled for 10 s whatever the
    game does; the remix page redraws up to 20 times every 0.5 s waiting for covers.
    A job ends on the event, not a timer.
16. **Overlay key and target fps are global but sit in a per-game panel**; the person
    cannot tell (see 7).

## To verify against primary sources

| Claim | Source |
| --- | --- |
| Windows notifications from a tray icon when the app is not packaged: whether `Shell_NotifyIcon` balloons or Windows toast notifications (which need an AppUserModelID shortcut) are the right call, and whether a click on either activates the app | Microsoft Learn: Shell_NotifyIcon, "Send a local toast notification from a desktop app"; Avalonia `TrayIcon` docs (menu and click only, no balloon) |
| Focus Assist / Do Not Disturb suppress the notification over a full-screen game ("appear over a full-screen game the way any other does") | Microsoft Learn: Focus assist, `QUERY_USER_NOTIFICATION_STATE` |
| Single-instance via a `Local\` named mutex and a window message to bring the first forward; Avalonia has no built-in | Microsoft Learn: CreateMutex; Avalonia discussions on single instance |
| Per-monitor DPI awareness v2 for Avalonia windows, and the saved window position restoring across monitors with different scales | Avalonia docs: screen scaling; Windows: DPI-aware desktop apps |
| Explorer's `/select,` flag to reveal a file | Microsoft Learn: Explorer command-line options |
| GitHub's issue URL length limit that makes a report "cut" (and the title/body/labels query parameters) | GitHub Docs: creating an issue from a URL query; observed limit near 8 KB |
| Avalonia's clipboard API (async, reading back after writing) and file/folder pickers (`IStorageProvider`) on Windows and macOS | Avalonia docs |
| Windows' Application Error record read (the "faulting module") | Microsoft Learn: Windows Error Reporting / event 1000 |
| ReShade's screenshot file naming and key, and its default save path (for before/after) | ReShade source / `ReShade.ini` documentation |
| MPC-HC's F6 toggle is the feed's key, not MPC-HC's | DLSS5-Feeder's docs |
| Windows' Graphics settings (High performance per executable) and the registry value behind it | Microsoft Learn: Graphics settings |
| That ReShade 6's OpenXR layer is registered per user in `HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit` | OpenXR loader spec; ReShade's installer |

## Fixtures

For view-model and headless UI tests; no window needed for the first four.

- **Card status table** (section 5): one case per row; the reference's logic is
  `core/ui/ctl_library.py:292-374`; inputs (install record with `api`, `dxvk`, `proxy`;
  stale counts; verdict `{ok, said, fps, next, why}`; anti-cheat summary; shared
  list entry) are hand-written dicts. A sample shared list is in the reference
  `pages/` and in `core/community.py`'s tests' shapes.
- **Filters and counts**: a library of 8 games covering every status row, with an
  architecture view and a hidden game.
- **dlss page rows**: hand-written `dlss-scan.json` (format in
  `core/dlssupdate.py:1149-1170` and `core/ui/ctl_dlss.py:86-126`: per game folder
  `entries[{rel, family, version, state, original, label, backup}]`, `anticheat`,
  `running`, `stamp`, plus `newest`, `at`, `seen`, `x86`, `offline`); cases: behind;
  RE Engine copies; updated with backup gone; install swap; missing; kept;
  anti-cheat; running; offline; a game that sorts first then stays.
- **Restore and refusals**: temp game folders with `nvngx_dlss.dll`, its
  `.dlss5-dlss-original`, `.dlss5-autopilot-backup` and `dlss5-dlss-update.json` in
  each state of 11 (`core/dlssupdate.py:1-60` documents the stacking); built with
  the reference's PE fixture builder `tests/fake_pe.py` (at the tag
  `python-reference~1`).
- **Session card**: the four states and stat lines (`core/sessioncard.py`), with
  fake `measured` values (`model_ms`, `fps`, `resolution`, `from_config`,
  `frames`).
- **Watcher answers**: event sequences `started`, `closed(<20 s)`, `closed` with a
  job running, closed with a recorded fault inside / before the session / as the
  log ends in the unload (`core/lookout.py:105-200`, `ctl_game.py:196-341`); logs
  from `_tools/reports/` for the verdicts.
- **Report flow**: delivery outcomes (`core/reportout.py:line_for` - four
  outcomes) and the dialog's enable rule (state chosen + at least 3 words).
- **Window settings**: `window` values off-screen, huge, malformed, zoomed.
- **Screenshots**: the four PNGs in the reference's `docs/screenshots/`
  (`library.png`, `game.png`, `dlss.png`, `remix.png`) are the only look reference;
  they show the information each page carries (status words, chips, notes, the
  dlss row and the remix list) and are not a visual spec.
- **Before/after**: ReShade-named PNG pairs `"<game> <yyyy-mm-dd hh-mm-ss>[_n].png"`
  (`core/compare.py:33`), one pair, one lone shot, a pair 6 minutes apart, a JPG.

## Open questions

1. **Parallel jobs on different games.** The reference allows exactly one writing
   job in the whole app. Per-game serialisation is required; may two games install
   or update side by side (the shared download cache, ReShade's per-user Vulkan
   layer registration and the one log would need care)? Recommended: one at a time
   until the engine's M4 is done, then revisit.
2. **Closing during a job.** Ask ("an install is running - wait / close anyway")
   only, or can an install be cancelled with the engine's rollback so closing
   cancels it? Needs the engine spec's cancellation story.
3. **Theme.** Follow the system's light or dark with Fluent, or ship dark only like
   the reference? Recommended: follow the system, since the information does not
   depend on colour.
4. **Windows notifications** (see "To verify"): is a balloon-style notification
   enough, or must clicking it open the game's page (which needs toast activation
   and an installed shortcut for an unpackaged exe)?
5. **The anti-cheat mark on installed cards.** Add the badge (Changed, section 5)
   or keep the reference's single line? And the wording for the "unsure" card line.
6. **VR page and sidebar default.** vr is on the rail by default though it is
   unproven in a headset; hide it by default (still reachable from view > sidebar
   pages)?
7. **Casing and wording.** The reference writes everything in lowercase; the Fluent
   theme reads better in sentence case. Wording stays content, but this changes
   every string.
8. **Reinstall from the library.** Keep a library-level "reinstall outdated parts"
   at all (the game page has its own update), or remove it and keep only the page
   button and the dlss page's update all?
9. **Restore original has no question** in the reference. Keep (it only puts the
   game's own file back) or ask when DLSS 5 is installed over the update?
10. **Remembering the search, filter and last page across runs.** The reference
    remembers sort and architecture but not the filter, search or page.
11. **The compare window stays a separate window** (kept from the reference because
    it is meant to stay open beside the game); confirm, or make it an in-app page.
12. **Log persistence.** Keep the in-memory drawer plus the file, or show the
    file's tail on open so a restart does not empty the drawer?
