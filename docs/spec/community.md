# Shared results and the compatibility feed

**Deferred to M8.** The design below is the one M8 builds.

The tool decides a route from the executable alone. What a thousand other
machines found is a better guide. After playing, a person presses **share the
result**; the app sends one small record to the project's server, a
Cloudflare Worker (free plan) with its storage in D1. The Worker adds the
records up into one feed and a searchable page, and every copy of the tool
reads that feed **before** an install. This spec covers sharing a result, the
server, the feed's format, how the tool uses the feed, and the page. Read from
`core/community.py`, `pages/compatibility.py`, `pages/compat_page.py`,
`.github/workflows/compatibility.yml`, the share path in `core/ui/ctl_game.py`
(`2877-2975`, `2360-2410`, `2594-2600`), `core/autopilot.py:125-142`,
`core/dlss.py:525-565`, `core/verdicts.py:276-420`, the README's "Bugs" and
"It reads what happened to everyone else", and the old tests around lines 9859
and 25610 (`d5801a3~1:test_all.py`).

- Changed from the reference: no GitHub issue and no GitHub Actions builder,
  because most players have no GitHub account. The reference opened a
  pre-filled GitHub issue in the browser and built the feed from issues; that
  flow is under "What the reference did".

## What the user gets

### Sharing a result

- **Where.** On a game's result, after the diagnosis ("did it work?"), a button
  **share the result** (`gamepage.py:974`). It is not offered before a result
  exists.
