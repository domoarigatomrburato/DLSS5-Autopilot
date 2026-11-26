# Network and downloads

Every component the tool installs is fetched at run time from its
publisher, over HTTPS, into a per-user download cache. This spec covers that
path, the verification a file passes before it enters the cache, the cache
itself, and the archive extraction behind it. Read from the reference's
`core/net.py` and `core/sources.py`, and from tests written against them.

## What the user gets

- **A download is fetched once.** It lands in the per-user cache (below) and
  the next install reuses it without a request. Progress is reported as
  (bytes so far, total). The last report is (size, size).
- **A wrong file never reaches the cache.** The file's magic bytes are
  checked against what its name says, and the caller's verification runs
  (below), before it is moved into place. For a zip that means the whole
  archive: an HTML error page saved as `.zip`, or a zip missing its central
  directory, is refused. A bad file already in the cache is fetched again.
- **A server that hiccups is ridden out** (see "Retries").
- **A failure is said in words.** On the last attempt the failure becomes
  an explanation that names the host and ends with what to do. The network
  kinds are:
  - **blocked:** a firewall or antivirus;
  - **dns:** the name does not resolve;
  - **refused:** nothing is listening;
  - **timeout:** no answer in time, or no bytes for too long;
  - **cut:** the connection dropped mid-transfer.

  Classify by error code first and by message text only as a fallback,
  because Windows error text is localised. The diagnosis reads these kinds
  too.
- **Certificates.** TLS is always verified, never switched off. Trust the
  system store only. A refusal is a typed failure, explained by its reason:
  - the name does not match;
  - the clock is wrong (dates);
  - an antivirus is inspecting traffic (unknown issuer, often self-signed);
  - a root is missing.
  - Changed from the reference: no bundled root set. It existed only because
    Python's `ssl` ignored the Windows store; .NET builds the chain against
    it. `ServerCertificateCustomValidationCallback` is never set, anywhere.
- **A full drive.** Disk full is recognised through any chain of wrapped
  errors, by errno 28 and Windows codes 112 and 39. The message names the
  drive the failed write was on, and the game's and the cache's drives with
  their free space. It ends with "free some up, then install again". The
  partial file is removed.
- **GitHub.** A 403 or 429 from `api.github.com` means rate limited, and
  says so. When the API is rate limited, the same release data is read from
  github.com's release pages, in the API's shape ([components](components.md)).
  No GitHub token is read or sent: 403 and 429 are handled by `Retry-After`,
  the rate-limit headers and the cached answer, because a token is a secret
  the tool would have to keep.

### One HTTP path

- One client, built on `SocketsHttpHandler`, serves every request of the
  tool: components, NVIDIA's runtimes, cover art, the compatibility feed and
  the self-update.
- `AllowAutoRedirect` is off. The client follows redirects itself, at most
  5 hops, and checks every hop before it is asked:
  - the scheme: `https` only; an `https` to `http` hop is refused;
  - the host: it must be on the caller's allowlist (below).
- Only `https` is opened. `http`, `file:` and `ftp:` are refused, since URLs
  come from API answers.
- The client takes its `HttpMessageHandler` from the caller. Production
  passes the `SocketsHttpHandler`; tests pass an in-memory handler.
- Every response, failed ones included, is disposed.
- The user agent is `dlss5-autopilot/<the running version>`.

### Hosts

The allowlist is data the caller passes with each request; the client holds
no list of its own. A request or a redirect hop to any other host is refused.
These are all the hosts the tool talks to, by the area that passes them:

| Area | Hosts |
| --- | --- |
| [components](components.md) | `reshade.me`, `api.github.com`, `github.com`, `codeload.github.com`, `raw.githubusercontent.com`, GitHub's release-asset hosts |
| [dlss-updates](dlss-updates.md) | `github.com` (the tag redirect), `raw.githubusercontent.com` (the files), plus the GitHub API fallbacks above |
| [self-update](self-update.md) | `api.github.com`, `github.com`, GitHub's release-asset hosts |
| [remix](remix.md) mod zips | `api.github.com`, `github.com`, GitHub's release-asset hosts |
| [library](library.md) cover art | `store.steampowered.com`, `api.steampowered.com`, `shared.fastly.steamstatic.com`, `cdn.cloudflare.steamstatic.com`, Epic's image hosts |
| [community](community.md) feed and shared results (M8) | the project's Cloudflare Worker host, named once it is deployed |

