# Material Inventory Graph

## Scope

`farm.material_inventory_graph` is the canonical current-state material source for downstream planning. It separates physical inventory nodes from access points, so two Junimo Chest access objects or one Workbench connected to several chests never duplicate item quantity.

Included nodes:

- current player inventory;
- player storage chests across loaded persistent locations and farm-building interiors;
- the current player's unlocked built-in fridge and placed mini-fridges;
- global chest inventories, deduplicated by explicit `Chest.GlobalInventoryId` or the native `JunimoChest -> FarmerTeam.GlobalInventoryId_JunimoChest` branch;
- Auto-Grabber internal `heldObject` chests;
- machine buffers, separated into `ready_output` and `in_process` supply states.

Workbench rows are edges over existing chest nodes. They follow the decompiled `Workbench.checkForAction` eight-neighbor rule and only connect `None` or `BigChest` special types. They do not copy slot rows.

## Native Sources

- `Utility.ForEachLocation(includeInteriors:true, includeGenerated:false)` supplies persistent locations.
- `Chest.GetItemsForPlayer(playerId)` supplies the player-specific or global inventory actually used by native chest actions; `Chest.Items` is not used for placed-chest quantity. Its separate `JunimoChest` branch resolves `FarmerTeam.GlobalInventoryId_JunimoChest` even when the explicit property is empty.
- `GameLocation.GetFridge(true)` and `GetFridgePosition()` supply the unlocked map fridge.
- `(BC)165` `Object.heldObject as Chest` supplies Auto-Grabber contents.
- machine `heldObject`, `readyForHarvest`, and machine data separate collectable output from in-process material.

## Storage Infrastructure View

`farm.chests` and `current_location.chests` use `storage_infrastructure.v1`. They are reference views over the canonical graph, not independent item lists. Every row retains its stable `access_point_id` and `node_id`, persistent-location topology, native capacity, occupied/free slots, owner/global identity, actor authorization, mutex state, chest flags, color, and the native empty-remove versus nonempty-shove relocation status. Contents are resolved only through `farm.material_inventory_graph.inventory_nodes[node_id]`.

The full-farm view includes placed player chests and owned built-in fridges across persistent loaded locations. The current-location view filters the same cached graph. It is collected only by snapshot profiles that already request the persistent graph (`daily`, `training_machine`, `fishing`, and `full`); other profiles return `not_requested_by_snapshot_profile` instead of performing a hidden world scan or reporting a false empty list.

`StorageInfrastructureCapacityProjection` validates the source graph schema and player, canonical-reference policy, summary counts, access/node uniqueness, exact capacity and slot counts, global identity, allowed storage kinds, slot identity, ownership, lock state, and supply state. Any mismatch blocks the capacity result rather than exposing partially trusted free space.

## Reservation Contract

`strategy_commitment_ledger.v1` persists `material_reservations[]` per save and player. Every reservation is bound to the source snapshot hash, controller decision, goal, actor, exact `node_id`, `slot_index`, `qualified_item_id`, and quantity. Upsert and cancellation use optimistic ledger revisions and append immutable audit history. A request is rejected unless the live graph still contains exactly one matching, immediately available, actor-authorized slot with enough unreserved quantity.

`MaterialSupplyProjection` consumes only active reservations. It counts only `available` nodes as immediately spendable, subtracts reservations per slot, and blocks stale identity, unknown slot, duplicate node/slot identity, non-positive quantity, integer overflow, or over-reservation. Cancelled reservations remain auditable but release supply. Ready machine output and in-process material remain visible but are not silently treated as current crafting stock.

Machine-crafting candidates compare their exact personal or native Workbench consumption plan with projected unreserved slots before ranking. The daily plan preserves the material-ledger ID, revision, guard status, and relevant reservation IDs; `ActionQueueCompiler` recomputes all of them against the latest supplied ledger and blocks stale queues.

Immediately before `LiveTrainingLoop` sends a machine-crafting request to the runtime executor, it calls the backend dispatch-readiness endpoint with the current ingested state hash, queue ID, and queue-item ID. The backend resolves the current save/player ledger and independently verifies both commitment-ledger bindings, material-ledger bindings, guard status, and active reservation IDs. A rejected dispatch sends no game input and writes no training row; the daily planner recompiles from the same current game snapshot plus the new controller ledger, with a bounded retry count.

## Multiplayer Non-Interference

The graph reads every persistent player chest instead of hiding storage owned by another farmer. Each node carries `ownership_class` and `actor_use_authorized`. Native `Object.placementAction` writes the placing farmer's multiplayer ID to `owner`; the bridge classifies that exact field as actor-owned, shared unowned, shared team global, or other-player-owned.

The default policy is `deny_without_explicit_authorization`. Shared, unowned, and other-player nodes remain transparent and contribute to `restricted_quantity`, but they are excluded from spendable projections, native chest transfer, and Workbench sources. The runtime rechecks chest owner and global-inventory identity immediately before native input. A later versioned cooperation policy may authorize selected shared nodes; absence of that policy cannot imply consent.

## Exit Boundary

Static read, deduplication, quantity aggregation, Workbench connectivity, and exact-slot reservation projection are complete and offline-tested. Hidden and silent isolated runtime smoke `runtime-material-inventory-graph-smoke-20260719-152511` passed 17/17 checks over a native matrix containing normal and Big Chests, two Junimo Chest access points sharing one implicit global inventory, built-in and mini fridges, an Auto-Grabber buffer, Workbench adjacency, and ready/in-process machine buffers.

Native normal-chest transfer and Workbench crafting are implemented, generic current-map machine placement is runtime-verified, the default-deny multiplayer resource boundary is enforced through read, projection, compiler, and runtime recheck, and controller-owned plan-horizon material reservations persist through an execution-time freshness gate. Storage identity/capacity/lock/relocation transparency and its fail-closed capacity boundary are implemented statically. The remaining storage boundary is route-safe layout candidates, chest acquisition and native placement, purpose assignment, evacuation-aware relocation, explicit shared-resource authorization, and isolated runtime validation. Downstream machine construction must distinguish personal-inventory crafting, authorized Workbench-connected crafting, and material staging; it must not treat graph-wide visible quantity as directly consumable.