- **The outcome.** In order:
  1. Windows recorded the game crashing in this session: *failed*, said by
     `crash`, on any route;
  2. the diagnosis's verdict says outright (working = *worked*; the failing
     stages, and the answers "it closed itself", "it never started" given by the
     person, = *failed*);
  3. otherwise the logs cannot show (a route that logs no frames, a second DLSS
     hook beside ours: stages 6, 7 and 11 of the verdict chain, `UNSEEN`,
     `verdicts.py:393-407`): the person is asked, "The logs this report read
     cannot show whether DLSS 5 ran - you watched the game. Did the DLSS 5
     picture show in the game?" with "it showed / it did not", and the result is
     marked as said by `person`. Before this question existed, such results were
     filed as *failed* whatever happened (#414 said "WORKED FINE"; 230 results).
- **The confirmation**, before anything is sent, lists what is sent: "This sends
  the result to the DLSS 5 Autopilot server: the game's name and executable, the
  route and build, the graphics api, <what else, below>, and a random id of this
  installation. No paths, no user name, no account, nothing else. Results are
  published as counts." Buttons "send" / "cancel". The list is built from the
  record actually being sent: whether the game ships DLSS, FSR or XeSS; the card
  and driver, the tool's version, whether it worked (or "that it worked / did not
  work (your answer)" or "(Windows recorded a crash)"), the verdict's id, and,
  when measured, the work area, the milliseconds and the frame rate.
- **Sending.** On "send", the app POSTs the record as JSON to the Worker through
  the [net](net.md) path (its host on the allowlist). No browser opens and no
  account is needed. The answer is "thanks - it counts from the next feed" or the
  reason it was refused; a failure (offline, rate limited) says so, and nothing is
  queued to send later without a new press.
- **The record** (`community.py:92-149`); strings are cut, nothing else is added.
  The keys of `measured` are taken one at a time, so a future caller cannot
  widen what leaves the machine.

  | Key | Content | Limit |
  | --- | --- | --- |
  | `v` | `1` | |
  | `exe` | the executable's **file name**, lower case | |
  | `game` | the library's game name | 80 chars |
  | `api` | `DX8`..`DX12`, `DXGI`, `VULKAN`, `OPENGL`, `UNKNOWN`, upper case; the game's own API (a DXVK install records Vulkan, and its results went into the Vulkan class instead of DirectX 9: gate 2.0.6) | |
  | `route` | the route's short key (`native`, `optiscaler`, `feeder`, ...) | |
  | `build` | the OptiScaler build key, or empty | |
  | `sm` | `sm_<n>` of the card, or empty | |
  | `gpu` | the card's model name | 40 chars |
  | `driver` | the NVIDIA driver version, e.g. `616.64` | |
  | `tool` | this tool's version | |
  | `result` | `worked` or `failed` | |
  | `res` | work area, whole percent (a published "75.0%" reads as false precision) | int |
  | `ms` | milliseconds a frame spent on what grows with the work area; on the feeder the model **and** the feed together, solved from two sessions' frame rates, an upper bound on the model; on OptiScaler the model alone | 2 decimals |
  | `fps` | frame rate | 2 decimals |
  | `by` | `person` or `crash` when the tool could not see the outcome itself; absent otherwise (records before 2.0.5 have none) | |
  | `up` | `dlss`, `fsr`, `xess` or `none`: what the game ships (from 2.0.6) | |
  | `verdict` | the diagnosis's verdict id from the catalogue ([diagnosis](diagnosis.md)), never its sentence | `[a-z0-9_.]`, 60 chars |

  `res`, `ms` and `fps` are present only if numbers (not booleans); the
  `measured` part is empty unless the work area was read from the add-on's own
  config (a slider's guess is never published, `autotune.py:480`) and enough was
  measured (`autotune.py:465-498`).
- **What must not be in it**: no path, no user name, no machine name or id, no
  GPU serial or UUID, no IP, no store or launcher account id, no install folder,
  no free text typed by the user. The `game` name and `exe` are the only
  user-influenced strings; a game added by hand takes its name from a folder
  name, so only the last component, never a path.
  - Changed from the reference: the record carries the verdict's **id**, not
    its sentence. The reference put the diagnosis's sentence, written from log
    content, into the issue body unscrubbed, so a quoted log line could carry a
    path; an id carries nothing but itself, and the server needs the id anyway
    to decide whether the result counts.
- **The install id** ("Identity", below) travels beside the record, not in it.
- **Classes.** A *class of games* is one graphics API plus what the game ships:
  `DX12/dlss`, `DX11/none` ... (`game_class`). It is what the tool knows before
  anybody has reported. The API alone is not honest: 53 of 69 OptiScaler results
  on DX12 were games with their own DLSS, and 9 of 27 feeder results on DX12 were
  games without; side by side they compared two kinds of game, not two routes.


### Identity

- **A random install id** (a 128-bit random value) is created on the first run
  and kept in the settings. It is sent with every record, used by the Worker to
  dedupe votes, and never published.
- No hardware fingerprint: it is more invasive, and a client can forge it
  anyway.
- **One vote per (install id, game, route)**: the latest wins. An earlier vote
  for the same key is replaced in every table (routes, drivers, class,
  configurations, measurements).

### The server

A Cloudflare Worker with a D1 database, on the free plan.

- **Receives** a record: checks it against the record's schema, then applies the
  rules below, and stores it with its install id, the day it arrived and the
  salted hash of the sender's IP prefix. A refused record gets a 4xx answer with
  the reason, which the app shows.

1. **Counts or not** (`counts`, `community.py:164-188`), in this order:

   | Case | Counts? |
   | --- | --- |
   | `by` is `person` or `crash` | yes |
   | the record's verdict id is a setup mistake (`NOT_A_ROUTE`: another executable or folder ran, a launcher, a wrong renderer, a proxy name the game does not load, ReShade wrote no log, ...) | **no**: a mistake says nothing about the route, and counting it "failed" steers the next person away from one that works (gate 2.0.8) |
   | a verdict whose stage can see the outcome | yes |
   | a verdict in the unseen stages (6, 7, 11) and no `by` | **no**: an unknown is left out, not counted failed |

2. **Sanitises** every string by cutting it (exe 120, route 40, game 80, build 60,
   api 12, sm 12, gpu 40, driver 40, tool 20) and type-checking, lower-casing exe and
   route (a hand-made "Feeder" would open a second bucket and put a route name the
   dropdown does not have into someone's advice). Everything here was sent by a
   client nobody controls; one hand-made request (a list or a number as `exe`,
   `Infinity` or `NaN`, a 200 000-character name) is refused alone and never
   breaks the feed. Numbers: `res` 1..100, `ms` 0..10000, `fps` 0..10000,
   finite, not boolean; otherwise absent.
3. **Bounds**: at most 5000 games; `by_class` and `by_driver` at most 300 keys
   each; a route key must match `[a-z_]{2,20}`; a class key needs an API from
   `DX8 DX9 DX10 DX11 DX12 DXGI VULKAN OPENGL UNKNOWN` and `up` in the four values; a
   driver key must match `\d{3}\.\d{2}`; a record with anything else
   counts for its game and not in the tables.
4. **Dedupe** by (install id, game, route), the latest winning (above).
5. **Counts** go into `games[exe].routes[route]` and `.drivers[driver]` (the
   driver as typed, cut to 40), the class and driver tables, and a **configuration
   row** keyed by (exe, route, build, api, sm, gpu, driver, tool) with `worked`,
   `failed` and `last` = the day (10 characters) the result arrived, **the day
   only**, never the install id or the record id.
6. **Measurements** only from `worked` results with a work area, per game and
   route, then medians as the feed describes (below).
- **Serves** the feed and the page (below), rebuilt from D1 when results change
  and cached by Cloudflare's CDN.
- The record's schema and the card-family table are **one definition** shared by
  the tool and the Worker, as a JSON schema and a data file in this repository,
  so the two cannot drift.

### Anti-abuse

- **Rate limits per IP**: Cloudflare's free rate-limiting rule in front of the
  Worker, and a per-IP limit inside it (a handful of records per hour).
- **IPs are never stored.** Only the hash of the sender's IP prefix (the /24 of
  IPv4, the /48 of IPv6) with a salt that rotates daily, so a stored value
  cannot be traced back after the day (GDPR).
- **The five-result threshold counts distinct sources**: a result adds a source
  only when its install id and its hashed IP prefix both differ from those of the
  game's other counted results. Prefix hashes can only be compared within one
  salt day; across days, distinct install ids carry the count.
- **The limit, stated honestly:** without accounts, a determined attacker with
  many IPs and fresh install ids can skew the results. That is accepted, because
  the data is advisory only and never decides an install by itself: it can move a
  recommendation among the routes on offer, and only with the margins below.

### The compatibility feed

One file, `compatibility.json`, served by the Worker beside the page and
cached by Cloudflare's CDN. Counts only; no install id, no IP hash, no
record id.

```
{"schema": 1, "generated": "2026-09-09T11:00:00Z", "reports": 812,
 "games": {"cyberpunk2077.exe": {
    "name": "Cyberpunk 2077", "sources": 37,
    "routes":   {"optiscaler": {"worked": 41, "failed": 6}},
    "drivers":  {"616.64": {"worked": 3, "failed": 22}},
    "measured": {"optiscaler": {"n": 9, "res": 70, "ms": 6.4, "fps": 78}}}},
 "by_class":  {"DX12/dlss": {"optiscaler": {"worked": 30, "failed": 8}}},
 "by_driver": {"616.92": {"feeder": {"worked": 11, "failed": 32}}}}
```

- `sources` is the number of distinct sources behind a game's results (see
  "Anti-abuse"); every threshold below counts sources, not results.
- `games` is keyed by the executable name; `routes` and `drivers` are the same
  results counted per route and per driver **within that game**;
  `measured` is per route; it is omitted for a game with no measured result.
- `by_class` and `by_driver` count the same results by what the tool knows before
  an install (the class; the driver), each split by route, because a driver's rate
  across routes is mostly its route mix (616.64 read 7 of 28 and 617.14 16 of 22,
  but 22 of 616.64's results were feeder results against 5 of 617.14's).
  Only results that carry `up` (2.0.6 on) have a class; guessing it for older ones
  would mix games with and without DLSS.
- `measured` rows are the **median** of what people ran: `n` results carrying a
  work area, `res` the median rounded to an integer, `ms` and `fps` medians over
  those of the `n` that carried them (so they can be fewer), 2 decimals. Only
  from results that **worked**: a session that crashed measured a crash. Only
  medians, so one machine with a strange number cannot move it far.
- **Schemas tolerate absence**: a list published before 2.0.6 has no tables, and
  every consumer then behaves as it did before.
- Changed from the reference: the file carries a `schema` number (1), so a reader
  can refuse a shape it does not know instead of guessing from missing keys.

### How the tool uses the feed before an install

- **Fetching.** `fetch` downloads the file (the net spec's text limit and TLS
  rules) into the cache as `compatibility.json`; a copy younger than **6 hours**
  is used as is; on any failure the stale copy is used, and with none `{}`. It
  never raises and never blocks an install: it is advice. `cached()` reads the disk
  copy only (for the recommendation, which runs on every game entry and must not
  wait on the network); the first run with no copy recommends by the tool's own
  rules, as offline. A malformed file (not an object) is treated as no file.
- **Where it shows.** The game page writes a block "what other people found"; the
  library row shows a one-line count; the command line prints one `shared  :` line
  (the driver note); the autopilot orders its routes by it; the route
  recommendation can move by it (below).
- **Per game: only with five distinct sources.** `advice(entry, chosen route, driver)`
  speaks only when the game's results across all routes come from at least
  **`MIN_REPORTS = 5`** distinct sources ("two people are not a finding");
  fewer: nothing.

  | Line | When | Text |
  | --- | --- | --- |
  | best route | five distinct sources; the route with **the highest lower Wilson bound** of its worked rate, ties broken by more results, and at least one worked | `<N> reports for this game. The <route> route worked best: <w> of <n> on that route[, failed in <f>].` |
  | nothing works | five distinct sources and no route has a worked result | `<N> reports for this game, and no route is reported working.` |
  | the user's route did worse | the chosen route has results, and it failed in all of them | `The route chosen here (<route>) failed in all <f> of its reports.` |
  | | or it differs from the best one and the best one has more worked | `The route chosen here (<route>) worked in <w> of its reports.` |
  | driver | this game's results on the user's driver have failed at least 5 times and failed more than twice as often as they worked | `<f> of the <all failures in the game> failures reported here were on driver <d>.` (without "of the" when the sum is smaller) |

  `<route>` is the dropdown's name for the route (the label up to its first
  " - "), or the raw key for a route the tool no longer names.
  - Changed from the reference: the best-route sentence gives `<w> of <n> on
    that route` with the route's own count, not `of them` after the
    game's total (it reads as "worked in 3 of 40" when the route had 3 of 3).
    Why: the denominator was the wrong number.
  - Ranking is by the **lower Wilson bound** of the worked rate (below), as
    everywhere in this spec, and the line says "worked best" with the counts.
    Changed from the reference: it ranked by the count of worked results, so 41
    of 100 beat 9 of 9; one rule for every ranking keeps the advice from
    disagreeing with itself.
