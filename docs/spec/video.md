# Video, YouTube, webcam and screen capture

The DLSS 5 feed does not care what draws the frame: a video player that
renders through Direct3D 11 is, to ReShade and the feed, a game with no depth
buffer. The tool sets up a portable MPC-HC in a folder of the user's choice,
installs DLSS 5 into it like a game (the feeder route, 64-bit, DirectX 11),
and then gives the user ways to put pictures in front of it: a file, a live
YouTube (or any yt-dlp) link, a downloaded link, a webcam, a screen or a
single window. It can also render a clip offline into a new file. Read from
the reference's `core/video.py`, the video page's controller
`core/ui/ctl_video.py`, the sideline in `core/installer.py`, the README's
"Video, YouTube, webcam", and the old test section 11/26/27 (commit
`d5801a3~1`, `test_all.py`). The generic install and uninstall of the feeder
route is another spec; this one owns what is specific to the player.

## What the user gets

### The player folder

- **Set up the video player** fetches everything into one folder. Default
  `%USERPROFILE%\Videos\DLSS5 Player` (`video.py:77`); the user may pick
  another (the folder is asked first, with "use this folder / choose another").
  Nothing is written anywhere else except the download cache and the one
  remembered setting `video_player_dir` (the folder), which makes the player a
  card in the library (`video.py:45,273,287`).
- **Idempotent.** If `mpc-hc64.exe` is already in the folder, MPC-HC is not
  fetched or extracted again, so the user's playlists and settings survive
  (`video.py:206`). The ini, yt-dlp and deno steps run every time.
- **MPC-HC**: the newest release of `clsid2/mpc-hc`; the asset whose name ends
  `x64.zip` (`video.py:47,85-92`). The zip may or may not have one wrapping
  top folder; if every entry shares it, it is dropped. Each member is written
  through the safe-extraction rule of the net spec; a member that would land
  outside the folder aborts with a message naming the player and the folder
  (`video.py:212-239`). After extraction `mpc-hc64.exe` must exist or the
  setup fails.
- **yt-dlp**: the newest release's `yt-dlp.exe` (`yt-dlp/yt-dlp`), copied
  **beside the player** as `yt-dlp.exe`, because MPC-HC only finds it there
  (the `YDLExePath` setting broke URL playback outright; `video.py:38-43`).
  An older build's copy under `tools\` is deleted. If this step fails the
  player still works: the user is told "local files only until it is dropped
  into the folder by hand" and the failure goes to the log.
