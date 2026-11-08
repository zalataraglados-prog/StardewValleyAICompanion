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

This four-field ROOT subset is not complete Object state, recursive child state,
enchantment `_applied`/Farmer membership, world/light effects, producer provenance,
before-capture/passive intervals or final owner admission. Next nested work must
reuse the original GraphCheck, WeakNativeReferenceIds, character/scope budgets and
this same per-item helper. No independent traversal or premature training credit.
