# The tool updating itself

The tool asks GitHub whether a newer release of itself exists, downloads it
in the background, verifies it, and on one click replaces its own
executable and restarts into it. The previous build is kept for the new
build's first run so a bad build can be gone back from, and removed on a
later start. Read from the reference's `core/update.py`, `core/selfupdate.py`,
the update handlers of `core/ui/app.py` (`:125-135`, `:507-610`), the
release workflow (`.github/workflows/release.yml`), the README sections "Is
it safe" and "Code signing policy", and the old suite's update checks
(sections 40, #136, #148, #450 in the history before the `python-reference`
tag).

The updater is Windows-only in behaviour (it replaces an executable). On
the development hosts the check and the download and verification run and
are tested; the replacement sits behind an interface whose Windows
implementation is in `Autopilot.Windows`.

## What the user gets

### Discovering a release

- The release answered by `https://api.github.com/repos/<owner>/<repo>/releases/latest`
  (`update.py:21-23`, the repository of this tool, one constant). GitHub
  returns the newest published release that is neither a draft nor a
  pre-release. The tag, with a leading `v` or `V` stripped, is the version;
  the answer's `html_url` is the page to open (falling back to
  `https://github.com/<owner>/<repo>/releases/latest`).
- The request goes through the shared download path: when the API is rate
  limited or blocked, the same answer is built from github.com's release pages
  (see [net](net.md)).
- **The result is cached for 6 hours** (the version and the time, in the
  user's settings) because anonymous API calls are limited to 60 an hour per
  address and the installs need them (`update.py:32-50`). **check** with
  force skips the cache.
- **A failed check is silent.** No internet, an answer without a tag, a
  malformed answer: no notice, and the tool is never blocked
  (`update.py:42-43`, `:67-68`).
- **Newer** means the release's version is greater than the running one,
  compared as versions (below). An equal or lower version is never offered:
  the tool never offers a downgrade.
- The running version is one value, the build's own version.

Changed from the reference: the reference compares the digits of the two
strings as tuples (`update.py:26-29`). `2.1` against `2.1.0` reads as newer,
`2.2.0-beta1` (digits 2,2,0,1) reads newer than `2.2.0`, and a tag with
no digits reads `(0,)`. Parse both as semantic versions: a pre-release is
lower than its release, missing parts are zero, and a tag that is not a
version is ignored (no notice) and logged.

### What the user sees

- **Running as the built executable**, with updating left on (the settings
  key `auto_update`, true by default, because a download costs nothing until
  it is applied and the apply is gated on verified sums; `false` keeps it
  manual, `app.py:515`): when a newer release exists, a banner says "version `<new>`
  is out (you are on `<current>`) - downloading..." and the build is
  downloaded in the background. When the download and verification finish,
  the banner says "version `<new>` downloaded and verified" with a "restart
  into it" action. Nothing is replaced without that click.
- **Auto-update off**, the banner says "version `<new>` is out (you are on
  `<current>`)" with "update now". Pressing it asks "update and restart?" /
  "The new version is downloaded and the tool restarts into it. The current
  build is kept beside it as .old.exe for the new one's first run, so you
  can go back; it is removed when the new version starts a second time."
  with "update" and "not now"; on yes it downloads (progress as MB), then
  restarts.
- **Running from source** (not the built executable): the same banner;
  the button opens the release page in the browser and replaces nothing
  (`app.py:554-556`).
- **A failed background download** is logged as a warning and shown nowhere:
  the banner stays on "downloading...". Changed from the reference: the
  banner says "could not download version `<new>` - `<reason>`" with "try
  again" (the reference's text never changes, `app.py:513-520`).
- **Failure to apply** shows "update failed: `<reason>`" with "try again"
  and keeps the running build (`app.py:597-606`).
- **While a job runs** (an install, uninstall, autopilot, a DLSS update) the
  update does not start. Changed from the reference: it says "another job is
  running - the update waits for it" instead of ignoring the click silently
  (`app.py:541-542`).
