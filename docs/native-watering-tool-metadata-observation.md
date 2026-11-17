# Selected Watering Tool Metadata Observation

## Current Scope (261)

This is the purpose-scoped producer/wire for the selected watering/pickup
resource consumer, not a second executor or a resource certificate. It is OFF
by default. There is no automatic LiveTrainingLoop/owner activation yet.
The pending historical operation47 has no such witness; this producer cannot
retroactively create one. The primary/pickup resource leaf is documented in
[watering maintenance](money-crop-watering-maintenance.md).

`action_origin_watering_resources` admits only `training_singleplayer` with both
option/source option `executor.water_crop`, an explicit scope, snapshot anchor,
tool inputs and the inventory payload/transient/graph/debris tiers. It cannot be
combined with selected tilling. The old Move/Clear and Till scopes remain separate;
new fields are omitted from ordinary request/receipt JSON. This is request
admission only, not proof that eventual metadata or resource effects are correct.

## Native Evidence

Inspected the actual E runtime1.6.15 DLL, SHA256
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.

- `Tool.Update` token0x060015b4: sprite arithmetic depends on native runtime
  category, qualified ID, direction, farmer motion frame, actor facing and the
  previous cursor in the non-reset branch. Do not replace it with Hoe's fixed
  completed-cycle offset or copy the observed final sprite as a prediction.
- `Tool.endUsing` token0x060015ba increments `swingTicker` once per actual call.
  `Farmer.EndUsingTool`0x0600093a uses an event queue for the local player;
  `performEndUsingTool`0x0600093b calls the native method. One request need not
  imply one call, especially integrated refill then watering or a partial block.
- `Farmer.Update` calls `Equip<Tool>` and `Equip`0x06000941 sets the new item's
  inventory-history property. DO NOT patch generic Equip: shared reference-type
  method bodies can affect unrelated equipment instantiations.
- Instead observe non-generic `Item.HasBeenInInventory` setter0x06000cd6,
  `hasbeenInInventory.Set(value)`, only for tools currently in this scope's actor
  inventory. Ignore unowned/cloned items in this property hook; receiving new
  items remains the original pickup reader's job.
- Read the existing native save XML structurally: its can has `WaterLeft`
  (capitalized), `initialParentTileIndex`, `InitialParentTileIndex`,
  `currentParentTileIndex`, `swingTicker`, and `hasBeenInInventory`. A CLR property
  name alone is not sufficient XML schema evidence. Reading the old save proves
  schema only, not the current unsaved watering outcome or water quantity.

## Passive Producer

Raw before/after frames contain tick, actor reference and every inventory Tool's
slot/reference/type/qualified ID, initial/current sprite indices, swing counter,
inventory history and nullable water for WateringCan. Observing all held tools
allows a later consumer to account for legitimate pre-water movement/selection;
it must not assume only the selected can's metadata could change.

Three NON-GENERIC hooks retain entry order, tick, physical tool and actor/location:

| Kind | Actual inputs |
| --- | --- |
| `update` | direction, farmer motion frame, actor facing/tool power |
| `end_using` | native actor/location and tool power |
| `inventory_history` | actual Boolean setter input for an owned tool |

Postfix records normal return; finalizer records failure and returns the SAME
exception reference. Prefixes never return false, alter arguments or replace a
native result. A missing/failed return makes the scope unavailable, not successful.
Observation reads native inputs/endpoints, not expected values or a cycle count.
No scope means no observation. There is no per-frame full inventory/XML recapture:
the pre-existing payload tiers capture endpoints, while calls append small rows.

Bounds remain explicit:512inventory slots/tools,512metadata calls and256-character
tool/location IDs. Overflow/capture/installation failure is diagnostic, never
silent truncation. Water-hook installation failure does not poison the original
Move/Clear observer's patch state. Installation rollback removes only this
dedicated Harmony ID. No generic methods are admitted for these hooks.

## Proof And Required Successor

Final E-referenced harness build: zero warnings/errors. Core focused/adjacent54/54
and Backend139/139 passed;29new cases total, earlier repeated runs overlap.
Coverage includes opt-in/default JSON, scope separation, missing tiers/mixed flags,
anchor admission, raw negative water/wrapped counters and nullable call completion.
An isolated callback probe loads the built library WITHOUT a game instance or
Harmony installation: normal completion, exception identity/failure marking and
unscoped no-op passed. This is NOT an actual native hook-attachment/timing test.

Next, replay these raw native inputs into independently calculated metadata and
use one slot-scoped XML adapter with the existing pickup kernel. Bind actual
raw endpoints, actor/tool identities, complete calls and original snapshot anchor;
compose with the260primary/pickup resource leaf. Then enable this profile only
through the original selected watering/maintenance/owner chain. Keep pure default
and native-refill dispatch unchanged. Consider pickup need at selection, not
always-on global capture. Do not replay days or modify old executors to fit a new
consumer. Native attachment and real timing remain unverified until that focused
runtime check; no formal training, owner47 acceptance or production credit here.
