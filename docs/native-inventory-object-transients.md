# Native Inventory Object Transients

This extends the existing opt-in inventory transient observer, not an executor,
second graph walker, planner or resource-credit path. Native authority is the same
E: vanilla 1.6.15 assembly, SHA256
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.

## Raw Subtype Contract

For the catalog's Object identity family (exact native `StardewValley.Object` and
`StardewValley.Objects.ColoredObject`), retain four raw fields in the optional
`native_item_transients.v1.object` object:

| Wire Member | Native Declaration | Meaning |
| --- | --- | --- |
| `has_held_object` | Object._hasHeldObject, bool | Event-maintained cache affecting readyForHarvest |
| `has_light_source` | Object._hasLightSource, bool | Event-maintained cache controlling world-light insertion |
| `shake_timer` | Object.shakeTimer, int | Raw signed counter; expiry may reset health |
| `health` | Object.health, int | Raw signed health; native getHealth/setHealth are methods, not serialized properties |

Fresh native source confirms `updateWhenCurrentLocation`, the two visible-change
handlers, `getHealth`/`setHealth`, location entry and `DayUpdate`. Do not recompute
cache flags from pointers, normalize negative counters or demand cache coherence
without native timing proof. Missing private layout fails, never supplies false/0.
Direct reads compose in the original helper before virtual headers and XML.

`destroyOvernight`, `displayNameFormat`, ColoredObject colors/aliases and enchantment
Level already have persisted wrappers. They are not new missing transients merely
because their backing fields are ignored by XML. Actual alias emission needs its
own evidence; no serializer or constructor is invoked to read these four fields.

## Consumption And Legacy Evidence

The original `ReadInventoryTransients` remains the ONLY parser. Its optional
`requireObjectState: true` requires all four fields on every applicable slot at
BOTH endpoints. Present but partial state, Object state on a non-Object type, or
transients on an empty slot fail even in legacy mode. Missing entire Object state
is accepted only for the original common-root proof when not explicitly required.

`ObjectSubtypeStateVerified` records the requested strict scope;
`ObservedObjectOccurrences` separately counts actual Object slot occurrences across
both endpoints. Zero applicable slots is not an actual Object calibration sample.
Legacy receipts do not gain new subtype credit. Returned values are immutable,
not aliases to the mutable wire objects. All full-state, native-outcome, resource
and formal-training authorization flags remain false.

## Verification And Remaining Work

Implementation/verification status is recorded in CURRENT_WORK_CN.md and
CURRENT_STATUS_CN.md; source preparation is not live-game proof. Native-CLR
fixtures, consumer negatives and any live probe must be identified separately.

Controller/retained API peer implementation is integrated from peer `3a4ca38e`
as `f3dc5840`, two owned helper files only. Constructor-bypassed pinned native CLR
fixtures passed84 focused checks (Object/ColoredObject/non-Object applicability,
false/0, signed extrema, detached values, raw-cache inconsistency retention,
private layout failures and legacy UTF16/container guards); no game startup there.
Local consumer suite266/Core26 passed, zero failures/skips. Four normal E: Release
components and replay/probe builds completed with zero warnings/errors. An initial
nullable assertion warning was corrected; unchanged old matrices were not rerun.

Hidden/silent isolated `native-object-transients-a` completed original tree/move
actions. Existing helper-only probe passed28/28 reads, including4 ordinary Object
return-value comparisons against independent raw-field reads; RNG fingerprints,
bounded measured state and raw references/scalars did not change within helpers.
These actual Object values were false,false,0,10; other values/ColoredObject remain
native-CLR fixture coverage, not live-game claims. The move scope recorded16 native
collection callbacks, but that alone is not full producer attribution.

Both actual records passed the original source reader, quantity continuity,
tool-input/persisted/common transient readers, NEW `requireObjectState: true` and
current one-cache-read anchor client (2/2). The replay explicitly checked the new
strict feature markers and positive Object occurrence counts. Source save hash
remained `68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`;
owned game34172/backend2312 exited. No live runtime remains or model training ran.
Exact-head remote CI/review/publication status is recorded in current-work/status.

This four-field ROOT subset is not complete Object state, recursive child state,
enchantment `_applied`/Farmer membership, world/light effects, producer provenance,
before-capture/passive intervals or final owner admission. Next nested work must
reuse the original GraphCheck, WeakNativeReferenceIds, character/scope budgets and
this same per-item helper. No independent traversal or premature training credit.