"GitHub's release-asset hosts" and "Epic's image hosts" are named exactly
once they are verified (below); a wildcard is never used.

## Verification

One contract, run on the finished `.part` file before it enters the cache.
The caller says which kind applies:

| Kind | Passes when |
| --- | --- |
| expected SHA-256 | the file's SHA-256 equals the given value (from a pin shipped with the release, or GitHub's asset `digest`) |
| Authenticode signer | the file carries a valid signature whose signer is the given publisher. Windows only, behind an interface; tests pass a fake |
| sums file | a sums file of the same release (signed, or published beside the asset) lists the file by its exact name, and the hash matches. The sums file comes through this same path |
| none, with a reason | no publisher offers any of the above. The reason is logged and recorded with the component; the magic-byte check still runs |

- A kind the caller named that cannot be completed refuses the file. A sums
  file that cannot be read is a refusal, never a silent "none": a network
  that blocks one small file must not turn the check off.
- A refused file is deleted and never enters the cache.
- A cached file is verified again before use when the kind is a signer, and
  its digest is part of its cache key otherwise (below).
- [self-update](self-update.md) and [app](app.md) point here for the
  release's verification; [components](components.md) and
  [dlss-updates](dlss-updates.md) say which kind each component takes.

## Timeouts

| Timeout | Value | Applies |
| --- | --- | --- |
| connect | 15 s | every request |
| read stall | 30 s with no bytes received | every request |
| overall, text and JSON | 60 s | API answers, pages, the feed |
| overall, files | 2 h | downloads; a backstop only, since the stall timeout catches a dead transfer |

A caller may pass a shorter overall value (cover art passes 15 s). Every
value is injectable, so tests do not wait.

## Cancellation

- Every public API takes a `CancellationToken`.
- Cancelling stops the transfer at the next chunk, removes the `.part`
  file, releases the cache lock, and reports "cancelled", which is never
  retried and never classified as a network failure.

## The cache

`net` owns the layout. Other specs name what they cache, never where.