- **Per class, when the game itself has too few** (`class_line`): for the chosen
  route, with at least 5 results in the class: `Shared results for <class
  words>: the <route> route worked in <w> of <n>.` plus the best other offered
  route that also has at least 5, ordered by confidence: ` The <route> route:
  <w> of <n>.` Said, never acted on. Class words: `DirectX 12 games with their
  own DLSS`, `... with FSR and no DLSS`, `... with XeSS and no DLSS`, `... with no
  DLSS, FSR or XeSS`; the APIs read `DirectX 8..12`, `DirectX 11 or 12`,
  `Vulkan`, `OpenGL`, `unread-API`.
- **Per driver** (`driver_note`), for the chosen route: with the per-driver
  tables, at least **`MIN_DRIVER = 10`** results of this driver on this route:
  `Shared results with the <route> route on driver <d>: <w> of <n> worked.` and,
  if a **newer** driver (numeric comparison of its dotted parts; never an older
  one) has at least 10 results on the same route and the **lower** Wilson bound
  of its rate is above the **upper** bound of this driver's: ` On <other>: <w2>
  of <n2>.` (the best such). With no per-driver table (an older list), the plain
  count over all routes: `On driver <d>, <w> of <n> shared results worked (all
  routes).` With no route given: the total over all routes with at least 5:
  `On driver <d>, <w> of <n> shared results worked.` and the best reported driver
  if its rate is higher.
- **Measurements** (`measured_note`), only where the route reads a work area:
  from the chosen route's `measured` row (or, with no route, the row with the
  most results); at least **`MIN_MEASURED = 3`** results and a work area:
  `<n> shared results where this game worked on the <route> route say what they
  ran at: a <res>% work area[. The model and the feed together cost about <ms>
  ms a frame there (feeder) | . The model cost about <ms> ms a frame there][, at
  around <fps> fps].` The count is attached to the work area only; the other two
  are separate clauses because their counts can be lower. A measurement is one
  number with its count beside it, so three can be shown where three outcomes
  could not.
- **Steering the recommended route** (`dlss.py:525-565`, `class_pick`): where the
  game itself has fewer than five distinct sources, and the recommended route
  is not Remix (a Remix game is never steered), the class's table moves the recommendation to
  another offered route only if **both** routes have at least **`MIN_CLASS = 20`**
  results in the class **and** the 95% Wilson interval of the other route's
  worked rate lies entirely above the current route's (lower bound above the
  upper bound); the route must fit the card. The reason is shown with
  "Without those results the tool would pick <was>; it stays in the list." The
  Wilson bounds: `z = 1.96`, `centre = p + z²/2n`, `spread = z·sqrt(p(1-p)/n +
  z²/4n²)`, `lower = (centre - spread)/(1 + z²/n)`, upper = `1 - lower(n - worked,
  n)`; worked is clamped to `[0, n]`.
- **Ordering for the autopilot** (`rank_routes`): the offered routes, never added
  or dropped, ordered by evidence: (3) a route that **worked in this game** (by
  Wilson lower bound); (2) a route with a worked result and at least 5 results in
  the class table, once any offered route has 5 there (else across all games);
  (1) a route nobody tried, in the order offered; (0) tried in this game and never
  worked, last (ordering these by their own numbers once put the route with the
  most failures second, and with three attempts the untried one was never reached).
  Each comes back with its counts ("<w> of <n> in this game", "... in <class
  words>" or "across every game shared").
- **After a failed route** (`next_route`): the sentence naming what to try next.
  (1) in this game, another allowed route that worked for somebody, ordered by
  worked then rate: `In this game the <route> route is reported working by <w> of
  <t>. Try that next - pick it in the route dropdown and install again.` (2)
  otherwise the class (or all games), another allowed route with at least 5
  results and a **higher** rate than the failed one: `Nobody has reported this game
  yet. <Across ...>, the <route> route worked in <w> of <t> tries[, against <w> of
  <n> for <tried>] - it is the next one to try.`
  - Changed from the reference: branch (2) says "Nobody has reported this game
    yet" even for a game with reports where none of the other routes worked (it
    is reached whenever branch (1) finds nothing); it says "No other route has
    worked in this game yet." in that case. Branch (2) also ranks by plain rate,
    where `rank_routes` and the steering use confidence bounds: use the bounds.
- **Library row** (`ctl_library.py:375-390`): "<w> of <n> worked for others" for
  the route with the most worked results, only when one worked, **at any number
  of reports**.
  - Changed from the reference: the row follows the same five-result rule as
    everything else. Why: "1 of 1 worked for others" is the single-report finding
    the module refuses elsewhere.
- Everything above is **advice**: nothing writes a setting, and a route is never
  removed from the list.

### The page

A single HTML page, served by the Worker, everything inline: no CDN, nothing
fetched after it loads. Title "DLSS 5 Autopilot - compatibility"; a line "<reports> shared results
across <games> games, rebuilt <day>. A result belongs to one card, driver and build
- the same game can work on one and fail on another."

- A search box (game name or executable, case-insensitive) and four filters: route,
  card family (`RTX 20/30/40/50` from the `sm` through the shared table, else
  `other`), driver (newest first by numeric order), result (any, worked, did not
  work). Only results matching all filters count in a game's line, and a game with
  none left is hidden. Without per-configuration data (the page built from the feed
  alone), the card and driver filters are disabled and a note says so.
- Games are sorted by number of results (most first), then name. Each game shows
  its name, "worked in W of N" coloured (all, none, some) and a chip per route
  `<route> w/n`. Opening a game shows, per measured route, "<route>: <res>% work
  area[, <fps> fps][, <ms> ms a frame for the model and feed together (feeder) | ms of
  model a frame] - the middle of <n> measured result(s)", then a table: route, build,
  api, card (`NVIDIA `/`GeForce ` stripped), driver, tool, result ("worked" / "did not
  work" for one report, "worked W of N" for several), last seen (the day).
- "Nothing matches. Clear a filter, or be the first: install it with the tool and
  share the result." A footer: a result gets there when somebody presses **share the
  result** and confirms; results the tool could not see and nobody confirmed are left
  out; and the limit stated under "Anti-abuse", in one sentence.
- **Every string in the data was sent by a client nobody controls.** The rows are
  built with `textContent`, never HTML; the data sits in a `<script
  type="application/json">` block that must not be able to close itself or open a
  comment: **every `<` in it is written `\u003c`** (the takeover's requirement; the
  reference replaced only `</` and `<!--`, which is the same defence done by
  substring). The link to the repository is HTML-escaped.
- **A content-security policy** in the response: only the page's own inline
  script, by hash, and no other source; a second defence on top of escaping
  and `textContent`.
- Run alone with only the feed, the page works with the per-game sums and says so.

## What must never happen

- Anything leaving the machine without the person pressing **share the result**
  and confirming what is sent.
- A path, a user or machine name, a hardware id or a free-text field in a record or
  in the verdict that accompanies it.
- An IP address stored anywhere, or an install id, IP hash or record id in the
  published feed or page.
- An unseen outcome counted as a failure; a setup mistake counted against a route.
- One install's results counted twice for one game and route.
- A malformed request breaking the feed, widening a table beyond its bounds, or
  putting HTML in the page.
- Advice from fewer than five distinct sources for a game, or a measurement from
  fewer than three, or a route steered by less than 20 results per route in a
  class with non-overlapping intervals.
- A shared-result request blocking an install, or a feed failure shown as an error.
- The shared results deciding an install by themselves.

## What the reference did

Kept as a record; none of it is the design.

- **Sharing** opened a pre-filled GitHub issue in the person's browser
  (`community.py:792-856`), `https://github.com/<owner>/<repo>/issues/new?labels=result&title=...&body=...`,
  posted under the person's own GitHub account. The body carried the verdict line,
  a list of the facts and a machine-readable block: `<!-- autopilot-report`, the
  record as compact JSON, `autopilot-report -->` (`community.py:191-217`).