- The window's size and position are saved before the restart, because the
  restart ends the process without the close handler (#641).

### Fetching the build

(`selfupdate.py:77-218`.)

1. Files from earlier updates are cleaned first (below).
2. A new empty staging folder is created. Success leaves it for the
   replacement to empty; any failure, including cancellation and a refusal,
   removes the folder.
3. **Choosing the asset.** The release's assets: the archive (a `.zip`), else
   a bare `.exe`; and the sums file named exactly `SHA256SUMS.txt`. The
   reference takes the first `.zip` it finds, else the first `.exe`. No
   buildable asset: "The release has no downloadable build."
4. **Download** through the shared path (HTTPS only, size limit, no
   downgrade to http, see [net](net.md)), with progress.
5. **Extraction** of the executable from the archive, safely: every member
   name is resolved under the staging folder and `..`, drive letters and
   absolute members are refused with "The release archive contains a path
   that writes outside the update folder - refusing it." before anything is
   written (#79). No executable in the archive: "The release archive
   contains no executable."
6. **Sanity of the build**: it is a real 64-bit Windows executable (MZ, PE
   signature, machine 0x8664); "The downloaded file is not a 64-bit Windows
   executable - refusing to install it." and its size is not implausibly
   small (the reference refuses anything under 4 MiB in total, because a
   real build is about 11 MB, `selfupdate.py:40`, `:184-187`). The release
   is a single-file, self-contained publish: one executable is simpler to
   replace and to verify than a folder, so the size floor is a constant of the
   build.
7. **Verification** (below).

### Verification: what the takeover requires

These replace the reference's behaviour; its rule is "check the hash when a
sums file happens to be present" (`selfupdate.py:193-217`). They are the
[net](net.md) spec's verifier contract applied to the tool's own release: the
**sums-file** kind for the archive and the executable, and the **Authenticode
signer** kind once releases are signed. This section says what each requires
here; [app](app.md) points to the same contract.

- **A release without `SHA256SUMS.txt` is refused.** The reference
  installs it after the shape checks alone (`:197`: "When it is missing ...
  the shape checks above still apply"), so removing the file from a release
  removes the check. Message: "The release has no SHA256SUMS.txt - refusing
  to install an unverified build."
- **The sums file must list the downloaded archive, by exact name, and the
  hash of the whole archive must match** (hash the file as downloaded,
  before anything is extracted). The reference hashes only the extracted
  executable (`:211`), after the archive was already unpacked, and finds the
  entry by name suffix (`_expected_hash`, `:221-227`).
  - The sums file's lines are `sha256sum` style: hash, whitespace, optional
    `*`, a path. The release workflow writes the executable as
    `dist/dlss5-autopilot.exe` and the archive as
    `DLSS5-Autopilot-v<version>-win64.zip` (`release.yml`, step "Hashes").
  - The entry is matched on the file name only, case-sensitively and
    exactly: `evil-dlss5-autopilot.exe` must not match
    `dlss5-autopilot.exe`. Duplicate entries for one name that disagree
    refuse the release. A hash that is not 64 hex digits refuses it.
  - When the executable is also listed, the extracted executable's hash must
    match its entry too.
  - Mismatch: "The downloaded build does not match the hash GitHub
    published (`<12 chars>...` vs `<12 chars>...`) - refusing to install it."
  - A sums file that cannot be read: "Could not read SHA256SUMS.txt for the
    release (`<reason>`) - refusing to install an unverified build." Not
    listing the archive: "SHA256SUMS.txt does not list `<archive>` -
    refusing to install an unverified build."
- **The sums file only proves the download matches what the release holds;
  it does not prove the release is the project's.** Whoever can replace the
  archive can replace the sums file, so two more checks apply:
  - **Authenticode.** Once releases are signed (the release workflow has the
    SignPath steps, off until the project exists, README "Code signing
    policy"), the new executable must carry a valid signature, and its
    signer must be the same publisher as the running executable's. A running
    build that is signed never accepts an unsigned or differently signed
    update. While releases are unsigned, the check is "not signed" on both
    sides, recorded in the log as the reason the check was skipped, and
    never silently passes a signed-to-unsigned change. The check is an
    interface (the Windows implementation reads the signature; the test host
    supplies a fake) and is done on the extracted executable, before
    staging is trusted.
  - **Build attestation.** The release carries a signed provenance
    attestation (`actions/attest-build-provenance`, README "Is it safe").
    Verifying it in the app is open question 2; the sums file's hash is
    what can be verified offline today.
- **Re-verify before replacing.** The staged executable is checked again,
  hash against the value verified at fetch time, immediately before it is
  swapped in. The reference verifies at fetch and then replaces hours later,
  after a background download at start-up, from `%TEMP%` where any process of
  the same user can write (`selfupdate.py:386-405`). Stage in a folder of
  the tool's own under the per-user data folder, not the temp folder.
- **The same limits as every download:** a size ceiling on the archive and
  on the uncompressed executable (the reference reads each member whole into
  memory with no limit, `:163-169`).

### Replacing the running executable

Windows will not let a running executable be overwritten or deleted but
does let it be renamed (to verify, below). The reference hands the swap to a
batch file because a running process cannot replace itself, then exits at
once without cleanup (`selfupdate.py:238-311`, `:379-405`):

1. write `dlss5-autopilot-update.bat` into the temp folder (a fixed name);
2. start it with `cmd /c`, detached, no window, with a clean environment;
3. exit the process immediately (`os._exit`), skipping every handler.

The script waits up to 30 one-second rounds, deleting an earlier `.old.exe`
and trying to rename the target to `<name>.old.exe`; copies the new build over
the target; if the target is then missing, renames the old build back; deletes
the staged build and the staging folder (`rmdir` without `/s`, so a folder that
still holds anything stays); and starts the new executable from a fresh
top-level process.

Behaviour to specify, in this order:

1. **Preconditions.** Running as the built executable; the staged build
   exists ("The downloaded update is no longer there - press update again to
   fetch it." otherwise); the folder of the running executable is writable
   (a probe, as for the DLSS files); no job is running. A tool installed
   where it cannot replace itself (an administrator-only folder) says so and
   opens the release page: it never offers an elevated update, which would
   run a downloaded build with administrator rights.
2. **Save state:** window placement, settings written and flushed, logs
   flushed, tray icon removed.
3. **Swap with rollback.**
   - Rename the running executable to `<name>.old.exe` (replacing a stale
     `.old.exe` only after the new one is known good, see Cleanup: a stale one
     is a previous update's leftover and is removed first).
   - Copy the staged build to the executable's place through a temporary
     name and rename it, then verify its hash against the verified value.
   - On any failure: remove the partial new file, rename `.old.exe` back,
     keep the staged build, and tell the user (the "update failed" banner).
     The running build is untouched in that case.
4. **Start the new build** as a fresh top-level process with a clean
   environment (the reference clears the packaging tool's `_PYI_*`
   variables and sets `PYINSTALLER_RESET_ENVIRONMENT`, #136; none of it
   applies to the C# build), once the old process has released its single
   instance, then exit normally (not by killing the process).
5. **The staged build and its folder are removed** after the new build is
   in place.

Changed from the reference: no batch file, and no `exit` before the result is
known. Reasons:

- When the swap fails, the reference script's `pause` and error text run in a
  window that was created hidden (`creationflags` no-window), the tool has
  already quit, and nothing restarts it: the user's tool vanishes and a
  failed update is invisible (`selfupdate.py:256-264`, `:397-405`).
- Its `copy /y` is unchecked (`:294-296`): a short copy (disk full) leaves a
  broken executable that is then started, with the old one only coming back
  when the file is missing altogether, not when it is damaged.
- A fixed script name in the temp folder races between two instances and is
  rewritten by whoever is last (`:394`).
- Paths are interpolated into the script unescaped (`%`, `&`, `^`, `!` in
  a path, and non-ASCII text through code pages) (`:253-254`, `:268-289`).
- The one-folder layout's `xcopy` and `rmdir /s` rollback logic exists only
  for PyInstaller's `_internal` folder, an antivirus accommodation with no
  meaning for the single-file C# build.

The in-process rename depends on Windows allowing a running image to be
renamed on the same volume (to verify). If it did not hold on some set-up
(for instance a folder with a filter driver), the fallback is the reference's
helper approach: a helper started before the exit that reports its failure
visibly. Decide only if verification says so.

### Cleanup of old files

- **The previous build.** `<name>.old.exe` left by a swap is removed on a **later** start, not the
  first start of the new build, so a broken build can be gone back from
  (#450; `selfupdate.py:339-376`).
  - A few seconds after the main window is up (3 s in the reference,
    `app.py:134`), off the UI thread, the build looks at the settings value
    "settled version" (`update_settled`).
  - If it is not equal to the running version: record the running version and
    remove nothing. This is the new build's first run; the old build is
    there for the whole of it.
  - If it is equal: remove the previous executable. A file that cannot be
    removed (held by a scanner, or running) is left, and the next start tries
    again. Each removal is logged ("removed the previous build: `<path>`").
  - A new update starts the count again (the version differs).
  - A new build that dies before its window appears never records itself, so
    the old one stays.
  - From source, nothing is removed and nothing is recorded.
  - A copy started from the `.old.exe` itself never removes itself.
- **Staging folders.** Folders of ours in the temp folder named
  `dlss5-autopilot-update-*` older than 24 hours are removed before each
  fetch (a finished or failed update leaves one) (`selfupdate.py:94-105`).
  Changed from the reference: they are also removed at start-up with the
  previous build, since a user who never updates again would otherwise keep
  them forever; with the staging folder in the per-user data folder, the same
  rule applies there. Only folders this tool created, by name and age, never
  anything else.

### Manual rollback

The previous build is a file the user can rename back by hand, as the
confirmation text says. Nothing automatic goes back (open question 3).

## What must never happen

- A release without a `SHA256SUMS.txt` is installed.
- A build whose archive hash does not match the published one is installed or
  started.
- The tool replaces itself without the user's click, in the background or at
  start.
- The running executable is lost: at every step the old build exists as the
  executable or as `.old.exe`, and a failed swap leaves the running build
  working with the user told.
- A downgrade or an equal version is offered or installed.
- An archive member writes outside the staging folder.
- A download without a size limit, over a downgraded (http) request, or from
  another host than GitHub's release hosts is accepted.
- A signed build is replaced by an unsigned or differently signed one.
- The staged build is started without being verified immediately before.
- Updating while an install or another writer holds the busy flag.
- A failed check or a failed background download blocks the tool, shows a
  traceback, or retries in a loop.
- The old build is removed during the new build's first run, or while it is
  running, or its folder while the old executable remains.
- Cleanup removes anything not created by this tool.

## Bugs and questionable choices in the reference not to port

- `selfupdate.py:197`: a release with no sums file is installed unverified.
- `selfupdate.py:211`, `:221-227`: only the extracted executable is hashed;
  the match is by name suffix and the first hit wins; a hash of any length is
  compared as text.
- `selfupdate.py:111-132`: the first `.zip` and the first `.exe` asset win,
  whatever their names; `:136-139`: the first `.exe` inside the archive, at
  any depth, whatever its name.
- `selfupdate.py:163-169`: archive members read whole into memory, no
  uncompressed limit.
- `selfupdate.py:386-405`: the staged build is not re-verified before the
  swap, and the process kills itself before the outcome is known; see above.
- `selfupdate.py:394`: one fixed script name; `:253-289`: path quoting.
- `selfupdate.py:259`: `ping -n 2 127.0.0.1` as a sleep, 30 rounds; replaced
  by a real wait or by no wait at all (rename needs none).
- `selfupdate.py:42`, `:184`: the 4 MiB floor and the `_internal` folder
  layout are properties of PyInstaller builds.
- `update.py:26-29`: the digit-tuple version compare.
- `update.py:47-50`: a cache hit returns the cached latest version with
  `RELEASES_PAGE`, not the answer's own `html_url`.
- `app.py:541-542`: a click ignored silently while busy.
- `app.py:513-520`: a failed auto-download is logged only.
- `update.py:60-64`: the cache is written inside two bare `except` blocks
  that hide settings failures.
- `selfupdate.py:329-376`: the "settled" record is a global settings key
  that a manual reinstall of the same version also trips; harmless, kept.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| Windows allows renaming a running executable on the same NTFS volume (and not deleting or overwriting it); antivirus filter drivers and OneDrive-style folders can still refuse | Windows file-sharing documentation (`FILE_SHARE_DELETE`, `MoveFileEx`), tested on a built single-file exe |
| `releases/latest` returns the newest non-draft, non-pre-release by creation date, not by version | GitHub REST docs for releases |
| GitHub release assets expose a digest in the API; whether it can be a second hash source | GitHub REST docs: release asset fields |
| Anonymous API limit of 60 requests an hour per address | GitHub REST docs: rate limits |
| Authenticode verification on Windows from .NET: which API (`WinVerifyTrust`, or the signer's certificate) and how to read the signer subject reliably, including timestamped signatures after the certificate expires | Microsoft docs for `WinVerifyTrust`; `X509Certificate`; SignPath documentation for the signed subject |
| The attestation can be verified without the GitHub CLI (Sigstore bundle, Fulcio chain, the workflow identity) | GitHub docs for artifact attestations; Sigstore verification libraries for .NET |
| How the single-file self-contained .NET build behaves when run from a renamed `.old.exe` and when two instances start | .NET single-file deployment docs; tests on Windows |
| Defender and SmartScreen behaviour on a new unsigned release (README "Antivirus warnings") | Microsoft Security Intelligence submissions; observation |

## Fixtures

- **The latest-release answer:** `tag_name` (`v9.9`), `html_url`,
  `assets[]` with `name` and `browser_download_url`. The old suite builds one
  with an archive asset and a `SHA256SUMS.txt` asset (section 40 of
  `test_all.py` at `17d8a94~1`). Also: a release with only a bare `.exe`, with
  no sums asset, with no assets, a pre-release tag, a draft, a lower tag, an
  equal tag, `2.1` against `2.1.0`, and the answer of a rate-limited API.
- **`SHA256SUMS.txt`** as the release workflow writes it: two lines,
  `<64 hex>  dist/dlss5-autopilot.exe` and
  `<64 hex>  DLSS5-Autopilot-v<version>-win64.zip`. Variants: `*` binary
  marker, a missing archive line, a hash of the wrong length, a name that
  ends the same (`evil-dlss5-autopilot.exe`), duplicate disagreeing lines.
- **Archives built in the test:** a one-file release (the executable and a
  README); a member named `../x.exe`; an absolute member; a drive-letter
  member; a zip with no executable; an executable that is not a 64-bit PE; a
  tiny one.
- **A fake PE builder** (as in [dlss-updates](dlss-updates.md)): a 64-bit and a
  32-bit executable.
- **Cleanup:** a temp folder with `dlss5-autopilot-update-*` folders of
  different ages and other folders; an executable folder with `.old.exe`;
  a settings file with and without the settled version, and an `.old.exe` that cannot be removed (a directory of that
  name stands in for a running file, as the old test does, section for #450).
- **Replacement:** a temporary folder as the "install folder", an interface
  fake for the rename and start, to exercise success, a failing copy and a
  failing start.

## Open questions

1. **Asset names and the sums entries.** Fix them in the release workflow
   (archive name, executable name, the sums file listing both), since the
   verifier matches them exactly.
2. **Verify the build attestation in the app,** or leave it to the user's
   `gh attestation verify`? It is the only check that proves the release came
   from the project's workflow, but needs a Sigstore verifier.
   Recommendation: verify Authenticode once signing exists, document the
   attestation for users, and revisit.
3. **Automatic rollback** when the new build fails to start (a start-up
   marker that the old build checks and acts on), or hand rollback only?
4. **Restart while a game watcher, autopilot or an install is running:**
   today only the busy flag stops it; should an active watcher block or
   postpone the restart?