- **deno** (`denoland/deno`, asset `deno-x86_64-pc-windows-msvc.zip`), only
  `deno.exe`, into `tools\`. yt-dlp needs a JavaScript runtime to read
  YouTube's player; without one most formats are missing. `yt-dlp.conf`
  beside `yt-dlp.exe` gets one line `--js-runtimes "deno:<path with forward
  slashes>"` (quoted: yt-dlp splits the file like a POSIX shell). Other
  lines are kept, an older `--js-runtimes` line is replaced, a copy under
  `tools\` is deleted (`video.py:601-663`). A deno failure is a warning
  ("YouTube may offer fewer formats"), never a failed setup.
- **ffmpeg + ffprobe** are *not* fetched with the player (170 MB): only on the
  first download, webcam, capture or offline render, so people who only stream
  never pay for them. They live in `tools\` (see below why), from
  BtbN/FFmpeg-Builds, release tag `latest`, asset
  `ffmpeg-master-latest-win64-gpl.zip`. If the API does not answer or the
  name has moved, fall back to the tag's direct download URL; the GPL build
  must be 64-bit, not `shared`, not `lgpl` (no NVENC), not `arm64`
  (`video.py:310-318,691-757`). Only `bin/ffmpeg.exe` and `bin/ffprobe.exe`
  are extracted.
- **Layout and why.** `yt-dlp.exe`, `yt-dlp.conf` and the player sit in the
  folder; `tools\` holds ffmpeg, ffprobe, deno and the offline processor. A
  program started from the player folder can load ReShade's `dxgi.dll`
  (ffmpeg does, for D3D11 hardware decoding) and overwrite the player's logs
  with a no-swapchain session (`video.py:38-43`). Subfolders created by the
  tool: `downloads\`, `processed\`.
- **Known player.** The player is "known" while the remembered folder still
  holds `mpc-hc64.exe`. It is presented as a library item named
  `Video player (MPC-HC)`, source "Video", kind video, 64-bit, DirectX 11,
  and its route is the feeder (`video.py:279-292`; old test: "the feeder is
  what it gets").

### The player's settings (`mpc-hc64.ini`)

MPC-HC is portable when `mpc-hc64.ini` sits beside the exe. The tool writes
it on every setup:

| Setting (section `[Settings]`) | Rule | Why |
| --- | --- | --- |
| `DSVidRen=14` | forced every time | MPC Video Renderer (D3D11). The default EVR is Direct3D 9, where ReShade attaches to a device the feed cannot use. A renderer the user changed would silently break the route. |
| `UpdaterAutoCheck=0` | forced | the first-run "check for updates?" dialog stops playback until answered |
| `AutoZoom=0` | forced | the window must stay where the tool parks it beside a screen capture |
| `RememberWindowPos=1` | written only when absent | |
| `YDLMaxHeight=1440` | written only when absent; a user value (old test: 720) is kept | 1440p keeps VP9/AV1 within what one 60 fps neural pass copes with |
| `YDLExePath` | removed | an earlier build wrote it; it breaks URL playback |

- **Hotkey.** In `[Commands2]`, a `CommandModN=<id> <mod> <vk> ...` line for
  command id `996` ("jump to start", bound to Home by default) gets its key
  code set to `0`, so Home opens ReShade's overlay instead of restarting the
  video. Other commands are untouched. The menu still offers the command
  (`video.py:73-74,139-147`; old test: ids 807 and 830 untouched).
- **Byte-level and idempotent.** MPC-HC writes the file with a UTF-8 BOM and
  CRLF. The rewrite reads bytes, splits on any line ending, drops blank lines,
  writes CRLF and keeps the BOM (adds it when the file is new). Three
  rewrites in a row must give identical bytes: a text-mode rewrite once turned
  every CRLF into CR CR LF, the blank lines doubled, and with enough of them
  MPC-HC crashed on opening a URL (0xc000041d, `video.py:108-117`; old
  test "three rewrites keep the BOM, CRLF only, and add no blank lines").
- Keys missing from an existing `[Settings]` are appended to it; a file with
  no `[Settings]` gets one at the end; no key is duplicated; every other key and
  section survives.

### DLSS 5 in the player, and the d3dcompiler sideline

- Pressing **install** on the player card installs the feeder route into the
  folder (CLI: `--video` forces feeder, no DXVK; see the CLI spec). The
  offline render and the "play a link" warning both require this install to
  have been done ("dlss 5 is not installed into the player yet - the link
  still plays, but plain").
- **Sideline.** MPC-HC (and a number of older games) bundles a
  `D3DCompiler_47.dll` that Windows loads in preference to System32's copy.
  The DLSS 5 add-on compiles a compute shader with target `cs_5_1`; the
  bundled copy rejects it, and the feed then reports frames delivered while
  neural rendering silently does nothing (`installer.py:310-319`). So, on
  every ReShade-based install (not OptiScaler, not Remix; `installer.py:3866`):
  - a file named `d3dcompiler_47.dll` (any case) in the install folder is
    **renamed** to `<name>.dlss5-off`, never deleted;
  - the install record lists it in its own key (`sidelined`), never in the
    list of written files, because uninstall deletes everything in that list;
  - a failure to rename is a warning ("nothing will visibly happen in game")
    and the install goes on;
  - the preview ("what will happen?") lists it among the backups as
    `D3DCompiler_47.dll -> D3DCompiler_47.dll.dlss5-off`;
  - a reinstall keeps the moved-aside original (old test: bytes identical);
  - **uninstall** renames it back, byte for byte, and removes nothing else;
    an install with no such file records an empty list.
  - Changed from the reference: when both `<name>` and `<name>.dlss5-off`
    exist at install time and differ, the *current* file is the game's own
    (a repair or update restored it), so it replaces the old aside file by
    an atomic rename instead of being deleted (`installer.py:3513-3514`
    deleted it and kept the older one). When they are identical the extra
    copy is dropped. Why: deleting the game's newest file to keep an older
    one is silent data loss.
  - Changed from the reference: on restore, when something already sits at
    the original name and differs from the aside file, it is kept, the aside
    file stays on the record, and the user is told (`installer.py:3558-3559`
    deleted whatever was there). Why: the takeover rule "restores that keep
    their backup when they fail" applies to this file too.

### Playing

- **Open a file**: file picker (`mp4 mkv mov webm avi m4v ts wmv`, plus all
  files); starts `mpc-hc64.exe <file> /play` with the player folder as the
  working directory.
- **Play a link**: the field, or a link on the clipboard when the field is
  empty. A link is accepted when it starts with `http://` or `https://`, has
  no space, and is shorter than 2048 characters (`video.py:666-668`);
  otherwise "Paste a link first (https://...)". The player resolves the stream
  itself through the `yt-dlp.exe` beside it: nothing is downloaded. Tested
  2026-09-02 on MPC-HC 2.8.1 with a local file and a YouTube link: 60 fps,
  the feed costing about 5% of the frame.
