# Deferred Pickup Uses Producer-Time Required Output

## Accepted Parent And Original Intent

PR532 accepted zero-crop-credit native refill recovery on the original persistent
owner. Its final head13169d94 and mainc0717ae3 share treeeaceb580;
CI42455296540 completed42/42 with Core2443/Backend1381 and no failures/skips.
Same-head on-open review6718284786 reported no findings. That work is not repeated.

Issue236 section B already requires completion to use producer-time inventory,
not same-item pickups made before the producer. The original deferred-pickup
contract in commit4b7441003ead75b8b91044210ba8dcdeca1962e7 also requires only
AcquisitionRequiredAmount, not the whole guaranteed drop. Historical controller
context confirms this intent: the October16 session at2026-10-16T12:14:37.981Z,
event ordinal512801, explicitly describes requiring one of six guaranteed items
and rejecting requirements above the guarantee. The original JSON remains local.

## Shared Completion Fix, Not Another Attribution Engine

Original capture already stores inventory at producer start and exact produced
debris/chunk references. The pickup completion gate nevertheless added the required
amount to the older request inventory. An approach pickup could therefore satisfy
the later producer's requirement. The active pickup gate also checked only the
absolute counter, bypassing the original ambiguity rule.

Both completion paths now use the same original producer baseline and fresh
action-origin observation. The extracted game-free arithmetic retains the old
binary rule: disappearance of matching preexisting chunks makes direct inventory
credit zero. No speculative foreign-stack allowance or probabilistic attribution
is introduced. Absolute thresholds use long arithmetic; completion still requires
only the requested quantity, while original initial conservation checks remain.

The active path observes provenance only when the target inventory count changes,
using its existing per-tick count, not a new full snapshot or per-frame debris scan.
The already-collected tagged receipt reports producer inventory and the credited
quantity; it no longer falsely states that all debris disappeared when the required
subset was collected. Untagged ordinary pickup facts and completion stay unchanged.

Capture allowlists, run/queue/route identities, producer timing, retention/consumption,
native pickup, BFS, combat, input, animation and runtime defaults are unchanged.
Ordinary refill-route repairs are not silently relabeled as tree/artifact sources.
Fresh current-E Debris decompilation used the existing DLL SHA256
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee;
no game code, quantities or collection physics were modified.

## Validation And Successor

The new source completion guard failed against the original completion gate before
the fix. Final targeted Core regression passed168/168, including14 pure arithmetic
cases and original action-origin/deferred/tree/mining guards. RuntimeTestHarness
Release against explicit E built with zero warnings/errors. A broader intermediate
run exposed only an old combat source assertion's whitespace sensitivity; two
assertions now match the same call/arguments across newlines, with no combat change.
Raw logs stay outside Git. Exact-head CI/review and merge are recorded in closeout.

These checks do not constitute new native gameplay evidence. L1 remains19/19 and
11/11, formal training remains false, and Student fresh-save21/21 is post-training.

Next continue original refill-approach/control continuity, explicitly attributing
its actual clearance/pickup effects, then harvest attribution and native save/day
continuity. Do not disable old route repair or call a resource-changing move neutral.
Join those boundaries before bounded real managed integration. Global calendar,
shared budgets/cross-root/carrier continuity, Provider/model and manifest/preflight
remain later. No worker/game/server/model training was launched in this fix.
