# Machine Demand Windows

## Decision boundary

Machine type is not a permanent label. Each craft candidate receives a current demand scale from the demand stream that justifies it:

- `factory_scale_batch`: crop or other batched supply whose next arrival wave is known.
- `workshop_scale_recurring_or_bounded`: daily/short-cycle products and bounded refining backlogs such as eggs, milk, ore, or charcoal work.
- `collection_scale_one_off`: an active quest, collection requirement, or first-craft path.

The demand source is recalculated from every fresh snapshot. A Preserves Jar can move between collection, workshop, and factory scale over one save.

## Wiki and planning cross-check

The official wiki timings support using throughput rather than a permanent per-machine label. Preserves Jars take 4000 machine minutes for jelly/pickles, while a Stardew processing day contributes 1600 machine minutes; Strawberry first growth/regrowth is 8/4 days and Cranberry is 7/5 days. Short workshop cycles differ sharply: Cheese Press is 200 minutes, Charcoal Kiln is 30 minutes, and Fish Smoker is 50 minutes. The implementation does not hardcode these examples: it reads the current native machine rule and live crop phase, but the examples validate the scale separation.

The Stardew forum's artisanal-goods planning method independently reaches the same capacity shape: divide the crop quantity by the number of machine cycles available before the next harvest. It also warns that a year-wide maximum count can overbuild machines that sit idle in other seasons. This is why the candidate uses a current horizon and a latest useful construction window instead of maintaining one permanent maximum fleet target.

## Latest useful build window

For each learned machine recipe, the transparent bridge exposes every current player-inventory input accepted by a detached native machine probe, the native output for every accepting location, and the effective processing time. Core then computes:

1. `backlog_input_units`: current accepted input stacks, not inventory-slot count.
2. `minutes_until_next_arrival`: time until the earliest matching live crop harvest, using Stardew's 1600 processing minutes per full day.
3. `capacity_before_next_arrival`: completed cycles available from the existing fleet after subtracting each machine's current busy time.
4. `capacity_deficit_units`: backlog minus existing capacity, clamped at zero.
5. `required_additional_machine_count`: minimum new machines that can absorb the deficit within the window.
6. `latest_build_lead_minutes`: processing time needed by the most-loaded new machine.

Expansion opens only when the current time is inside `latest_build_lead_minutes + 60`. Before that point the candidate is excluded upstream as `deferred_until_latest_build_window`; building then would create avoidable idle time. Active task and collection requirements remain higher priority than production expansion.

Live crop arrival is exact only under the recorded condition that the crop remains in season and receives every required daily growth update. The bridge derives the next harvest from native `phaseDays`, `currentPhase`, `dayOfCurrentPhase`, and `fullyGrown` state. It does not substitute Wiki constants for runtime facts.

## Task outputs

Task matching applies to both the machine item and its possible products. Learned-machine capability snapshots contain the complete native output-rule catalog without rule/output truncation. The current Raccoon request is read from `Raccoon.GetBundle()` only after the native interaction has materialized its season; cooldown and completed ingredient bits remain explicit. This allows a Fish Smoker to become a collection-scale task candidate even when no fish is currently in the player's inventory.

## Deliberate fail-closed boundary

Current live crops are not the same as a committed future planting plan. Owned Strawberry Seeds in winter prove only inventory, not that the policy will plant a particular count on Spring 1. Therefore the machine horizon does not invent year-two arrivals from seed inventory.

Before full policy training, add a versioned long-horizon commitment ledger that records crop, tile count, planting date, expected first harvest, recurring harvest waves, cancellation/revision history, and source strategy decision. Machine demand must consume that ledger beside live crop waves. Until then, `committed_future_planting_queue` is a training-blocking strategic field gap for cross-season prebuilding, while current-backlog/live-crop timing is covered.

## Evidence

- Local decompile: `Crop.newDay`, `Crop.harvest`, `Object.OutputMachine`, `MachineDataUtility.GetOutputItem`, and `Raccoon.GetBundle`.
- Wiki cross-check only: `https://wiki.stardewvalley.net/Preserves_Jar`, `Strawberry`, `Cranberries`, `Cheese_Press`, `Charcoal_Kiln`, `Furnace`, `Fish_Smoker`, and `Smoked_Fish`.
- Tutorial cross-check only: `https://forums.stardewvalley.net/threads/artisanal-goods-planning.1729/`.
- Offline tests cover task > production > collection priority, factory build deferral, latest-window opening, existing-fleet suppression, and a native Raccoon Smoked Fish requirement.
- No game process or runtime smoke was started for this slice.
