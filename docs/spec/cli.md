# The command line

The command line is a second, console-subsystem executable in the same
release, sharing Core with the app. Changed from the reference: one
window-subsystem exe attached to the parent console, so the shell prompt
returned before the output and redirection was awkward. With no arguments
it prints the usage; with a game folder it installs into that folder, reads it
(`--check`) or removes (`--remove`) without a window, printing what it reads and
does, so it can be scripted and pasted into a bug report. Read from `dlss5_autopilot.py` (the whole file, `1-474`), the
README's "Command line", and the old tests around line 25492
(`d5801a3~1:test_all.py`). The command line decides nothing of its own: it
runs the same detection, route choice, plan and install as the window; what it
prints is the contract of those areas (see the detection, routes and install
specs), and this spec fixes only the shape.

## What the user gets

### The grammar

```
invidia-claritatis.exe                                 the usage text, exit 2
invidia-claritatis.exe "D:\Games\Game"                 install
invidia-claritatis.exe "D:\Games\Game" --check         read and report, write nothing
invidia-claritatis.exe "D:\Games\Game" --remove        uninstall
invidia-claritatis.exe --help | -h | --version
```

| Option | Applies to | Meaning |
| --- | --- | --- |
| `--route <name>` | all | one of `native upstream optiscaler renodx bridge feeder standalone uplift remix`; it must be among the options detection offers for this game, else `error: route '<name>' is not available for this game (options: <list>)`, exit 1. It replaces the recommendation, and the recommendation's reason and outlook are not printed (they describe a route this run is not taking) |
| `--dxvk` / `--no-dxvk` | DirectX games DXVK can translate | run on Vulkan through DXVK, ReShade loading as a Vulkan layer; the default is automatic (games known to need it, such as MGS V, get it); `--no-dxvk` turns the automatic choice off |
| `--remix-swap` | `--route remix` | replace a Remix runtime that has no neural pass with the community build (experimental; the remix spec) |
| `--vr` | ReShade routes, 64-bit | also register ReShade's OpenXR layer (the vr spec); unverified with a headset |
| `--no-gpu-pref` | machines with an NVIDIA card and another GPU | leave Windows' graphics setting alone; by default the game and the feeder's helper are set to High performance, a choice already made is kept, `--remove` takes it out |
| `--opti-build <key>` | `--route optiscaler` | another OptiScaler build than the default: `y4my4my4m` (multi-pass, multi-frame generation), `wilsjo2` (neural pass before the upscaler, 1-3 passes; installed with that placement on, which is off in the fork's default), `wilsjo2-mfg` (the same from the fork's RTX 40 multi-frame generation package; refused on a card known not to be RTX 40), `janblade` (a fork of wilsjo2's, same placement), `jlrouzies` (wilsjo2's, with the neural-rendering settings in `OptiScaler.ini` applied while the game runs). An unknown key: `error: --opti-build takes one of: <keys>`, exit 2. With another route: `note: --opti-build applies to --route optiscaler only; ignored here` on stderr |
| `--feed-helper` | `--route feeder`, 64-bit | run DLSS in `host64\dlss5-feed-host64.exe` instead of the game's own process, for a game whose process cannot start it (feeder 1.18.0 and newer) |
| `--own-fg <dll or name>` | `--route optiscaler`, DirectX 12 | frame-generation files the user downloaded: `dlssg_for_sm86` (its `version.dll` with `dlssg_sm86.ini` beside it, or one of its `alternatives\` proxies with the ini copied beside it) or DLSS Enabler (its `version.dll`). The first time, the path of the dll; the tool keeps a copy, after which its name is enough (`sm86 sm86-winmm sm86-dxgi sm86-d3d12 sm86-dbghelp enabler`, any case). Experimental, and out again on `--remove`. Not given a value: `error: --own-fg takes the path of the .dll you downloaded, or a name you used before`, exit 2; a name with no stored copy: `error: --own-fg: no copy of <set> is stored yet - give the path of the .dll you downloaded`, exit 2; an unrecognised file: `error: --own-fg: <reason>`, exit 2. On another route: `note    : --own-fg applies to --route optiscaler only; ignored here`; on another API: `note    : --own-fg applies to D3D12 games only (<api> here); ignored` |
| `--help`, `-h` | | the usage text, exit 0 |
| `--version` | | the tool's version alone, exit 0 |

- The first non-option argument is the game folder; the executable is chosen as
  the window would (`games.manual`). No executable: `error: no executable found
  in <folder>`, exit 1.
- The tool **does not touch the network** to decide: the shared list is read from
  the cache only, so an offline run prints the same lines as an online one minus
  the `shared` line. Installing downloads as always.

### What it prints (install and `--check`)

One `label : value` line per fact, labels padded to a column, in this order;
anything not applicable is omitted:

| Line | Content |
| --- | --- |
| `game    :` | the game's name |
| `gpu     :` | `<card> (<architecture>)  driver <version>`; with no NVIDIA card: for an AMD card `no NVIDIA card detected` and the indented AMD explanation; otherwise `no NVIDIA card detected - dlss5 will not run` |
| `exe     :` | the executable's path |
| `arch    :` | `<32-bit|64-bit>   API: <api> (<why>)` |
| `driver  :` | the driver warning for the route that will be installed, when the driver cannot run it |
| `shared  :` | the shared-results note for this driver and route (community spec), when there is one |
| `route   :` | the route's label; below it one indented line per option: `*` for the chosen one, the name padded to 11, `NOT FOR THIS PC - ` before an unusable one, and its note |
| | `this game ships its own DLSS (<up to 3 pieces of evidence>)`, or `this game ships FSR|XeSS and no DLSS (...)`, and the recommendation's reason when there is one |
| `outlook :` | `<stable|beta|experimental> - <why>` |
| `dxvk    :` | `yes - <game> closes itself when ReShade hooks it; the game will render on Vulkan and ReShade loads as a Vulkan layer (--no-dxvk to turn this off)` when DXVK is in use |
| `fg files:` | the stored set and its files, and the "untried" note, for `--own-fg` |

Then, in `--check`:

| Line | Content |
| --- | --- |
| `installed:` | `yes` or `no` |
| `status   :` | `ready`, or the reason the game cannot be set up (no executable; architecture or API unreadable, with the instruction to set them by hand; 64-bit DirectX 8), or the refusal (the RTX 40 multi-frame-generation package on another card; no card any route runs on; an own-fg set that does not apply) |
| `warning  :` | the launcher warning (the executable is a launcher), the frame-generation package's requirements, and the OptiScaler FSR frame-generation warning |
| `renodx   :` | the local RenoDX add-on found, or `will download from the mirror` |
| `plan     :` | the steps joined by ` -> ` |
| `outside  :` | the Windows graphics-setting change the install would make (`--no-gpu-pref` to skip) |

`--check` writes nothing anywhere.

For an install, after those lines: `\r  NNN%  <message>` progress, then `Done - N
files written.` (or `Installed, but no record was saved - N files written.` when the
record could not be written), then one line `  ! <warning>` per warning, then
the in-game advice by route:

| Route | Advice |
| --- | --- |
| remix | Alt+X, then Developer Settings Menu, then the Post-Processing tab: "Enable Neural Uplift (DLSS-NR)" |
| uplift | press the overlay key, open the GITC Uplift tab, tick Enable (it starts switched off); the card at the top says whether it works |
| feeder on a 32-bit game | press the overlay key, then turn on neural rendering in the DLSS 5 page (F6 toggles it); it drives the 64-bit helper; play borderless or true fullscreen at the display's own resolution; do not alt-tab to the helper's window; turn the game's own MSAA/SSAA off |
| anything else | press the overlay key (Insert for OptiScaler, otherwise the configured key, Home by default), then enable neural rendering in the DLSS 5 panel; turn the game's own MSAA/SSAA off |

### Uninstall

`--remove` runs the same uninstall as the window and prints each action
(`removed: ...`, `restored: ...`).

Every run, after its arguments are validated, calls install's start-up
cleanup of the old all-users Vulkan layer ([install](install.md), "Shared
registrations") and prints what it says. Run as administrator, it removes the
layer (`removed: our ReShade Vulkan layer from the all-users list`); otherwise
it prints how to.

**No prompts.** The command line never asks a question; everything that
needs an answer is a flag (`--remix-swap`, `--vr`, `--route`), so a script
never hangs on input.

**Ctrl+C** cancels the running job ([platform](platform.md), "Cancellation"):
an install rolls back, an uninstall finishes the entry it is on, and the
process exits with **130**. Changed from the reference: it had no handling,
and a Ctrl+C mid-install left a partial chain.

**Beside the app.** The CLI may run while the app is open. It takes the job
lock around every write and waits, saying so, while the app runs a job
([platform](platform.md)).

### Console and exit codes

- A console program: output and errors go to its own standard streams, in
  UTF-8, and redirect like any console tool's.
- Exit **0**: the action was done (or `--check` found the game ready, or
  `--help`/`--version`); **1**: refused or failed (no executable, route not
  available, install error, a refusal in `--check`); **2**: a usage error;
  **130**: cancelled with Ctrl+C.
  - Changed from the reference: `--check` also exits 1 when `status` is not
    `ready` for an ordinary reason (no executable found for the game, architecture
    unreadable): the reference returned 0 (`dlss5_autopilot.py:271`), so a script
    could not tell a ready game from an unsupported one.

## What must never happen

- `--check` writing anything: no file, no registry value, no download of a
  component.
- A destructive action from an unrecognised or misspelled option. An option the
  program does not know is an error, never ignored.
- An uninstall refused because the executable cannot be read now: it works from the
  install record alone.
- An install proceeding on a driver that cannot run any route without saying so
  (the `driver` line precedes the install; a command-line install used to proceed
  in silence).
- Output claiming a route the run is not taking (the recommendation's reason after
  `--route`).
- `--help`, `--version` or `--check` doing anything else when a path is also given.

## Bugs and questionable choices in the reference not to port

- `dlss5_autopilot.py:348-466`: the arguments are parsed by hand. A value is
  removed with `args.remove(<value>)`, which removes the **first token equal to it**,
  so `--route feeder` next to a game folder named `feeder` removes the wrong one;
  an option missing its value (`--route` last) is silently ignored; an unknown option
  (`--rotue`) is silently ignored and the run installs; `--check` with `--remove`
  checks; `--dxvk` with `--no-dxvk` takes the first; a second folder is ignored;
  `--help`, `-h` and `--version` are answered only when no folder is given, so
  `game --help` installs. Changed from the reference: a standard parser
  (System.CommandLine) with mutually exclusive options, one value per option and
  an error for everything else. Why: each of the above silently does something
  the user did not ask for.
- `dlss5_autopilot.py:354-365`: `--remove` first removes the all-users Vulkan layer
  (as administrator) **before** any argument is validated and before the folder is
  read, silently, wrapped in `except: pass`. Do it as part of the uninstall, after
  validation, and report failures.
- `dlss5_autopilot.py:272-278`: `--remove` is refused when the install checks
  (`check_supported`) fail, so a game whose executable can no longer be read cannot
  be uninstalled. The plan's "uninstall from the record alone" applies.
- `dlss5_autopilot.py:421-444`: `--video` (dropped with the video area) ran
  `video.prepare` before `--check` was considered, so `--video --check` wrote.
- `dlss5_autopilot.py:296`: progress is printed with a carriage return and padded
  to 60 characters, which turns a redirected log into one long line. Print a line
  per ten percent when the output is not a terminal.
- `dlss5_autopilot.py:76-97`: attaching to the parent console and rebinding
  stdout/stderr by `open("CONOUT$")` makes the shell prompt return at once and the
  output arrive after it. The console executable replaces it.
- `dlss5_autopilot.py:372-374,392-394,422`: `_console()` is called in some branches
  and not others, so an error printed before it is lost.
- `dlss5_autopilot.py:249-260,283-294`: the `--check` and install paths build the
  same options object twice with a copy of every field; one builder, so a new option
  cannot reach one path and not the other.
- The usage text lives in the module docstring and in the README; one source.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| `AttachConsole(ATTACH_PARENT_PROCESS)` from a GUI-subsystem exe lets a console receive the output, the prompt returns early, and redirection works only through the handles opened | Microsoft Learn `AttachConsole`, "Console handles"; a test from cmd and PowerShell |
| the usage conventions (exit codes 0/1/2, `--version` alone, `-h`) | the .NET System.CommandLine docs and the POSIX/GNU conventions it follows |
| Windows' High-performance graphics setting for a program (`UserGpuPreferences`) and its per-program scope | Microsoft documentation of Graphics settings; the install spec |
| that a window-subsystem build can show a console only by attaching, and that a second console-subsystem exe is the usual alternative | .NET `OutputType` documentation |

## Fixtures

- Argument vectors and the expected parse for each row of the option table, and for:
  a folder called `feeder`; `--route` as the last token; an unknown option; `--check`
  with `--remove`; `--dxvk --no-dxvk`; `--help` with a folder; a folder with spaces;
  `--own-fg sm86` with and without a stored copy; `--opti-build` with a bad key.
- Transcripts to compare the printed lines against, one per route family (the old
  tests at 25492 and the reference's outputs): a ready game, a game that ships DLSS,
  a game that ships FSR, an AMD machine, an NVIDIA machine with an old driver, a game
  with an unreadable executable, a Remix game, a 32-bit game with the feeder.
- A folder with no executable; one with only a launcher; one in a path with a
  junction.
- An install record to uninstall from while the executable has been deleted.

## Open questions

1. **Machine-readable output.** A `--json` for `--check` (the same facts, as an
   object) would make the command line scriptable; not in the reference. Worth it
   now or after the first milestone?