- **Download a link**: yt-dlp saves `bv*[ext=mp4][height<=CAP]+ba[ext=m4a]/
  bv*[height<=CAP]+ba/b` merged to mp4 into `downloads\` as
  `%(title).80s [%(id)s].%(ext)s`, `--no-playlist`, with `--ffmpeg-location
  tools\`. CAP is 1440, or 2160 when "full quality" is on (`video.py:792-810`).
  YouTube serves video and audio apart, so ffmpeg is needed: it is fetched
  first (with its 170 MB announced). Progress comes from yt-dlp's
  `[download] N%` lines; its `[...]` lines are echoed to the log (cut at 120
  characters), its `ERROR` lines are kept (cut at 160) and the last two, or
  the last three output lines, go into the failure message
  (`yt-dlp failed (exit N): ...`).
  - **The file is the answer, not the exit code**: when yt-dlp reported a
    saved file that exists and is non-empty, the download succeeded even if the
    exit code is non-zero (it said 100% downloaded, fixed up, saved, then
    failed; `video.py:848-856`). The exit code is then only logged.
  - When it finishes the file opens in the player.
- A link that is not a link, a missing `yt-dlp.exe` ("run 'set up the video
  player' again") and a missing ffmpeg for a download are refused with a
  message and no process started.
- The user can open the `downloads\` and `processed\` folders from the page.

### Rendering a clip offline

"Pick a video and render it" produces `processed\<name>_nr.mp4` (or
`<name>_nr_<width>.mp4` when scaled). It needs the player installed (the
render uses the NVIDIA runtimes the install put beside the player:
`nvngx_dlssnr.dll` and `nvngx_dlss.dll`; if either is missing: "press install
on the player first").

- **Components**: `DaniilSokolyuk/video2dlssnr`, asset
  `video2dlssnr_release_light.zip` (a pure D3D12 command-line tool that reads
  raw RGBA frames on stdin and writes them neural-rendered on stdout; the
  light release is the exe plus its NGX forwarder, a quarter of a megabyte).
  Only `video2dlssnr.exe` and `nvngx.dll_dlssnr.dll` are taken from the zip
  into `tools\video2dlssnr\`. When the API does not answer, the `latest`
  download URL is used. Plus ffmpeg and ffprobe (an older `tools\` with
  ffmpeg but no ffprobe fetches the pair again).
- **Probe** (ffprobe, first video stream): width, height, `r_frame_rate`,
  `nb_frames`; when frames are unknown they are `round(duration * fps)`; a
  file ffprobe cannot read: "ffprobe could not read the video".
- **Scale**: `native` x1, `2x`, or `4K (3840 wide)` (width pinned to 3840,
  aspect kept). Output sizes are rounded down to even numbers. Style
  `0 default / 1 natural / 2 cinematic`. Intensity is always 1.0 (no control
  exists; see questions).
- **Pipeline**: three processes piped without temporary files:
  1. ffmpeg decodes to raw RGBA, with the frames tagged bt709
     (`setparams=colorspace=bt709:color_primaries=bt709:color_trc=bt709,
     format=rgba`);
  2. `video2dlssnr.exe --nr-video --nr-in WxH --nr-style S --nr-intensity I
     --nr-ui-correction 0 --nr-motion 1 --dll-dir <player folder>`, plus
     `--nr-width/--nr-height` when scaling, started in its own folder;
  3. ffmpeg encodes `hevc_nvenc -preset p5 -pix_fmt yuv420p`, audio from the
     source (`1:a:0?`, aac 192k), `+faststart`, `-shortest`, at the probed
     frame rate.
- **Progress** from the processor's stderr lines `NRPROG <frame> <fps>`:
  percent capped at 99, text `frame N/TOTAL at F fps, M:SS left`; 100 at the
  end. Every other stderr line is kept, except those starting `done:`.
- **Log beside the output** `<output>.log`: everything the processor printed
  and ffmpeg's error. Its environment block (driver, which `nvngx_dlssnr.dll`,
  its version) says which side failed (issue #1042, err 127); the last three
  lines alone had dropped it.
- **Failure** = encoder exit non-zero, processor exit non-zero, or no output
  file: `processing failed: <first ERROR line | last two lines | encoder
  error, at most three> (full output: <log name>)`. On success the output
  opens in the player.
- Changed from the reference: the encoder writes to `<name>.part` and the file
  is renamed into place only on success; on failure or cancel the partial is
  removed (the reference left a half-written mp4 under the final name and
  overwrote an earlier render with `-y` before knowing the new one worked).
  Why: a failed render must not destroy a good earlier one nor leave a file
  that looks finished.
- Changed from the reference: a render can be cancelled; cancelling stops all
  three processes and removes the partial. Why: it is minutes to hours long
  and the reference had no way out but killing the app.

### Webcam

- **List cameras**: ffmpeg `-list_devices true -f dshow -i dummy`, 20 s
  timeout; every line of ffmpeg's output (stderr plus stdout) matching
  `"<name>" (video)` yields a camera, duplicates dropped (old test: a
  `(audio)` line and an "OBS Virtual Camera" listing give
  `["Brio 100", "OBS Virtual Camera"]`). No ffmpeg: an empty list, no error.
- **Start**: stop any capture already running, then ffmpeg reads
  `video=<camera>` via DirectShow (`-rtbufsize 64M`, requested size
  `1280x720` at 30 fps), encodes `libx264 ultrafast zerolatency`, GOP = fps,
  `yuv420p`, and sends MPEG-TS over UDP to `127.0.0.1:47321`
  (`pkt_size=1316`). The player is started on `udp://@127.0.0.1:47321 /play`.
  About half a second of latency. The feed sees it like any other video.
