# Contributing

## The most useful thing you can send

A **result**. In the tool, after you have played: **did it work?**, then
**share the result**. It carries the game, the route, the card, the driver
and whether it worked - no paths, no user name. Once a game has five, the
next person with it is told which route worked most often before they
install. That list is the only real evidence this project has.

A **bug report** from **report a bug** in the tool is the second: it fills
in the version, the card, the driver, the route, the diagnosis and the log
tails, which is what makes a report answerable in one round instead of
four.

## Running it from source

```
python dlss5_autopilot.py        needs Python 3.12 (what ships), on Windows
uv run poe build                 a local .exe (Windows only); the release build is GitHub's
```

The development tools are pinned in `uv.lock`; set them up with `uv sync`.
[AGENTS.md](AGENTS.md) lists the checks and when each is needed
(`uv run poe fix`, `check-files`, `check`, and `test` for the pytest suite,
which opens no window and reaches no network).

## If you send a pull request

- **Small and about one thing.** The first outside PR (#157) was one
  distinction - a protected executable is not an unwritable folder - and it
  went in.
- **A check with it.** The suite is in `tests/` (it grows as modules are
  triaged, see AGENTS.md; `uv run poe test` runs it). Add the case your
  change is about, built from a real log or a real folder rather than from a
  clean file made up for it. A rule that has never seen a real input is a
  guess.
- **Leave the files you touch clean.** `uv run poe fix`, then
  `uv run poe check-files <the files>`, before every commit.
- **Run the checks that guard what you touched.** `_tools/verdict_check.py`
  after anything in `core/diagnose/` - it replays every report anybody has
  sent and prints the ones whose answer moved.
- **Match the file you are editing.** Comments here say why a line exists,
  usually with the issue number that caused it; that is what stops the same
  thing being undone six months later.

## What this project will not take

- A component bundled into the repository or the release archive. Everything
  is fetched at run time from its own publisher - see [NOTICE](NOTICE).
- Anything that writes outside the game's folder without recording it in
  that install's manifest, or that uninstall cannot take back out. The
  Vulkan and OpenXR layer registrations are the exception (see
  [SECURITY](SECURITY.md)): shared by every install that needs them, listed
  under **what will happen?** before an install, and removed with the last
  install that needs them (the all-users Vulkan copy only when the tool
  runs as administrator).
- A rule that decides something about a game from one weak signal. The API
  detection is full of scars from exactly that.
