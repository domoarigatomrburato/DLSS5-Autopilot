# Watching games, the answer after a session, autopilot, aim-for, compare

The tool watches the games it installed into. When one starts it reads what the
process loaded, and when it closes it reads the logs back and says, without
being asked, whether it worked and what to try next. Autopilot is the same
loop run on demand: install a route, wait for the game, read the process, go
to the next route. "Aim for" turns the cost the logs reported into a setting.
Compare pairs two screenshots (neural rendering on and off) into one image.

Read from the reference's `core/watch.py` (process table, module lists,
sightings, the one-shot recorder), `core/lookout.py` (the continuous watcher),
`core/ui/ctl_watch.py` (what happens on a close, the toast, the tray, the plan
for "try another route"), `core/autopilot.py`, `core/autotune.py`,
`core/compare.py`, `core/compareui.py`, `core/sessioncard.py`,
`core/ui/ctl_game.py` (the "did it work?" press, `_measure_session`,
`_autotune`, `apply_tune`, `autopilot`, `share_result`). What the diagnosis
does with a sighting and with Windows' fault record, the ranking of routes and
the cost arithmetic are in [diagnosis](diagnosis.md); this spec says when
they are called and what the person sees. The install itself, the route list
and what a "game" and its install record are come from the install and
game-reading specs.

## What the user gets

### Watching installed games

**Which games**: every game that is installed (it has our install record, a
picked exe, and the record lists files). A game with no record is never
watched: nothing would be read against. The set is re-read after every scan,
install and uninstall. Games are keyed by their install folder, compared
case-insensitively with normalised separators.

**The loop** (one watcher for the whole app, one snapshot of the process table
per poll, never one watcher per game):

| Constant | Value | Meaning |
| --- | --- | --- |
| poll | 4 s (1 s while a start is waiting to be read) | process snapshot interval |
| settle | 8 s | after a game is first seen, before its module list is read: the graphics DLLs are loaded when the device is created, and a proxy read at 3 s reads as missing when it is merely late |
| minimum play | 20 s | a close shorter than this and never read is a crash at launch or a launcher hop and gives no answer |

(`lookout.py:29-31`.)

**What counts as "the game is up"**: a process whose executable image sits
**under the install folder** (any depth), or a child of one that has an image
path. They are ordered: the install's recorded exe first, then anything that
looks like a game (the game-reading spec's exe test), then helpers, deeper
paths last. The first is the game; the rest are its children and our own
helpers (the 32-bit feeder's `host64\dlss5-feed-host64.exe` outlives the game
by a few seconds and is never the game). A helper is not evidence about the
game and is not counted as "still running" for any purpose.

**Events** (what the watcher tells the app, in this order for one run):

1. `started(folder, sighting)` once the settle time has passed: the sighting
   (below) is written down and the status line says "<game> started -
   watching". It is sent even when the module list could not be read (the
   sighting then says why).
2. `closed(folder, playedSeconds)` when no process of the folder is left, if
   the play was at least 20 s **or** the module list was read (a start that
   settled).

The **answer** to a `closed` event is skipped for a game whose folder an
install, uninstall, autopilot pass or a DLSS update is rewriting right now
("closed while <job> runs on it - that job gives the answer"): reading half an
install overwrote results and offered the route being installed. (`started`
is still shown.)

**Settings**: two switches, "watch installed games while the app is open"
(default on) and "keep watching in the tray when the window is closed"
(default off). Turning the first off stops the watcher, clears every "running"
state (a card left on "is running" would stay over every later result) and
says "watch off". With the second on and at least one game watched, closing the
window hides it to the tray with the notice "still watching: N installed games
- the answer comes here when one closes"; the tray's "open" restores the window
on the game with the same answer and offer. If the tray cannot be shown, the
app quits.

**What the watcher reads and never writes**: it reads the process table and,
once per start, one game's module list; it writes nothing in any game folder
and opens a process for nothing but reading.

