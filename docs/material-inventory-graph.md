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

## Reservation Contract

`MaterialSupplyProjection` accepts reservations bound to exact `node_id`, `slot_index`, and `qualified_item_id`. It counts only `available` nodes as immediately spendable, subtracts reservations per slot, and blocks stale identity, unknown slot, duplicate node/slot identity, non-positive quantity, integer overflow, or over-reservation. Ready machine output and in-process material remain visible but are not silently treated as current crafting stock.

## Exit Boundary

Static read, deduplication, quantity aggregation, Workbench connectivity, and exact-slot reservation projection are complete and offline-tested. Hidden and silent isolated runtime smoke `runtime-material-inventory-graph-smoke-20260719-152511` passed 17/17 checks over a native matrix containing normal and Big Chests, two Junimo Chest access points sharing one implicit global inventory, built-in and mini fridges, an Auto-Grabber buffer, Workbench adjacency, and ready/in-process machine buffers.

This does not complete native chest transfer, Workbench crafting, storage placement/relocation, machine placement, multiplayer ownership policy, or long-term service execution. Downstream machine construction must distinguish personal-inventory crafting, Workbench-connected crafting, and material staging; it must not treat graph-wide available quantity as directly consumable by the personal crafting menu.