- **Refused size**: if ffmpeg exits within 2 s, it is started again without the
  size and rate (the camera's own default); if that exits too:
  `ffmpeg could not open the camera '<name>' - is another app using it?`
- Without ffmpeg: "ffmpeg is not in the player's tools folder yet - run one
  download first, or press 'set up the video player' again".
- Changed from the reference: the UDP port is a free port picked at start and
  given to both ffmpeg and the player, not the fixed 47321. Why: a fixed port
  collides with another app or a second instance, and any local process can
  feed frames to a known port.
- Changed from the reference: "started" is reported once ffmpeg has produced
  output (or has failed), not after a fixed 2 s sleep and a liveness check.
  Why: a slow camera or a device-busy error after 2 s was reported as started.

### Screen and window capture

Anything on the desktop through DLSS 5, with nothing injected into the source
(a browser playing a video, an emulator, a call, a game with anti-cheat). Same
UDP pipe as the webcam; the delay makes it for watching, not playing. Targets
are listed as `screen 1..N` (one per monitor, primary first, then by left, top)
and `window: <title>`.

- **Window list**: every monitor, then every visible top-level window with a
  title, at least 320x200, unique by title, excluding windows whose title
  starts with "DLSS 5 Autopilot". Nothing found: `screen 1`
  (`video.py:1094-1129`).
- **Screen, one monitor**: the left 62% of the monitor (width rounded down to
  even) is captured and the player is parked on the remaining right strip,
  topmost, so the player never overlaps the capture. **Screen, two or more
  monitors**: the whole chosen monitor is captured and the player goes to the
  next monitor (index + 1, wrapping), maximised. For 12 s the tool puts the
  player back when it moves or resizes itself (a stream arriving makes MPC-HC
  resize), then leaves it to the user (`video.py:1132-1144,1309-1336,1642-1664`).
- **Screen capture method**: Desktop Duplication on the GPU through ffmpeg's
  `ddagrab` (`output_idx`, framerate 30, offset and size of the region), D3D11
  device, `h264_nvenc -preset p1 -tune ll -zerolatency 1`, GOP = fps. If that
  process exits within 2 s, retry with the fallback: `gdigrab` (framerate
  min(fps, 30), the monitor cut out of the virtual desktop by offset and size)
  and `libx264 ultrafast zerolatency`: slower, always works, black for
  hardware-accelerated windows. Both failing: "ffmpeg could not capture
  '<target>' - a window has to be open and not minimised; a screen number has
  to exist".
- **Window capture** (one window, wherever it is and whatever covers it):
  not ffmpeg's own window capture (GDI shows hardware-accelerated windows black).
  The tool grabs the window's composited surface itself with `PrintWindow` and
  the flag `PW_RENDERFULLCONTENT`, as raw BGRA, and pipes it to ffmpeg
  (`-f rawvideo -pix_fmt bgra -s WxH -r fps`, `h264_nvenc p1 ll zerolatency`,
  CPU `libx264` when NVENC refuses the first frames, GOP = fps), about 80
  frames a second at 1080p on the capture side, capped at the rate (1-60,
  30 by default). Sizes are rounded down to even. While the window is minimised
  its last frame keeps being sent; a size change restarts the encoder at the
  new size ("window resized to WxH - restarting the encoder"); a closed
  window ends the feed ("the captured window closed - feed ended"); an
  ffmpeg that went away ends it ("ffmpeg went away - feed ended"). A
  minimised window is restored when the capture starts; "that window is gone -
  press refresh and pick it again" when its handle no longer exists; no feed
  after 2 s: "could not capture '<target>'". Because the player is not on the
  capture's desktop area, it can go fullscreen on top of the source.
- **Stop**: kills the capture, the encoder and the window feed, takes the
  player out of the topmost band, and stands down the watcher. A **watcher**
  stops the capture by itself when the player's window disappears (seen once,
  or not seen within 15 s), checked every 2 s; it stands down when a newer
  capture starts (`video.py:1674-1702`).