- **The confirmation**, before any browser opened: "A browser window opens with the
  result in it - nothing is sent unless you post it. It carries the game's name
  and executable, the route and build, the graphics api, <what else, below>. No
  paths, no user name, nothing else." Buttons "open it" / "cancel". The list is
  built from the record actually being sent: whether the game ships DLSS, FSR or
  XeSS; the card and driver, the tool's version, whether it worked (or "that it
  worked / did not work (your answer)" or "(Windows recorded a crash)"), the
  one-line verdict, and, when measured, the work area, the milliseconds and the
  frame rate.
- **The issue** (`community.py:792-856`) opened in the user's default browser as
  `https://github.com/<owner>/<repo>/issues/new?labels=result&title=...&body=...`.
  - Title: `result: <game or exe or ?> - worked` or `- did not work`.
  - Body, in order: `**<game>** worked|did not work on the `<route>` route.`;
    the verdict line (the diagnosis's one-line verdict, passed as a note) when
    there is one; "The tool could not see this from the logs; the person who
    played it said so." for `person`; "Windows recorded the game crashing in this
    session." for `crash`; the list `- api: ..`, `- the game ships: DLSS | FSR,
    no DLSS | XeSS, no DLSS | no DLSS, FSR or XeSS`, `- build: ..`, `- gpu: <name>
    (<sm>), driver <d>`, `- tool: <version>`, and `- measured: <res>% work area[,
    <ms> ms a frame for the model and feed together (feeder) | <ms> ms of model a
    frame][, <fps> fps]` when a work area was measured; then "The block below is
    what the compatibility list reads. Once a game has five results, the next
    person with it is told which route worked most often on it; three results on
    the same route that worked and carried a measurement tell them what those
    sessions ran at. Delete it if you would rather not share it - the rest of the
    report still stands." and the block.
  - The person reads it, edits it, posts it or closes it. Deleting the block
    leaves a plain issue that counts for nothing.
- **The block** (`community.py:191-217`): `<!-- autopilot-report` newline, the
  record as compact JSON with sorted keys, newline, `autopilot-report -->`,
  preceded by a blank line. A reader finds the first marker and the first
  closing marker after it, parses the JSON, and accepts it only if it is an
  object, `v` is `1`, and `result` is `worked` or `failed`.
- **The builder** ran in GitHub Actions: two jobs and a publisher, all in
  `.github/workflows/compatibility.yml` (permissions `{}` at the top, each job
  asking only for what it needed; every action pinned by commit hash). It read
  every issue of this repository and, read-only, of the one it was forked from
  (`Kizzuwatnaa/DLSS5-Autopilot`), parsed the blocks, applied the counting rules
  now in "The server" (plus seven hand corrections of upstream issues whose
  titles disagreed with their blocks: #414, #388, #307, #257, #242, #222, #437),
  kept one vote per issue author, and published the feed and page with GitHub
  Pages at `https://domoarigatomrburato.github.io/DLSS5-Autopilot/`.

- **`file`**, on a new issue only: if the body parses as a record, ensure the
  `result` label exists (colour `0E8A16`, "a shared compatibility result, read
  into the compatibility list"), add it, and **close the issue as completed** with
  a thank-you comment linking the list, saying that five results trigger the
  advice, that measured results tell what sessions ran at, and why it was closed:
  "it is a record rather than a bug report, so the issue list keeps showing the
  things that still need someone. It still counts. If you want to add what you
  saw ... reply here." (a burst of 16 results arrived in the first four hours of
  1.8.0). Permissions: `issues: write`, `contents: read`. Its own concurrency
  group per issue, **never cancelled**. Only issues whose body parses as a record
  are touched. The label is not trusted anywhere: GitHub silently drops `?labels=`
  from a pre-filled new-issue URL for anyone without triage rights, so most
  results arrive unlabelled; the parser is the filter.
- **`build`**, on issue opened, edited, labeled, unlabeled, closed, reopened or
  deleted, daily at 04:17 UTC (so a deleted issue drops out), and by hand: reads
  every issue of the repository (`state=all`, so closed results count) and builds
  the feed and page into a folder. The newest run cancels one in progress (it has
  read everything anyway). Permissions: `contents: read`, `issues: read`.
- **`pages`**, after `build`: publishes the folder with Pages; it must not fail
  silently, because Pages is the feed the tool reads. A read failure in `build`
  fails the run and Pages keeps the previous list rather than publishing one that
  lost a side.
- Nothing was committed to any branch.

## Bugs and questionable choices in the reference not to port

- Gone with the builder: the 50-page cap on the issue read
  (`compatibility.py:152-177`); counts that followed an issue edited after the fact
  (`compatibility.py:203-209`); whether a result counted decided by parsing the
  prose body (`community.py:152-161`); the correction table as code with seven
  magic issue numbers (`compatibility.py:57-60,72-89`); a fixed 60 s per request
  with no retry (`compatibility.py:169`).
- `community.py:282-287`: the game key is the executable's file name only; generic
  names (`game.exe`, `launcher.exe`, `start.exe`) merge different games. See open
  questions.
- `community.py:327-347`: "worked in <w> of them" (above); the sentence on the
  route chosen compares counts, not rates.
- `community.py:771-783`: the "next route" ranking by plain rate, and its false
  "Nobody has reported this game yet" (above).
- `ctl_library.py:375-390`: the library row ignores the five-result threshold.
- `compat_page.py:215-219`: `</` and `<!--` replaced by substring rather than every
  `<` (above).
- `compat_page.py:31`: the page builder imported the application's whole GPU module
  for a name table; share one small table, not the module.
- `community.py:36-46`: `route_name` imports another module at call time inside a
  `try`, so any error silently shows the raw key.
- The feed had no version field (above).
- A route key retired from the tool still appears in old results, and a rename
  would split its counts; keep a table of aliases in the Worker's data.

## To verify against primary sources

| Claim | Source that settles it |
| --- | --- |
| The current limits of the Workers and D1 free plans (requests a day, CPU time, database size, reads and writes a day) against the expected traffic | Cloudflare's Workers and D1 pricing and limits pages |
| That the free plan offers a rate-limiting rule for a Worker route, and its limits | Cloudflare's WAF rate-limiting rules docs |
| The Workers request headers that carry the client IP, and that nothing else logs it on the free plan | Cloudflare docs; the account's log settings |
| The CDN cache rules for a Worker response | Cloudflare cache docs |
| the Wilson interval formula and its use as a lower bound | any statistics reference (Wilson 1927); the module's own tests |
| the person who sends a record ran the tool: nothing proves it | none; stated as a limit |
| the verdict stage table (which verdict is "unseen") | the diagnosis spec; shared with the Worker as data |

## Fixtures

- Records: one of each outcome (`worked`, `failed`, `by:person`, `by:crash`), with
  and without `res/ms/fps`, `up`, a hand-written one with `Infinity`, a number as
  `exe`, a list as `route`, a 200 000-character game name, `Feeder` in capitals, a
  `v: 2`, an unknown verdict id, and a body that is not JSON.
- Records taken from the shared-result issues in the report corpus
  (`_tools/reports/`), one per outcome, the unseen-verdict and not-a-route cases
  among them, as the Worker would receive them.
- Requests: one install id with three results for the same game and route (the
  last wins) and one with the same game on two routes (both count); five install
  ids behind one hashed prefix on one day (one source); a burst over the rate
  limit (refused, nothing stored).
- A small feed in each shape (current; before 2.0.6 without tables; with a game of
  fewer than five distinct sources; with a class of 20+ results for two routes whose
  intervals overlap and one pair whose intervals do not), and the advice sentences
  above for each.
- The page: a feed whose game is named `</script><img src=x onerror=alert(1)>` and
  another with `<!--`; both must appear as text and create no element.
- Driver notes: the 616.64/617.14 example above as a per-driver table.

## Open questions

1. **Results already filed as GitHub issues.** Import them once into D1 (with the
   reference's counting rules and the seven corrections), or start empty? They
   carry no install id, so each would be its own source.
2. **The game key.** Executable name only merges generic names. Add a store id
   (`steam:<appid>`, which is public and not personal) to the record, and key by it
   when present?
3. **A `vr` field**, or an `os`/`proxy` field, so reports from untried setups (a
   headset) are countable? Each new field widens what leaves the machine.
4. **Do results need the tool's version** in the published configuration rows
   (`tool`)? It makes rows rarer and the page wider; it is how a fix is told apart
   from a regression.