Changed from the reference: **one watcher**. The reference has two:
`Recorder` (watches a folder until its game is seen once, after an install and
for the diagnosis's sighting) and `Lookout` (watches continuously), with
different settle times (8 s and 8 s, and 6 s in `wait_for`) and two code paths
that build the same record. One service reads, a sighting is the same record
whichever caller asked, and "wait for this game to start" (autopilot) is a
call on it.

Changed from the reference: the **session length is read from the process's own
start time and end time** (the OS gives both), not "first poll that saw it" to
"first poll that did not" (4 s of error each end), so the 20 s rule and the
session start used for attributing a Windows fault ([diagnosis](diagnosis.md),
"belongs to this session") are exact.

### The sighting

One **sighting** per (game folder), the latest wins. It is what a running game
had loaded, written down at the moment it is true: people play, close the game
and press "did it work?", and by then every question the process could answer
is a guess again.

| Field | Meaning |
| --- | --- |
| `at` | when it was read (UTC) |
| `exe` | the process's image path, and its file name |
| `refused` | empty, or why the module list could not be read (below); non-empty means **unknown**, never "none loaded" |
| `modules` | how many DLLs were mapped in |
| `ours` | names of our files (from the record's file list) that are loaded **from our folder** |
| `elsewhere` | paths of the same name loaded from another folder (at most 8) |
| `missing` | names of ours, not loaded |
| `graphics` | which of `d3d12 d3d11 d3d10 dxgi d3d9 d3d8 ddraw opengl32 vulkan-1` the process holds, from any folder. **Present only when the list could be read** (a record without the key says "not looked at", not "none") |

The rules that make "ours / elsewhere / missing" mean something
(`watch.py:306-374`, each born of a wrong verdict):

- Our file list is the record's `files`, reduced to **what the game process can
  load**: names ending `.dll .addon64 .addon32`, **never anything under
  `host64\`** (it belongs to the helper's process). A game list is matched by
  file name, case-insensitively, and a hit counts as ours only if the loaded
  file **is** our file (same real path after normalisation).
- Ours loaded **and** a same-named copy loaded from elsewhere: ours wins (a
  ReShade `dxgi.dll` loads System32's `dxgi.dll` after it; that is the chain
  working, not "ours is never reached").
- A same-named file loaded from elsewhere with none of ours loaded:
  `elsewhere` (the answer to "everything is in place and nothing happens").
  Exceptions that never count as elsewhere: a file we keep **in a subfolder**
  (OptiScaler's `D3D12Core.dll`) when the only hit is under the Windows folder
  (and then the name goes in neither list); an `nvngx*` runtime loaded from
  anywhere but System32 (the game's own DLSS, loaded by path; only System32's
  can get in front of ours).
- A name loaded **nowhere** is `missing`. Not every missing name is a fault:
  the diagnosis only treats a short list as essential (`nvngx_dlssnr.dll` once
  the model has run, add-ons, the proxy, DXVK's DLLs on a DXVK install);
  everything else loads on demand or never by name.
- A game under a `bin\` or `bin64\` subfolder (a Source game's `d3d9.dll`)
  counts as at the root for the "Windows' copy is a conflict" rule.

**Why a module list can be refused** (`watch.py:185-225`):

| Cause | Text the person sees |
| --- | --- |
| access denied opening the process | protected: an anti-cheat or a store build will not be opened |
| access denied or partial-copy listing it | protected / "the process was still starting, try again in a moment" |
| any other Windows error | "the DLL list could not be read (error N)" |
| not Windows | "not Windows" (tests) |

A refusal is recorded as `refused` and never as "nothing loaded".

Changed from the reference: **read the list more than once**, and keep the
union. One read at 8 s misses a game that creates its device late (a splash,
a launcher, a menu, a map load): the proxy then reads as missing and the
verdict blames the install. The proposed rule: read at 8 s, then every 20 s
while the process lives, at most 6 reads (about 2 minutes); keep every name
ever seen in `ours`, the latest `graphics`, a `refused` only when **no** read
succeeded, and `elsewhere` only for names that were never seen from ours.
It costs a handful of reads per start. The diagnosis needs a rule for a
sighting that changes between reads: see Open questions.

Where it is kept: the tool's own data folder (never a game folder: one more
file for an uninstall to take back out and for a launcher's file check to
object to), at most 40 folders, the newest kept. A sighting older than the
install (minus 60 s) is not evidence about it and is ignored.

Do not port: `settle()` (`watch.py:447-484`), which re-reads old records "with
the rules inspect() uses today" for records written by a 1.9.0 build. The
rewrite has no old records.

### The answer after a session

When `closed` arrives and nothing is rewriting the folder, on a worker (never
the UI thread):

1. Run the one diagnosis entry point ([diagnosis](diagnosis.md), `Analyse`)
   with the install's last failure, **passing the Windows crash record and the
   sighting** so the answer comes back already corrected for a fault, a
   person's answer, and the closing-fault exemption.
2. Measure the session (the cost, [diagnosis](diagnosis.md)) when the diagnosis
   says the game `Ran` and the route has the work-area dial.
3. Work out the offer: the verdict's class says whether another route may help
   ([diagnosis](diagnosis.md), "Try another route"); if so the **plan** supplies
   the next route and the reason.
4. Show it:

| Where | What |
| --- | --- |
| the log | `=== <game> closed: <verdict> ===`; an info line when a fault was seen but not counted (the closing-fault rule), with the note that explains it |
| a toast (15 s) | title "<game> closed"; text "working", or the one-line reason for the next route, or the first words of the verdict, or "it crashed in <module>" (adding the reason when there is an offer); actions: **try <route>** (when offered), **open** |
| the tray (window hidden) | the same title and text, plus "open the tool to try <route>"; opening the tray lands on this game with the same answer and offer |
| the library row and the game page | the session card (below) |

5. Keep it: the verdict id and short text, whether it was ok, the frame rate
   when measured, and the offered route and its reason, **per game**, so "try
   <route>" survives a restart. Every other writer (install, uninstall,
   autopilot, "did it work?") replaces or drops that entry and the offer with
   it.

**"Did it work?"** (the manual press, on the game page) runs the same
`Analyse` and the same measurement on the same logs; the only difference is
that it also runs while the game is up and prints the full findings. A
press while the game is running reads the logs as they are, and the
diagnosis's "unfinished session" rules apply; it does not take a sighting.

Changed from the reference: the watcher's close and the manual press **share
one implementation** end to end. The reference had two (`ctl_watch._on_lookdiag`
and `ctl_game._windows_crash` + `crash_overrides`) with different wording and
conditions, and the card measured the session at a work area of "100" when
unconfirmed while the page measured with the slider's value
(`ctl_watch.py:240`, `ctl_game.py:2459`). Here both say "work area unknown"
when the add-on's config does not confirm it ([diagnosis](diagnosis.md)).

Changed from the reference: whether the session is `ok` is read from the
verdict's **outcome** (`worked` + no counted fault), not from the text starting
with "Working" (`ctl_watch.py:335`, `ctl_game.py:2546`, `2578`). A reworded
verdict must not turn a success into a failure.

### The session card

One card per game, on the game page and in the library row. Pure data in, no
window needed to test a state.

| State | When | Headline | Detail |
| --- | --- | --- | --- |
| `running` | the watcher saw the game start and has not seen it close | "<game> is running" | nothing |
| `ok` | outcome `worked` and no counted fault | the verdict | the measurement line, the first findings |
| `failed` | outcome `failed` | the verdict | findings, the offered route and why |
| `unsure` | outcome `unknown`, or `not-a-result` | the verdict | findings; **never called a failure**: the logs cannot tell and the person watched |

Rules:

- `running` outranks a stored verdict; a "did it work?" pressed **after** the
  start outranks `running`. The "running" state is cleared when watching is
  switched off or the folder stops being watched (nothing would send "closed").
- The card says something only for a diagnosis result; for anything else
  (an install, an uninstall) there is no card.
- The measurement line (`ok` only): "<n.n> ms of model a frame" (when the model's
  cost is known) or "<n> fps"; then "<n>% work area" **only when the add-on's
  own config confirmed it**; then "<n> frames" only for the feeder (the cost
  lines of OptiScaler count readings, not frames, and are never printed as
  frames). Separated by a middle dot. Nothing when nothing was measured.
- `failed` shows the offered route and its one-line reason only when one was
  kept.

### Autopilot

"Install, start, read what the game loaded, and if none of ours is in, go to
the next route." One pass, three routes at most, only on request: the person
presses autopilot, or presses **try <route>** after a session said another
route may help (that continues the plan without asking again).

**What it asks first** (once, on the press): it is experimental; it will
install the first route; start the game itself (or ask the person to, with the
reason); read which DLLs the game loaded (read only); and, if ours are not in,
wait for the game to close, install the next route and go round again, "without
asking again, stopping after those or when you press stop". It names the
routes in order. A game with an anti-cheat asks one more question first
("go on anyway"). "Try <route>" asks nothing: the person already agreed to a
pass.

**The plan**: see "Try another route" in [diagnosis](diagnosis.md): the
recommended route first (only if offered), then the rest by shared results,
without the routes never tried automatically, at most **3**
(`autopilot.py:44`). The order and why are logged ("<route>: <why>") when other
people's results put it there. Each route is installed with **its own
settings** (an OptiScaler dial or a Remix runtime swap belongs to its route);
the reference's first version used the first route's settings for all three and
installed the second and third as weaker versions of themselves.

**May the tool start this game?** Never a guess dressed as a yes. When not,
the person is told what to press themselves (it is what they would have done).

| Refused when | Why it matters |
| --- | --- |
| no exe picked | nothing to start |
| the folder cannot be read to look for an anti-cheat | the cost of a wrong guess is a ban against one button press |
| an anti-cheat is present (names it and the evidence) | a game started by another program can be read as tampering |
| the game is already running (the same exe, or any exe in the folder that looks like a game, excluding `host64\`) | never a second copy |
| the exe is a launcher, not the game | it would start the wrong thing |
| this tool runs elevated, the route reaches the game through a per-user Vulkan layer, and no layer is on the machine's list | a game it starts is elevated too and gets none of the per-user layers: DXVK would run, ReShade not, and the route would be recorded as failed |

Otherwise the game is started from its exe, working directory its folder
(a store or launcher game gets one more line: "if <store> wants to own the
launch, start it there instead - the watching is the same either way"; a game
added by hand or an emulator does not).

**One attempt** (a route):

1. Install. An install that stops is **this route's answer**, not the
   pass's: the next route is a different download and set of files. The one
   exception is a file the game holds open (the install is refused write,
   "is running", "being used by another process"): wait for the game to close
   (up to **600 s**, checking every 2 s, saying "close the game and this
   carries on with the next route" once); if it never closes the pass stops
   with "the game is still running - the files it has open cannot be replaced
   while it is up". An install that left **no record** stops the pass at once,
   with the message that says how to undo it by hand (the next route would
   take the files for the game's own and back them up).
2. Start the game (above) or say "start the game now - watching for it".
3. Wait for the process, up to **300 s** (a cold start from a hard disk with a
   launcher in front is slow), then let it settle and read the module list
   (the watcher's sighting, written down as the sighting).
4. Never seen: stop the pass ("the game was never seen running"), because
   a rerun learns nothing.
5. Seen: the attempt **loaded** when ours are in the process and **none is
   shadowed**. A name loaded from elsewhere while ours sits beside the game is
   the very fault the pass looks for; ours being loaded too does not undo it.

**After an attempt**: loaded: stop ("loaded", with the file names, at most 3
and "N more"). Not loaded and a next route exists: wait for the game to close
(600 s) and go round again. No route left: "every route tried".

**The summary line** (one):

- ok: "The <route> install is loaded in the game (<names>). Play for a few
  minutes, then press 'did it work?'."
- nothing tried: "Nothing was tried."
- stopped for a reason of the machine's (the game would not close, the
  install did not finish, nobody started it, you pressed stop): "Stopped:
  <reason>." plus where things stand (below) plus the launch advice only if
  the game was never started;
- every route tried: "Tried <routes> - <the game loaded <name> from another
  folder, so the copy beside it is never reached | nothing of ours ended up
  running in the game>. Use 'report a bug' in the help menu: what the game had
  loaded is recorded and goes into the report." plus where things stand.
- where things stand: "The <route> route is what is installed in the folder now
  - 'uninstall' takes it back out", or "The folder's record says a <route>
  install is in it", or "Nothing of ours is in the folder". A failed fresh
  install that was rolled back is not an install.

**After the pass** the plan is remembered (routes, and the routes tried so far:
the earlier ones from a continued pass plus these), so when the game closes
and the logs say the route that loaded still did not work, the watcher offers
the next. What the card and page said before the pass described files that are
gone and is dropped. The page's route follows the install that was left.

**What it never does**: decide the picture is better (that needs frames,
a log and somebody looking: "did it work?" afterwards); start a game with an
anti-cheat or a launcher exe; keep going past three routes ("a loop that
reinstalls all night is not autopilot, it is a fault"); write into a game
folder itself (the install does; the watching only reads).

Changed from the reference: the pass takes **typed outcomes from the install**
(cause: network, file in use, disk full, no record), not substrings of the
failure text (`autopilot.py:478-481` matched `"refused to write"`,
`"is running"`, `"being used by another process"`). A reworded exception
message must not change what the pass does.

Changed from the reference: **a refused module list is not "the game loaded
none of our files"**. The reference reads an attempt's `ours`/`elsewhere`
lists without looking at `refused` (`autopilot.py:398-399`), so a protected
process that got past the anti-cheat check (a store build) is logged as "the
game ran and loaded none of our files" and the pass installs the next route
over a result that was never read. The attempt says "could not read the
process" and the pass stops (the next route cannot be judged either).

Changed from the reference: the **sighting rule is the watcher's** (the union
over several reads), so a game that creates its device late is not judged
from the first 6 s (the autopilot's own `settle=6`, `watch.py:649`, vs the
watcher's 8 s).

### "Aim for"

A setting, "aim for" (frames per second, 20 to 480, empty = off; anything else,
including a hand-edited value that does not parse, is "off"). It turns what a
session cost into a work-area setting for the **next** session. Nothing is
changed during a game: both add-ons read their config at start-up, so between
sessions is what can honestly be done.

After a session that `Ran` and was measured ([diagnosis](diagnosis.md), "Cost
and frame-rate measurement"):

1. **Remember** the sample (game, route, work area) **only when the add-on's
   config confirmed the work area** (an unconfirmed one is shown, never stored).
2. **Print the cost table** ("what the work area costs here"): rows for 50, 75,
   100 % and the session's own, marked "(this session)": each row `k·r²` ms
   "of model" (OptiScaler) or "of model and feed" (feeder), plus an fps when the
   base is known. Needs a split: see below. On the OptiScaler route: "this route
   writes down what the model cost but not your frame rate, so there is no fps
   here - only the cost of the dial itself". With an unconfirmed work area
   a line says the work area above is the slider's.
3. With no target: "set 'aim for' in settings and the next session turns this
   into a setting, not just a number to read." (when a table was printed).
4. **With a target**, **suggest** a work area (clamped 25 to 100):

| Situation | Suggestion |
| --- | --- |
| feeder, two comparable sessions (areas at least 5 % apart, `k > 0.05`, `base > 0`) | solve `r = sqrt((target_ms - base)/k)`, rounded: "N % should land on T fps" (or "already the right setting") |
| ... and `target_ms <= base` | no change: "even at the smallest work area this game does not reach T fps here - about F fps with everything the work area drives taken out - so the work area is not what is holding it back" |
| feeder, one session or not comparable, fps below 97 % of target | a step down by **15** points, "a step rather than a calculation", and why a second session at another work area is what the arithmetic needs (or that the recorded sessions are too alike) |
| ... fps above 115 % of target and the work area below 100 | a step up by 15 |
| ... otherwise | "that is your target, nothing to change" |
| OptiScaler (model cost only) | aim at **a quarter** of the target frame for the model: `r = current * sqrt(budget/model_ms)`; says the share the model took, says plainly that a quarter is a rule of thumb, "not a measurement of the fps you will get"; one reading is "play a little longer for a steadier number" |

5. When the suggestion differs from the session's work area, an **apply**
   button offers it; apply writes the setting where the add-on reads it
   (OptiScaler's `[DlssNr] WorkingScale` as a fraction rounded to 3 places,
   or the feeder's `dlss5-feed.cfg work_resolution` as a percent), and says
   "set to N %. It is read when the game starts, so it applies to the next
   run - play again and press 'did it work?' to see where it landed". On
   failure it says the same value is written by pressing install.

**The history** is per (game folder, route, work area), newest wins, **8 samples
per game, 60 games**, and it is per route: a frame rate from the feeder and a
model cost from OptiScaler are two different measurements, and a session on one
used to overwrite the other's point.

Changed from the reference: **least squares over all the recorded points** for
the feeder's `frame_ms = base + k·r²`, not the lowest and highest only
(`autotune.py:337`): a third and fourth session now improve the answer. Same
result at two points; same plausibility rules (`k > 0.05`, `base > 0`; a fit
that fails them is "step, not calculation"). The spread rule is the fit's range,
not the endpoints'.

Changed from the reference: **the history belongs with the install**, not in
the tool's settings file: it is read and written whole on every diagnosis, and
60 games of it is the file's biggest part. A record under the app data folder
keyed by the game's id. Uninstalling a game forgets its history.

Changed from the reference: **the OptiScaler suggestion is labelled an
estimate in the control itself** ("rule of thumb"), and the quarter is a named
setting with that default. The reference printed the rule only in a log line.

### Compare

"DLSS 5 on vs off": two screenshots made by ReShade become one side-by-side
picture to look at and to post.

- **Where screenshots are**: ReShade's `[SCREENSHOT] SavePath` in
  `ReShade.ini` (a relative path is against the game folder; empty = the
  game folder), plus the game folder itself. Only the top level of each is
  read (game folders hold hundreds of thousands of files and ReShade never
  nests). Image types `.png .jpg .jpeg .bmp`. Files this tool wrote (the
  compare export prefix) are never offered as a "before".
- **When it was taken**: the stamp in ReShade's file name
  (`<game> yyyy-mm-dd hh-mm-ss[_n]`, with a space or an underscore before the
  time) wins over the file's modified time, since copies and cloud sync rewrite
  the time. A name with an impossible date falls back to the modified time.
- **The pair**: the two **newest** shots whose times are at most **5 minutes**
  apart ("toggle, shoot, toggle, shoot" fits; further apart is more likely two
  unrelated sessions), returned **older first** (the first shot in the flow is
  the "before"). None: nothing to pair, and the page says how to take them.
- The window shows the pair with two labels ("before"/"after") and a swap
  control for the other habit, and the **hotkeys to use**: the screenshot key
  from `ReShade.ini [INPUT] KeyScreenshot` (`vk,ctrl,shift,alt`: a name such as
  "Ctrl + Print Screen"; `vk=0`: "not bound - set one in ReShade's settings
  tab"; nothing: Print Screen), and the toggle (F6 on the feeder, F5 on native
  and bridge).
- **Export** writes both side by side, the first on the left, into one image
  named `dlss5_compare_<yyyy-MM-dd HH-mm-ss>`.
- **Key names**: virtual-key code to key cap, as `compare.py:38-79` (digits,
  letters, F1-F24, numpad, the punctuation keys by their US names, Backspace,
  Tab, Enter, Pause, Caps Lock, Escape, Space, Page Up/Down, End, Home, arrows,
  Print Screen, Insert, Delete, Num Lock, Scroll Lock), `key <n>` for others.

Changed from the reference: export **any format ReShade writes**, as
PNG. The reference opened only PNG (`is_png`) because its toolkit's codec was
the limit, so a BMP or JPEG pair could be shown and not exported. Avalonia's
image stack reads all four.

Changed from the reference: **do not write the export into the game folder**
(`compare.py:267-269`). The game folder is the one place a tool's file is
one more thing for an uninstall to remove and a launcher to verify (the
reference says exactly that about its own sightings). Default to a folder
named after the tool under the person's Pictures, with a save dialog that
remembers the last place. The "ours" prefix is still excluded when scanning
the screenshot folder.

Changed from the reference: the per-side size cap (1 920 px wide, a limit of a
pixel-copy loop in a toolkit that "feels like a hang" above it) is **not a
product rule**; keep a cap that bounds memory (the value is open, below), and
say in the window when an image was scaled down.

## What must never happen

- **Starting a game this tool may not start** (anti-cheat, launcher exe,
  already running, elevated against a per-user layer) or starting one the
  person did not agree to: autopilot only runs on a press, "try <route>" only
  continues a pass already agreed to.
- **Reading a half-done install as a session's answer**: a close that arrives
  while an install, uninstall, autopilot or DLSS update runs on the folder
  does not answer.
- **Treating a refused module list as "nothing of ours is loaded".**
- **Counting our own helper process (`host64\`) as the game, or its files as
  the game's**, in running state, in "ours/elsewhere/missing", and in the
  "already running" refusal.
- **Calling a session a failure when the logs cannot tell** (`unsure`).
- **Offering a route for a verdict about how the game was started or set up**,
  or for a game never seen running (diagnosis).
- **Publishing or storing a measurement whose work area the config did not
  confirm.**
- **Looping**: more than 3 routes, an unbounded wait, or an install repeated
  after a refusal that cannot change.
- **Writing into a game folder from the watcher, compare or aim-for reading**
  (the install writes; apply writes the add-on's own config, which is the
  install's file).
- **Changing a setting during a game** (aim-for applies at the next start).
- **A toast or notification that names a route the plan does not hold**, or
  keeps offering a route already tried.
- **Showing a card state from a different game** (a pass waits for the person
  to close the game, so another game's page is often open when it ends: a
  pass's result belongs to its own game).
- **A watcher that dies silently or takes the app down**: an exception in a
  tick is logged and the next tick runs; the "watching N" state is only
  shown while something is watching.

## Bugs and questionable choices in the reference not to port

- **`ran(rep, seen=True)`** (`ctl_watch.py:245-255`): the default `seen=True`
  makes "the game ran" true on every close, so the guard it was written for
  (never offer a route for a game that was never seen) cannot refuse anything
  on that path; combined with "Installed after the last run - play once"
  being a route-failed verdict (diagnosis), a game closed in 20 s over logs from
  before the install gets "try another route". Pass the watcher's actual
  knowledge (the sighting) and the session's length; offer nothing for `NeverRan`.
- **The verdict as the key** for "ok" and for the card state
  (`startswith("Working")`, `verdicts.outcome(title)` on a stored title).
  Use ids and outcomes.
- **Two copies of the crash correction** (`_on_lookdiag` vs the page's
  `crash_overrides`), one adding a "play once" case the other lacked, and
  the report printing "Working." beside a fault record for months (#294
  #295). One call into `Analyse`.
- **Two watchers (`Recorder`, `Lookout`)** and three different settle
  times (8, 8, 6 s).
- **A "started" read at a single instant**: late devices read as missing
  (see "Read the list more than once").
- **Played time from poll ticks** (4 s of error at each end).
- **Plan memory as a dict of lists in a settings file, keyed by the str of a
  path** (`prefs["autopilot_plans"]`, `["last_verdicts"]`,
  `["autotune"]`): "a hand-edited entry that is not a plan is dropped, not
  raised on" is a symptom. Typed records in the app's data store keyed by game
  id.
- **`full_plan = list(dict.fromkeys(kept + planned)) if not ask else list(planned)`**
  (`ctl_game.py:1647`): a continued pass merges the stored plan with a fresh
  one, so the order the person was shown can change between two presses.
  A continued pass is the stored plan's remainder, unmodified.
- **The autopilot's "loaded" test is a name match only** (a file in the
  process): a route can "load" and still be dead (the diagnosis is what knows).
  It is a cheap early exit and must say so: "loaded" is never worded
  "worked" (the summary already tells the person to play and press "did it
  work?"; keep that).
- **Substring matching on install errors** (above).
- **Hard-coded `10 s` `TRY_WAIT` for the page to be read** (`ctl_watch.py:460`)
  with a polling `after(200)` loop and a token to cancel it: the "try"
  continues from a typed state of the game page ("support read"), not from a
  timer.
- **`_card_measure` measures at 100 % when unconfirmed** and the page at the
  slider's value: one rule (unknown).
- **The first autotune step of 15 points** is not derived from anything;
  keep as a named, documented default, not a magic number.
- **Compare's export in the game folder** (above) and **PNG-only** (above).

## To verify against primary sources

1. **Enumerating a process's modules from a 64-bit tool**, including a 32-bit
   game (`EnumProcessModulesEx` with `LIST_MODULES_ALL`), the access rights
   it needs (`PROCESS_QUERY_INFORMATION | PROCESS_VM_READ`; the limited
   right is enough only for the image path), and what a protected process
   returns (`ERROR_ACCESS_DENIED`), a starting one (`ERROR_PARTIAL_COPY`).
   Source: Microsoft docs. And whether `System.Diagnostics.Process.Modules`
   in .NET is enough (it fails across bitness); if not, a P/Invoke or the
   ToolHelp snapshot with `TH32CS_SNAPMODULE | SNAPMODULE32`.
2. **A process's start and exit time** from the OS (`GetProcessTimes`, or the
   .NET `Process.StartTime`) and what it costs on a protected process.
3. **Process-start notification** as an alternative to polling (WMI
   `Win32_ProcessStartTrace` needs administrator; ETW kernel process events
   too). The proposal keeps polling; confirm 4 s is cheap enough with a
   snapshot of several hundred processes.
4. **The image path of a process the tool cannot open**
   (`QueryFullProcessImageName` with the limited right) and that a protected
   game still reports its image.
5. **Whether a game started through a store or launcher is a child of the
   launcher** (parent id) and where its image lives: that the "image path
   under the install folder" test sees it (the reference's #191, #682 cases).
6. **That an elevated process does not get per-user Vulkan layers** (see
   [diagnosis](diagnosis.md), item 5): the refusal in "may the tool start this
   game" rests on it.
7. **Starting a game from a .NET process** (`Process.Start` with the working
   directory, `UseShellExecute`) and the token the child inherits (an
   elevated tool starts an elevated game): see Open questions for a
   de-elevated start.
8. **ReShade's screenshot settings**: the `[SCREENSHOT]` keys, the default
   file name pattern and its time stamp, the formats ReShade can write
   (PNG, BMP, JPEG, and anything newer), `KeyScreenshot`'s encoding
   (`vk,ctrl,shift,alt`), and that a relative `SavePath` is against the game
   folder. Source: ReShade's `reshade.ini` template and source.
9. **The on/off hotkeys** (F6 feeder, F5 native/bridge): they are the add-ons'
   own settings, to be read from the add-on's source or its config rather than
   hard-coded.
10. **That NGX / OptiScaler read their config only at start-up** (the premise of
    "applies to the next run"). Source: each project.
11. **Windows tray (notification area) and toast notifications** from an
    Avalonia app; a hidden-to-tray window; what happens to a notification
    clicked after the app quit. Source: Avalonia / Windows App SDK docs.
12. **What "frame time = base + k·r²" means physically** for the feeder: that
    cost grows with the area of the work resolution is the reference's model,
    confirmed only against its own sessions. Measure it on several games before
    promising anyone a frame rate.

## Fixtures

The reference ships no unit tests for these parts (its `tests/` holds only
fixtures and guards), so everything below is new work, in this order of value.

- **Sighting classification** (pure): a table of cases `{ours file list,
  install folder, module list (full Windows paths), refused?}` to
  `{ours, elsewhere, missing, graphics}`. Cover: ours loaded + System32 copy
  loaded (ours wins); only System32's loaded (elsewhere); a name in a
  subfolder with only a Windows copy (neither list); an `nvngx_dlss.dll` from
  `streamline\` (not elsewhere) vs from System32 (elsewhere); two copies of one
  runtime under the install (either is ours); the helper's `host64\` files
  (never counted); a `bin\` layout; mixed-case and `/`-separated paths
  (**Windows path rules on every host**: the tests run on macOS and Linux);
  a refused list (`ours` empty and `refused` set, never "missing"); a list with
  graphics DLLs (`graphics`) and without (`graphics` absent).
- **The watcher** (fake process table, fake clock): a game that starts and
  closes (events, order, minimum play); a start that never settles; a close
  shorter than 20 s unread; a launcher in the folder that starts the game in
  another folder (the child counts); the helper outliving the game; a folder
  being rewritten (no event answer); watch toggled off with a running state.
- **The answer after a session** (fake `Analyse`): `worked` plus a counted fault
  becomes a failure; `unknown` is `unsure`; a closing fault is shown and not
  counted; an offer exists only for route-failed verdicts of a session that
  ran; the plan continues from the route, does not repeat a tried one, ends at
  three; kept entry survives and is replaced by an install.
- **The card** as a table: state x measurement x offer to text (each
  measurement shape: model ms only, fps with frames, unconfirmed area).
- **Autopilot** (fake hooks for install, start, wait, closed): the refusal
  table, one case each; an install that stops (next route); a file in use
  (wait for close; never closes); no record (stop at once); never seen (stop);
  loaded; loaded but shadowed (next route); three routes then "every route
  tried"; stop pressed; the summary text for each end; the plan order with an
  empty shared list (offered order) and with a ranking.
- **Autotune**: log lines to measurement (the two shapes, decimal comma,
  `1.2.3` refused, the settled line = the last); fits from two, three and four
  sessions (the least-squares fit, a `k <= 0.05` case, a negative base);
  the suggestion table above, every row; the cost table rows; `shared()` (the
  15 % spread rule, unconfirmed area gives nothing); the history limits (8
  samples, 60 games, newest wins per (route, area)); the target parsing
  (empty, 19, 20, 480, 481, 1e999, "abc").
- **Compare**: file-name stamps (valid, impossible date, `_2` suffix), the pair
  rule (two within 5 minutes, two 6 minutes apart, ours excluded, a screenshot
  folder outside the game folder, only top level), `SavePath` forms (empty,
  relative, absolute, quoted), key names (every table entry, `vk=0`, modifiers,
  an unknown code), the export (two images side by side, a third format).
- **Real logs for the measurement** come from the diagnosis corpus
  ([diagnosis](diagnosis.md), Fixtures): its feeder reports carry the
  `frames: feed CPU` line and its OptiScaler reports the `cost`/`elapsed`
  lines.

## Open questions

1. **Several reads of the module list** (proposed above): is the 8 s + every
   20 s up to six reads right, and what should the diagnosis do with
   disagreeing reads (a name loaded in one read and gone in the next is
   evidence the game unloaded it; a late-loaded name is evidence the first read
   was early)? The proposal is "ever seen wins" for `ours`; confirm.
2. **Process visibility**: should the watcher also match a game by **exe name
   anywhere** (a launcher that copies or starts the game from another folder)?
   The reference sees only images under the install folder and children of such
   a process, and the diagnosis has a separate verdict for "the same exe ran from
   another folder". Matching by name would cost false positives (a same-named
   exe of another game) and a module-list read of an unrelated process.
3. **De-elevated start**: when the tool runs elevated and the route goes
   through a per-user layer, the reference refuses to start the game. A start
   with the shell's normal token (the Explorer token, or a scheduled task)
   would make it unnecessary. It is a security-relevant design (starting a
   process on behalf of an elevated one); not specified here. Ale to decide.
4. **The minimum play (20 s) and the 8 s settle** are the reference's, from a
   few cases (a launcher hop, a crash at launch). Measure on real games; and is
   "a close under 20 s and never read gives no answer" the right call, or
   should it say "it closed within 20 seconds" (a more useful sentence for
   a crash at launch)?
5. **Notifications**: the toast is in-app, the tray notice only when the window
   is hidden to the tray. Should a Windows toast show when the window is
   minimised or behind the game? (The person is in a full-screen game when the
   close arrives; the card is waiting when they come back.) Wording and
   whether to ask permission.
6. **The history store** (move out of settings): per game id, or per folder?
   A game moved to another drive would lose its history under the folder key.
7. **A quarter of the frame** (OptiScaler suggestion) is a rule of thumb the
   reference chose. Keep, make it a setting, or drop the suggestion and print
   only the cost table?
8. **The step size of 15 points** and the 97 % / 115 % bands for a first
   suggestion: keep, or derive?
9. **Compare's pair rule** cannot know which shot is "on": the order of two
   shots 20 s apart is the person's habit. The reference offers a swap. Is
   that enough, or should the tool ask the add-on (a toggle marker in its log
   with the time) so the labels are known? That needs the add-ons' own
   output; not specified.
10. **Compare's memory cap** per side (the reference's 1 920 px was a toolkit
    limit): choose one.
11. **Autopilot's early exit** after the first loaded route is a reference
    choice: the person plays, and the watcher's close gives the next
    suggestion if it did not work. The alternative is a pass that continues
    after a close, reads the logs, and tries the next route itself. The
    reference's choice is the cheaper and the safer (no unattended
    reinstalls); confirm it stays.
12. **Whether the compatibility list's per-game route ranking is available**
    when this ships: until then the plan is the offered order
    ([diagnosis](diagnosis.md)), and the "why" lines are not produced.
