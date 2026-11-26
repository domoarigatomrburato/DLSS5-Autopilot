# Network and downloads

Every component the tool installs is fetched at run time from its
publisher, over HTTPS, into a per-user download cache. This spec covers that
path and the archive extraction behind it. Read from the reference's
`core/net.py` and `core/sources.py`, and from tests written against them.

## What the user gets

- **A download is fetched once.** It lands in the per-user cache
  (`%LOCALAPPDATA%\dlss5-autopilot\cache`) and the next install reuses it
  without a request. Progress is reported as (bytes so far, total). The last
  report is (size, size).
- **A wrong file never reaches the cache.** The file's magic bytes are
  checked against what its name says, before it is moved into place. For a
  zip that means the whole archive: an HTML error page saved as `.zip`, or a
  zip missing its central directory, is refused. A bad file already in the
  cache is fetched again.
- **A server that hiccups is ridden out.** 5xx and 408 answers and dropped
  connections (refused mid-way, cut short, a body shorter than its length)
  are retried. The wait is a fixed step times the attempt number,
  injectable so tests do not wait. Every other status is the answer: a 404
  is reported at once, with its body readable.
- **A failure is said in words.** On the last attempt the failure becomes
  an explanation that names the host and ends with what to do. The network
  kinds are:
  - **blocked:** a firewall or antivirus;
  - **dns:** the name does not resolve;
  - **refused:** nothing is listening;
  - **timeout:** no answer in time;
  - **cut:** the connection dropped mid-transfer.

  Classify by error code first and by message text only as a fallback,
  because Windows error text is localised. The diagnosis reads these kinds
  too.
- **Certificates.** TLS is always verified, never switched off. Trust the
  system store plus a bundled root set (for machines whose store is
  outdated). A refusal is explained by its reason:
  - the name does not match;
  - the clock is wrong (dates);
  - an antivirus is inspecting traffic (unknown issuer, often self-signed);
  - a root is missing.
- **A full drive.** Disk full is recognised through any chain of wrapped
  errors, by errno 28 and Windows codes 112 and 39. The message names the
  drive the failed write was on, and the game's and the cache's drives with
  their free space. It ends with "free some up, then install again". The
  partial file is removed.
- **GitHub.** A 403 or 429 from `api.github.com` means rate limited, and
  says so. When the API is rate limited, the same release data is read from
  github.com's release pages, in the API's shape.

## What must never happen

- **An https request redirected to http** must be refused before the http
  server is asked. An https to https redirect is followed. Only the `http`
  and `https` schemes are opened, never `file:` or `ftp:`, since URLs come
  from API answers.
- **A download without a size limit.** Every read has a maximum, given by
  the caller or a default: 1 GiB for files, 32 MiB for text and JSON. RTX
  Remix mods get 2 GiB, GitHub's asset cap; one real mod zip is 524 MB.
  - The declared length is checked before anything is written.
  - The limit is enforced while streaming, because a server can lie or send
    no length.
  - The partial file is removed.
- **A file written outside the folder it belongs to.** Archive member names
  and cache file names are read the way Windows reads them, on every host:
  both slashes, drive letters (`C:x`, `C:/x`, `C:\x`), and leading slashes.
  - `..` anywhere is refused.
  - Paths are compared by whole parts, never by string prefix:
    `../game-other` must not pass as inside `game`.
  - An absolute member is accepted only where the caller allows it, and only
    inside the root.
  - Nothing is written outside the destination.
  - Cache file names built from remote tags and asset names are validated
    the same way.
- **Retrying what is not a network failure.** Programming errors, a refused
  redirect and a size limit are never retried.
- **Leaking a connection.** Every response, failed ones included, is
  disposed.

## Extraction

- Extract one member by its ending, ignoring case, or a tree.
- Extracting a tree can flatten one folder level and filter by extension or
  name, ignoring case.
- Returned paths keep the caller's spelling of the destination. A
  destination reached through a junction must not come back resolved.
- Write to a `.part` file and rename into place only after the checks pass.

## Bugs in the reference not to port

- `urlopen` with the default redirect handler followed https to http, and
  would open `file:` and `ftp:`.
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
- Unused cache-size and clear-cache helpers, and unused parameters.
