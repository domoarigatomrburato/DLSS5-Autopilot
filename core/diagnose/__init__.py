r"""Read the logs a game left behind and say what happened.

One module until 1.9.0, and 3,700 lines of it: the shapes, the readers,
one reader per route, the bug report's text and the chain that decides
the order - all in the file every fix had to touch. Split by what each
part answers, with the import path unchanged:

    model.py     the Report, the constants, the phrases
    layer.py     whether ReShade reaches the game as a Vulkan layer
    evidence.py  what the folder, the logs and the process say
    process.py   what the game's process had in it: ours, and whose else
    helper.py    the 64-bit helper a 32-bit game's feed hands frames to
    live.py      the bridge's own log, tied to ReShade's session
    routes.py    one reader per route
    body.py      the text of a bug report
    chain.py     analyse(), and the order it reads things in

`from core import diagnose` and `diagnose._manifest(...)` work as before:
the names callers outside the package use are re-exported here, one import
each. STANDALONE_LOG,
_layer_state and _user_data_roots are patched by the suite and by the
replay tools: patch them on `diagnose.model`, which is where they live
and what the package reads.
"""

from . import (
    body as body,
    chain as chain,
    evidence as evidence,
    helper as helper,
    layer as layer,
    live as live,
    model as model,
    process as process,
    routes as routes,
)
from .body import (
    PASTE_FIRST as PASTE_FIRST,
    _dlss_record_root as _dlss_record_root,
    _presence as _presence,
    _reshade_excerpt as _reshade_excerpt,
    _tool_log_lines as _tool_log_lines,
    answered as answered,
    anticheat_named as anticheat_named,
    event_line as event_line,
    event_module as event_module,
    issue_body as issue_body,
    issue_url as issue_url,
    refused_reason as refused_reason,
)
from .chain import _explain_no_log as _explain_no_log, analyse as analyse
from .evidence import (
    _NET_VERDICTS as _NET_VERDICTS,
    TAIL_FEED as TAIL_FEED,
    TAIL_OPTI as TAIL_OPTI,
    _addon_switch as _addon_switch,
    _crash_verdict as _crash_verdict,
    _dxvk_gone as _dxvk_gone,
    _feed_shaders as _feed_shaders,
    _game_ran as _game_ran,
    _install_crash as _install_crash,
    _installed_at as _installed_at,
    _last_feed_session as _last_feed_session,
    _last_session as _last_session,
    _live_evidence as _live_evidence,
    _loaded_block as _loaded_block,
    _manifest as _manifest,
    _opti_log as _opti_log,
    _remembered_evidence as _remembered_evidence,
    _route_from_files as _route_from_files,
    _stale_install as _stale_install,
    _tail as _tail,
    _user_data_names as _user_data_names,
    windows_crash as windows_crash,
)
from .helper import _helper_verdict as _helper_verdict
from .layer import _dxvk_files as _dxvk_files
from .model import (
    _CORE_NAMES as _CORE_NAMES,
    _DISPATCH_MS as _DISPATCH_MS,
    _OURS_IN_STACK as _OURS_IN_STACK,
    _RAN_ENTRIES as _RAN_ENTRIES,
    _RAN_SECONDS as _RAN_SECONDS,
    _RESHADE_KEEP as _RESHADE_KEEP,
    _STANDALONE_SESSION as _STANDALONE_SESSION,
    BAD as BAD,
    BRIDGE_LOG as BRIDGE_LOG,
    FEED_LOG as FEED_LOG,
    HOST_LOG as HOST_LOG,
    INFO as INFO,
    MANIFEST as MANIFEST,
    NEVER_RAN_SAID as NEVER_RAN_SAID,
    OK as OK,
    RESHADE_LOG as RESHADE_LOG,
    VULKAN_LAYER as VULKAN_LAYER,
    WARN as WARN,
    Finding as Finding,
    Report as Report,
    drop_never_ran as drop_never_ran,
)
from .process import _foreign_hooks as _foreign_hooks
from .routes import _last_run as _last_run, _overlay_key as _overlay_key

# The three the suite and the replay tools patch are not re-exported: a copy
# would be a value that looks right and is not the one the code reads. Patch
# them where they live - diagnose.model.
PATCHED = ("STANDALONE_LOG", "_layer_state", "_user_data_roots")
