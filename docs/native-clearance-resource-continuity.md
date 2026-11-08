# Native Clearance Resource Continuity

## Parent And Scope

PR540 accepted at mainf95fd37f, tested5fc02ba6/tree1054cbf5, after CI42481457238
completed42/42: Core2521/Backend1489, zero failures/skips. Controller source review
and native hay calibrations were performed; no automated review submission had
arrived at merge time. Owned hay branch refs were removed only after equal-tree
verification. Accepted L1 and native storage matrices are not recollected.

This increment supplies two parts of the same original refill resource boundary,
not another executor or an authorization to train.

## Per-Call Native Stamina

NativeClearanceToolCostProjector consumes actual immutable call inputs: raw
argument power, Farmer.toolPower, effective skill/MaxStamina, Efficient, raw float
stamina and statue_of_blessings_2 protection. Axe uses argument power; Pickaxe
overwrites power with Farmer.toolPower. Preserve unchecked int operations and
single-precision rounding per call, including negative debit. Efficient skips
the expression AND setter; zero debit still calls the setter. Farmer protection
rejects decreases before the native [-16,MaxStamina] clamp. Do not substitute
double planner estimates, infer swing count, or fill absent inputs with defaults.

Source: actual pinned1.6.15 Axe/Pickaxe and Farmer.Stamina decompilation from the
existing external evidence root. The API peer implemented and tested only the
two allowlisted source/test files (original e89214df, integrated38fd23b8).
Focused31/31 passed; after governance integration, cost+hay Core65/65 passed.
These are synthetic arithmetic fixtures, not actual tool-invocation evidence.
Base Tool callbacks, animation and whole-action side effects are outside this
projection; native/resource/formal authorization flags remain false.

## Inventory Quantities Across Capture Gaps

NativeOutputObservationSnapshotBoundary reuses the original observation reader,
snapshot hash/identity/value readers, exact day/location and tick interval. It
compares all qualified-ID/raw-quality aggregate quantities at both snapshot ends
to the complete captured inventory denominator. Validate raw occupied/empty slot
shape, unique contiguous indices, row limits and overflow; never repair order,
guess unavailable values or authorize slot-metadata/producer provenance from counts.
The existing standalone observation reader and shared executor gates are unchanged.

The first combined observation regression passed90/90. Offline reuse of the same
retained actual native sources admitted3 of4 observed steps, rejecting the tree
pickup move. Detailed replay found snapshot-before/capture-before wood5/5,
capture-after16 at tick2295, snapshot-after17 at2297. Thus one real pickup occurred
after capture; the old source reader still passes, but this stronger quantity
boundary correctly refuses it. Original reports remain unchanged. Diagnostic replay
reopened only that failed step, not the three accepted cases or a new game run.

## Required Successor

The API peer integrated OpenWithSnapshotInventoryContinuity into the ORIGINAL
trace in29a73ac9: all frames use the stronger reader and retain original input/CAS/
gzip/queue/session checks. The old Open remains unchanged calibration scope and
SnapshotInventoryContinuityVerified=false; only a fully admitted stronger trace
sets it true. Both retain false native/resource/formal flags. The focused combined
acceptance passed110/110 (20 new original-compiler/queue tests and90 observations).
Two fixture-only failures were corrected without weakening production guards:
missing wood mirror in the material graph, then a one-frame no-repair assumption.
Earlier failures are retained in external peer notes.

Next close native observation lifecycle
gaps with actual callbacks/atomic snapshot boundaries, not an arbitrary count
tolerance, sleep, fabricated admission or weakened old invariant. Actual call
inputs, full slot metadata, world mutations and producer origin remain required
before the exact resource boundary can join the existing single persistent owner.
Harvest/native save/day, bounded managed integration and original global/Provider/
model/manifest work follow. L1 stays19/19 and11/11, formal=false; Student21/21 is
post-training. Raw evidence and local-data stay outside Git.