| What | Where |
| --- | --- |
| downloaded files | `%LOCALAPPDATA%\dlss5-autopilot\cache\files\<key>-<name>` |
| API answers, pages, the feed | `%LOCALAPPDATA%\dlss5-autopilot\cache\api\<key>.json` |
| unpacked archives | `%LOCALAPPDATA%\dlss5-autopilot\cache\unpacked\<key>\` |
| lock files | beside each entry, `<entry>.lock` |

- **Identity.** `<key>` is the first 32 hex digits of the SHA-256 of the URL
  plus the expected digest when the caller knows one. A publisher that
  replaces an asset under the same URL gets a new entry, because its digest
  differs. `<name>` is the asset's own name, validated as a file name by the
  rules under "Extraction"; it is a label for people, never the identity.
- **Locking.** The CLI and the app can run together, so an entry is written
  under a cross-process lock: the lock file is opened exclusively for the
  download, the verification and the rename into place. A second process
  waits for the lock (cancellably), then finds the finished entry and uses
  it. Finished entries are never rewritten in place, so reading one needs no
  lock; deleting a bad one takes the lock.
- **Writing.** Downloads go to `<entry>.part`, then are renamed into place
  after every check passes.
- **Answers.** An API answer is fresh for 6 hours and is used at any age
  when the live request fails ("Retries").
- Changed from the reference: the reference cached by a label
  (`dlssnr-<label>.zip`), so a replaced asset was served stale forever, and
  kept API answers in a sibling `api-cache` folder.

## Retries

- **3 attempts** in all. The waits are 1 s then 4 s, injectable.
- Retried: 5xx and 408 answers, and dropped connections (refused mid-way,
  cut short, a body shorter than its length). Every other status is the
  answer: a 404 is reported at once, with its body readable.
- **`Retry-After`** (seconds or an HTTP date) replaces the wait when it is
  60 s or less. A longer one ends the attempts and goes to the fallback,
  saying when the server will answer again.
- **GitHub rate limits.** `x-ratelimit-remaining` and `x-ratelimit-reset`
  are read on every GitHub API answer. With 0 remaining, no further API
  request is made until the reset time, for the whole process.
- **Fallback.** When the attempts end, an API answer of any age from the
  cache is used, and the caller is told its age. With none, the caller's own
  fallback runs (the github.com pages, see [components](components.md)).
- Never retried: programming errors, a refused redirect, a size limit, a
  failed verification, a cancellation.

## Extraction

One extractor in Core opens every archive the tool meets. No process is
started for it:

| Format | Read with |
| --- | --- |
| zip | `System.IO.Compression` |
| tar.gz | `System.Formats.Tar` over `GZipStream` |
| 7z | SharpCompress |

- No PATH lookups, no Windows `tar.exe`, no downloaded `7zr.exe`.
  - Changed from the reference: it tried an installed 7-Zip on the PATH,
    then Windows' `tar.exe`, then fetched and ran `7zr.exe`
    (`optiscaler.py`). A program found on the PATH runs with the tool's
    rights.
- Extract one member by its ending, ignoring case and matched on whole path
  parts, or a tree.
  - Changed from the reference: it took the first match. More than one match
    is refused; the caller passes a longer ending.
- Extracting a tree can flatten one folder level and filter by extension or
  name, ignoring case.
- Returned paths keep the caller's spelling of the destination. A
  destination reached through a junction must not come back resolved.
- Write to a `.part` file or folder and rename into place only after the
  checks pass.

**Archive limits**, checked from the headers before anything is written and
enforced again while writing, since headers can lie. Defaults, which a caller
may lower:

| Limit | Default |
| --- | --- |
| entries | 50,000 |
| total expanded size | 16 GiB |
| one entry's expansion | 200 times its compressed size, for an entry over 1 MiB; for a solid 7z block, the block's ratio |

- A tar.gz has no index, so its limits are enforced in one streaming pass
  into the `.part` folder, with the ratio taken over the whole archive
  (expanded size to `.tar.gz` size); a refusal deletes the `.part`.
- A tar symbolic link or hard link is refused.

**Names.** Member names and cache file names are read the way Windows reads
them, on every host:

- both slashes are separators; drive letters (`C:x`, `C:/x`, `C:\x`) and
  leading slashes are recognised;
- `..` anywhere is refused;
- an alternate data stream (`:` in any part) is refused;
- a reserved device name (`CON`, `PRN`, `AUX`, `NUL`, `COM1` to `COM9`,
  `LPT1` to `LPT9`), with or without an extension, is refused;
- a part ending in a dot or a space is refused;
- two members whose paths are equal ignoring case are refused.

## What must never happen

- **An https request redirected to http** must be refused before the http
  server is asked. An https to https redirect on the allowlist is followed.
- **A request to a host off the caller's allowlist**, by URL or by redirect.
- **A download without a size limit.** Every read has a maximum, given by
  the caller or a default: 1 GiB for files, 32 MiB for text and JSON. RTX
  Remix mods get 2 GiB, GitHub's asset cap; one real mod zip is 524 MB.
  NVIDIA's runtimes get 256 MiB ([dlss-updates](dlss-updates.md)).
  - The declared length is checked before anything is written.
  - The limit is enforced while streaming, because a server can lie or send
    no length.
  - The partial file is removed.
- **A file entering the cache before its verification passes.**
- **A file written outside the folder it belongs to.**
  - Paths are compared by whole parts, never by string prefix:
    `../game-other` must not pass as inside `game`.
  - An absolute member is accepted only where the caller allows it, and only
    inside the root.
  - Nothing is written outside the destination.
  - Cache file names built from remote tags and asset names are validated
    the same way.
- **An archive that expands past its limits**, or a tar link written at all.
- **Retrying what is not a network failure.**
- **Leaking a connection.**
- **Custom certificate validation**, or any trust root besides the system
  store.
- **Running a program to unpack an archive.**

## Bugs in the reference not to port

- `urlopen` with the default redirect handler followed https to http, and
  would open `file:` and `ftp:`.
- No host allowlist was enforced (`sources.py:1` promised one).
- No size limit anywhere except cover art.
- The retried HTTP error responses were never closed: a socket leak.
- There were four retry loops, each with its own waits, attempt counts,
  rate-limit handling and messages. The file download never recognised
  GitHub rate limits.
- `except Exception` retried programming errors as network failures.
- A truncated download surfaced as a raw "incomplete download" instead of
  the "cut" explanation.
- Drive-letter rules followed the host OS, so the escape checks could not be
  tested off Windows.
- A bundled certificate bundle beside the system store.
- `.7z` archives were opened by whatever 7-Zip the PATH offered, by
  `tar.exe`, or by a downloaded `7zr.exe`.
- Unused cache-size and clear-cache helpers, and unused parameters.

## To verify against primary sources

| Claim | Source |
| --- | --- |
| The exact host names GitHub's release downloads redirect to (`objects.githubusercontent.com`, `release-assets.githubusercontent.com` or others) | observe a redirect chain for a release asset today |
| The host names of Epic's image URLs in the launcher's catalog cache | a real `catcache.bin` |
| SharpCompress opens the real OptiScaler `.7z` archives, including BCJ2 filters, and reports entry sizes for solid blocks | SharpCompress docs and source; y4my4my4m's real archives |
| `System.Formats.Tar` exposes symbolic and hard link entries so they can be refused, and DXVK's archive has none | .NET docs; a real `dxvk-<v>.tar.gz` |
| GitHub's `Retry-After`, `x-ratelimit-remaining` and `x-ratelimit-reset` on 403 and 429 answers | GitHub REST rate-limit docs |
| `SocketsHttpHandler.ConnectTimeout` and a read-stall timeout built on the response stream cover the cases above | .NET docs |
| On Windows: whether .NET's chain build triggers the Windows root auto-update (a root missing from the store is fetched on first use) | Windows PKI docs; a test on a clean Windows install |
| The Windows errors for a full disk (112, 39) and the .NET exceptions that wrap them | Win32 error docs; a test on a full volume |

## Fixtures

- **An in-memory `HttpMessageHandler`** that answers from a table of URLs:
  status, headers, body, a body cut short, a declared length that lies, a
  slow body that stalls, a redirect chain (https to https on the list, https
  to http, to a host off the list, a loop past 5 hops). Tests stay offline.
- **Rate-limit answers**: 403 and 429 with GitHub's body and headers, with
  and without `Retry-After`, with `x-ratelimit-remaining: 0`.
- **TLS failures**: the typed kinds raised by a fake handler, and the
  messages they produce.
- **Archives built in the test**: zip, tar.gz and 7z with a normal tree; a
  member with `..`, a drive letter, an absolute path, a `:` stream, `CON`,
  `aux.txt`, a trailing dot, a trailing space, two names equal ignoring
  case; a tar with a symbolic link and a hard link; an archive past each
  limit (a highly compressible entry, too many entries); an HTML page named
  `.zip`; a zip missing its central directory.
- **A cache folder** with a finished entry, a `.part` leftover, a held lock
  from another process (a second test process), and an entry whose digest
  no longer matches.
- **Disk full**: a stream fake that throws errno 28 and Windows 112 and 39,
  directly and wrapped.
- **Real reports**: `_tools/reports/` 175 (a certificate for another name),
  434 (a blocked socket), and the 140-era "not a zip" cases.

## Open questions

- None open.