- Changed from the reference: the player is identified by its executable path
  (our folder's `mpc-hc64.exe`), not by the window class `MediaPlayerClassicW`
  alone. Why: the class is shared by any MPC-HC on the machine; the watcher,
  the parking and the F6 toggle all hit a different MPC-HC if the user runs
  their own.
- Changed from the reference: the window list skips cloaked windows, tool
  windows and the player itself. Why: "visible" alone lists suspended UWP
  shells and the player capturing itself.
- Changed from the reference: the tool's own process does not change its DPI
  awareness mid-run (`SetProcessDPIAware` was called from the capture
  helpers); the application manifest declares per-monitor DPI awareness once.

### Checking the feed took the picture

Ten seconds after a capture starts, the tool reads `dlss5-feed.log` in the
player folder and reports either "`<target>` through DLSS 5: yes - N frames
processed so far[, motion alive]. F6 toggles it." (N at least 3) or the
warning "the feed has not processed any frames yet. if the player shows the
picture, give it a few seconds; if it shows nothing, another app may hold the
camera, or the window is minimised."; for window capture the frames sent are
logged too (`ctl_video.py:368-400`).

The log line formats the feed writes, and the rule (`video.py:986-1030`):

- lines `HH:MM:SS.mmm  <text>`; the day is taken from the start time, and a
  stamp more than 12 h away from it moves to the neighbouring day (a stream
  begun before midnight otherwise read every line as tomorrow's);
- lines stamped more than 1 s before the start are ignored (an old run);
- frames = the largest N in `frame N delivered`;
- motion is alive when a line containing `MV probe` has `(\d+)% non-zero` with
  a value above 0;
- a missing or unreadable log is `(0, no)`.

### Toggling and the notes

- **F6** switches neural rendering on and off in the DLSS 5 add-on (renodx-dlss5
  4.6 and newer); **Home** opens the ReShade overlay (MPC-HC's own Home is
  unbound, above). Neural rendering redraws the whole window, menus included;
  text looks hand-drawn.
- The page's toggle button brings the player to the front and presses F6
  for real: the add-on reads its key through ReShade's input hook, which only
  sees real keyboard input aimed at the window, never a posted message.
  "The player is not running - open it and start a video first" when there is
  no player window.
- Changed from the reference: the key press is sent only after the player is
  confirmed to be the foreground window; otherwise the user is told to press F6
  in the player. Why: `SetForegroundWindow` is refused by Windows' focus
  rules at times, and the reference then pressed F6 into whatever app had focus.
- The checklist shown after setup: open a file or URL; F6 toggles; Home opens
  the overlay; and "there is no depth buffer in a video, the feed runs on colour
  and motion only - 'depth is flat' in the log is expected here".

## What must never happen

- A file written outside the player folder from an archive member (the
  extraction rule of the net spec; it is the one place here that keeps an
  archive's own folder structure, issue #79).
- The player's ini rewritten so that blank lines multiply, the BOM is lost or
  a line ending other than CRLF appears.
- A user-changed renderer left in place: the route cannot work on EVR.
- The user's playlists and settings overwritten by a second setup.
- The game's `d3dcompiler_47.dll` deleted, or listed among the files uninstall
  deletes. It is restored byte for byte.
- A failed yt-dlp, deno or ffmpeg step reported as a failed player setup (the
  player works without them), or a successful download reported as failed
  because of a late non-zero exit code.
- ffmpeg or any other program started from the player folder itself.
- A capture left running with nobody watching it: closing the player stops it.
- A render's partial output left under its final name.
- Anything sent anywhere but `127.0.0.1` over UDP.

## Bugs and questionable choices in the reference not to port

- `video.py:1002-1020`: the day inference for log stamps is a workaround for a
  log that carries no date. Port the rule only because the feed's log format is
  not ours to change; if the feed ever writes a date, read it.
- `video.py:254`: yt-dlp is replaced only when its size differs from the
  stored one. A new release of the same size is never installed. Compare the
  published checksum (see verify) or the release tag recorded beside it.
- `video.py:30-31,212-238`: a zip member is read whole into memory; use a
  stream with the size limit of the net spec.
- `video.py:1203-1216,1692-1694,1746,1761`: the player is found by window
  class (above).
- `video.py:1768-1771`: a 250 ms sleep on the UI thread, then F6 into
  whatever has focus (above).
- `video.py:915-978,1630`: fixed 2 s sleeps decide "started" (above).
- `video.py:869-871`: a fixed UDP port on loopback (above).
- `video.py:1059-1067`: `SetProcessDPIAware` called from a helper, a process-wide
  side effect on first use.
- `video.py:1339-1355,1361-1400`: a GDI DIB section is created and destroyed
  per frame; allocate once per size.
- `video.py:1070-1091,1285`: the monitor numbering (sorted primary first, left,
  top) is used as ddagrab's `output_idx`, which is DXGI's output order on the
  default adapter. The two orders may differ and monitors on another adapter
  are unreachable; see verify.
- `video.py:402-435`: `r_frame_rate` is the stream's base rate, not the average;
  a variable-frame-rate source is re-timed wrongly (audio drifts). See open
  questions.
- `video.py:441,484-490`: `intensity` is a parameter nothing sets; do not carry
  an unused knob.
- `video.py:605-663`: `ensure_deno` returns `None` silently when the asset is
  absent or the member missing; prepare then prints nothing. Say so.
- `ctl_video.py:405-411`: `capture_stop` does not wait for the processes; the
  page updates before they are dead.
- Module-level globals for the running capture (`_webcam_proc`, `_window_feed`,
  `_watch_gen`): one capture session object, one at a time, is the behaviour.
- `video.py:1112-1127`: windows with the same title collapse into one entry.
- Downloads of ffmpeg, yt-dlp, deno and the processor are not verified beyond
  the net spec's magic-bytes check; see verify.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| `DSVidRen=14` is the MPC Video Renderer; `UpdaterAutoCheck`, `AutoZoom`, `YDLMaxHeight`, `YDLExePath`, `RememberWindowPos` are the ini keys and meanings | MPC-HC (clsid2) `AppSettings.h` and `AppSettings.cpp`, the `[Settings]` writer |
| the `[Commands2]` line is `CommandModN=<id> <mod> <vk-hex> ...`; id 996 is `ID_PLAY_SEEKSET` and its default key is Home; F6 unbound in 2.8.1 | `resource.h` and the default command table in clsid2/mpc-hc |
| MPC-HC finds `yt-dlp.exe` only beside the exe, and a second start hands its URL to the running instance (single instance) | clsid2/mpc-hc source for the YDL integration, `/play`, and the "only one instance" setting |
| the release asset names (`x64.zip`; `yt-dlp.exe`; BtbN's `ffmpeg-master-latest-win64-gpl.zip`; deno's zip; `video2dlssnr_release_light.zip`) and the `latest` tag semantics | the four GitHub release pages |
| yt-dlp reads `yt-dlp.conf` from the exe's folder and `--js-runtimes "deno:<path>"` is the flag, split like a POSIX shell | yt-dlp README, "Configuration" and "--js-runtimes", and the EJS wiki page |
| `--print after_move:filepath` and `--print-to-file` exist; format selection string | yt-dlp README "Output Template", "Format Selection" |
| the d3dcompiler claim: the bundled `D3DCompiler_47.dll` rejects `cs_5_1` while System32's accepts it, and Windows loads the application-folder copy first | Microsoft's D3DCompiler documentation and DLL search order; a test with the real MPC-HC copy (record its file version) |
| `ddagrab` options (`output_idx`, `offset_x`, `video_size`) and which adapter it uses; DXGI output order versus `EnumDisplayMonitors` order | ffmpeg `ddagrab` filter docs; Microsoft DXGI `EnumOutputs` |
| `PrintWindow` with `PW_RENDERFULLCONTENT` returns the composited surface of covered and hardware-accelerated windows; protected (DRM) content stays black | Microsoft Learn `PrintWindow`; a test on a browser window |
| `dshow` device listing format, and the way to address two cameras with the same name (`video=Name@index` or alternative names) | ffmpeg `dshow` documentation |
| `hevc_nvenc` presets and `-movflags +faststart`; NVENC availability on RTX 20-50 | NVIDIA Video Codec SDK, ffmpeg docs |
| `NRPROG <frame> <fps>` lines and the `--nr-*` flags of `video2dlssnr`, including `--dll-dir` | the project's README and `nr_video.py` |
| the feed's `dlss5-feed.log` line formats (`frame N delivered`, `MV probe ... N% non-zero`) | the DLSS5-Reshade-AIO feeder source |
| 60 fps / 5% frame cost on MPC-HC 2.8.1 (a measured claim of the reference) | a fresh measurement before it is stated to users |

## Fixtures

- Ini: byte strings in the old tests (`test_all.py` lines 2941-3010 in
  `d5801a3~1`): fresh; a user renderer `DSVidRen=11` with `Volume=42` and
  `YDLMaxHeight=720`; a BOM+CRLF file rewritten three times; a stray
  `YDLExePath`; a `[Commands2]` with ids 807, 996, 830.
- Feed log: the five-line sample in the old test (lines 5222-5250): an old run
  at -120 s, frames 1 and 7, an MV probe with 98% non-zero; the cases "future
  start", "missing log", and a log spanning midnight.
- ffmpeg device list: the three-line sample (a Brio, its microphone, an OBS
  Virtual Camera), plus a real `-list_devices` output from a Windows machine
  with two same-named cameras (to be captured).
- Release answers: one real `releases/latest` JSON each for clsid2/mpc-hc,
  yt-dlp, denoland/deno, BtbN's `latest` tag, video2dlssnr (to be captured;
  the net spec's fixtures hold the rate-limited shape).
- A fake MPC-HC zip with and without a wrapping top folder, and one with a
  `../x` member.
- The real player folder (old test 2847): `mpc-hc64.exe` seen as a 64-bit
  DirectX 11 video target.
- A folder with `D3DCompiler_47.dll` of known bytes, install then uninstall
  (the sideline), including: a reinstall, an existing aside file that differs,
  an occupied original name, and an install with no such file.
- A yt-dlp transcript with `[download] 42.0% ...`, a saved path line, and a
  non-zero exit after the file was saved (issue #1034); one that fails with
  `ERROR:` lines.

## Open questions

1. **Updates.** MPC-HC is never updated once extracted; yt-dlp is updated only
   by running setup again, yet YouTube breaks old yt-dlp builds within weeks.
   Should the tool refresh yt-dlp (and deno) automatically when the player is
   opened or a link is played, or hand `yt-dlp -U` to the user? Recommendation:
   check yt-dlp on each setup and on a failed link, with a button.
2. **Verification of the fetched programs.** Which of these publish checksums
   (BtbN, yt-dlp `SHA2-256SUMS`, deno `.sha256sum`, clsid2) and does the tool
   verify them, as the plan demands "verified hashes where publishers provide
   them"? video2dlssnr publishes none: pin a version and a hash, or follow
   `latest` unverified?
3. **Variable-frame-rate sources and HDR.** The render forces 8-bit RGBA tagged
   bt709, so an HDR source is mislabelled, and VFR sources drift. Refuse HDR?
   Convert with a `fps` filter?
4. **Intensity and style.** Is a user control for `--nr-intensity` wanted?
5. **Two cameras with the same name** need alternative names or an index;
   the reference loses the second. Worth it?
6. **DRM windows.** A protected window captures black by Windows' design. Say so
   in the page, or detect a black stream?
7. **Capture frame rate** is fixed at 30; the code supports 1-60. Expose it?
8. **A second capture or a running player.** Starting a capture while the user's
   own copy of this player is open is handled by single-instance MPC-HC; confirm
   with a test.
9. **Rewriting the ini while the player is running**: MPC-HC rewrites it on exit
   and would undo the tool's keys. Refuse, or warn?
