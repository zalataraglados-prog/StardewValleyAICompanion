# Canonical Hay Storage Resources

PR539 accepted at main5bf13fbc/tree4b02c1fc after final-head CI42476060159:
42/42, Core2487/Backend1483, zero failures/skips. Its six actual native trace
replays and accepted L1 evidence are reused, not recollected.

## Scope And Invariants

Grass can send hay directly to silo storage without an inventory admission.
A farm-computer report or feed hopper is not a prerequisite for reading it.

- farm.hay_storage (hay_storage_inventory.v1) contains stable raw stock/capacity,
  building count and identity for native noninterior/non-generated locations plus
  actual Farm. Rows are reference-deduplicated and sorted independently of actor
  navigation. This is not a claim of global interior hay coverage.
- current_location.hay_storage_context (hay_storage_context.v1) separately holds
  the current source row, actual Farm ID and native traversal order, including
  repeated references. An interior/generated source can lie outside inventory.
- daily/full/collection_teacher request these views; other profiles do not add
  scans. No per-frame refresh, background reader or machine-cache scan is added.
- Ambiguous identities/custom or patched getters remain unavailable, not zero.
  Resolve inherited getters to the declaring method before Harmony checks;
  rejected getters are not invoked. Empty post-unpatch metadata is not a patch.
- The old persistent resource projector is unchanged. Tests prove navigation-only
  context changes are neutral, while real farm stock changes remain non-neutral.

## Native Rule Authority

Actual E: game1.6.15 DLL SHA256:
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee.
Fresh GameLocation/Utility/Grass decompilation stays in the external evidence root.

GetHayCapacity sums positive building capacities after construction.
StoreHayInAnySilo always tries current, even for zero; then Farm when remainder>0
and actual source Name!=Farm; then Utility.ForEachLocation(includeInteriors:false)
in original order, only for rows with buildings, stopping at remainder<=0.
Repeated references share stock, not additional capacity. tryToAddHay uses
Math.Min(capacity-stock,offer) with unchecked integers: overcapacity can decrease
stock and increase remainder. Preserve that behavior, not a free-capacity clamp.
The pure projector validates both scopes and source echoes, but does not predict
grass RNG, prove offer origin, dispatch, or authorize native/resource/training credit.

## Verification And Required Successor

Core34/34 and related Backend355/355 passed, zero failures/skips. Native B checked
87 locations/24 reads, raw counts/order and scoped player/map/RNG unchanged;
seven arithmetic/capacity cases, cross-location spill and patch rejection/restoration
passed. Its tuple retention mistakenly emitted an empty object; that record stays
preserved, not claimed as a full snapshot. Native C repeated only read/retention,
retaining both complete schemas and87 rows, not the seven-case matrix. Source save
hash was unchanged and owned game processes exited. B/C timings19.95/11.34ms for
24 reads are calibration timings, not FPS or global purity proof. Product native
builds passed; the external test-only detached-name setup retains one deliberate
AvoidNetField warning. Earlier failures stay external; no raw/local-data is committed.

Final-head CI/review is recorded at publication. Next join the same original refill
trace's exact clear/pickup resources and producer provenance, including tool effects
and capture-to-snapshot gap, to the existing single persistent owner. Then harvest,
native save/day and bounded managed real integration; original global calendar,
budgets/cross-root, remaining probes, Provider/model and manifest follow. L1 stays
19/19 and11/11, formal=false; Student fresh-save21/21 is post-training.
