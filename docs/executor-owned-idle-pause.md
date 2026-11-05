# Executor-Owned Idle Pause

## Native Failure And Scope

The first current-geode smoke on clean `308d701f` used the retained Year-5
CrabPotAudit baseline. The existing pre-Year-3 admission correctly stopped it
before dispatch. Its baseline/copy hashes matched. This is an unsuitable test
starting point, not acquisition proof or a reason to relax the deadline.

The subsequent isolated ProofFarm baseline (Year 1, spring 7) reached the real
current-source axes, but its native movement context reported `game_paused=true`
and `route_timing_ready_now=false`. The harness itself had applied its configured
idle pause. The calibration loader could not distinguish that dispatch-releasable
pause from ordinary native pause. Location production then hid the original
timing rejection behind a generic missing-input reason. No acquisition was
executed in either run; diagnostics remain local and are not promoted.

## One Existing Clock Controller

The original harness clock branch still applies/releases pause. It now takes
ownership only when it actually changes `Game1.paused` from false to true;
pre-existing pause is not claimed or released as its own. An ownership epoch
advances only on acquisition, not per frame.

An optional read-only SMAPI API reports the actual owner flag, empty dispatch
queue, absence of active operation/movement/native night transition/menu, actor/world
identity, epoch and the original release-before-pending-execution policy. The
bridge reads it synchronously while collecting the existing movement context.
Absent/unloaded/incompatible APIs provide no authority. It neither calls HTTP
from the game thread nor changes game state, and adds no snapshot polling.

The bridge keeps the true `game_paused` and `route_timing_ready_now` values.
The original calibration loader may use the owned release only with exact
identity/schema/policy, a positive epoch and all other native movement gates
clear. Missing evidence, ordinary pause, dialogue, fade, warp, freeze, event,
tool use, immobilization or incompatible movement still block. Existing runtime
calibration constants and fresh source/dispatch/native checks are unchanged.
There is no preference label, new executor, Teacher authority or training grant.

A saved, safe next-day state releases native night ownership even if the
composite acceptance still awaits its first planned work receipt. The same
RuntimePrimitives guard governs native sleep, idle pause and this read-only
evidence API. The remaining proof must not keep ordinary legacy callers running
their clock or prevent their next sleep. Full composite acceptance is unchanged.

No per-tick timestamp is added to semantic state: a stable idle pause must not
invalidate every cached plan. The existing snapshot freshness/identity protocol
continues to bind observations. Location reports now retain actual calibration
blocking reasons instead of passing a null calibration to a generic producer.

## Baseline Preflight And Verification

`current_geode` now streams the source XML calendar before builds/deployment or
game launch. Invalid/duplicate dates and starts too late to leave the following
morning before Year 3 reject. DTD/external entities are prohibited. The original
C# admission independently rechecks the live date; held-item capability mode
keeps its former semantics.

Focused calibration/ownership tests pass 40 cases; boundary/preflight checks
pass 33 without launching a game, including the actual entry's late-date refusal.
The current-native calendar integration passes 18 checks, including owned/unowned
pause and retention of the original timing reason. Harness and bridge build against explicit E
game references with zero warnings/errors. The required native
acquisition/custody/donation/consumption trace has since passed the
[accepted current-geode boundary](geode-dialogue-lifecycle.md#accepted-native-boundary),
including normal save/night/key and source preservation. Unknown acquisition routes and
strict-frontier rules are not waived by this repair.

Use an explicitly earlier isolated baseline such as `ProofFarm_450250338` for
current-geode validation. A fixture containing 59 donations remains a threshold
capability test even on a Year-1 save: it does not prove acquiring 60 distinct
items from a new save, the full Rusty Key criterion or Teacher partitions.
Coverage counters and `formal_product_training=false` remain frozen.

The first candidate `84b866ba` exposed an interop mistake in the new API:
SMAPI's locked `ModRegistryHelper` checks `Type.IsPublic`, so a nested public API
class was refused (confirmed by the native SMAPI warning). The actual API is now
a top-level game-free runtime primitive with a type/surface/callback regression
test. API failures retain bounded explicit unavailable diagnostics, never
authorization. The CI failure on that candidate was a negative fixture mutating
the snapshot reused by the next runtime-source test; negative cases now use a
separate short sibling fixture, and the original snapshot hash is checked intact.

The threshold launcher also now binds the existing Backend repository environment
setting to `runRoot/strategy-ledger` and restores it afterward. Earlier attempts
read a historical ProofFarm ledger (revision 233); a new isolated run must not
silently reuse that test history. No old ledger, reservation or filesystem item
is removed. The native rerun must independently confirm its own ledger and API
evidence before any acquisition/consumption claim.

The corrected `8226ae8c` candidate has since passed exact-head CI and a partial
native acquisition/custody trace; API evidence and revision-0 isolation were
observed in the real game. It stopped before donation on stale dialogue/control
state left by the original geode response path. See the
[native lifecycle follow-up](geode-dialogue-lifecycle.md); this is not a complete
criterion or a reason to reopen the pause admission guards.
