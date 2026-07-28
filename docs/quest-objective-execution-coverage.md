# Quest objective execution coverage

This document records the executable quest boundary added after the authoritative
1.6.15 dictionary build. It is an implementation coverage record, not a claim
that every quest objective is executable.

## Authority

The implementation was checked against the matching Linux-server 1.6.15 decompile:

- `NPC.tryToReceiveActiveObject` probes special-order delivery callbacks first, then
  calls `Quest.OnItemOfferedToNpc` through `Farmer.NotifyQuests(..., onlyOneQuest: true)`.
- ordinary report interactions call `Quest.OnNpcSocialized` through the native NPC
  action path;
- `ItemDeliveryQuest.OnItemOfferedToNpc` performs native item reduction, dialogue,
  friendship, reward, and quest completion;
- `DeliverObjective` registers its handler on `SpecialOrder.onItemDelivered`;
- `ReachMineFloorObjective` subtracts 120 from Skull Cavern levels before updating
  objective progress;
- `DonateObjective` uses the native drop-box `QuestContainerMenu` lifecycle. Direct
  mutation of `donatedItems` is therefore prohibited.
- `LostItemQuest.OnWarped` creates the exact quest item at its declared map/tile as an
  `IsSpawnedObject` overlay object, and `OnItemReceived` advances `itemFound`.

The runtime terminal calls `GameLocation.checkAction`; it does not call quest
completion methods with `probe:false` and does not write quest counters directly.

## Executable binding

`quest.advance` now converts supported live objectives into existing candidate kinds:

| Objective | Bound candidate or terminal |
| --- | --- |
| ordinary fishing quest catch | existing `catch_fish` candidate filtered by exact item identity |
| ordinary location quest | existing `route_connector_tile`, one connector per fresh snapshot |
| ordinary item delivery | existing NPC route plus `quest_npc_interaction` terminal |
| ordinary slay/fishing/resource report | existing NPC route plus native report terminal |
| ordinary socialize quest | next exact `who_to_greet` NPC plus native report terminal |
| ordinary lost-item pickup | exact declared map/tile/item identity plus existing native `collect_spawned_object` candidate |
| ordinary lost/secret-lost item return | existing NPC route plus native report terminal |
| special-order `DeliverObjective` | context-tag-matched inventory item plus native NPC delivery |
| special-order `DonateObjective` | exact `DropBox <box_id>` map Action, adjacent stand tile, and native `QuestContainerMenu` insertion/confirmation |
| special-order `FishObjective` | existing fishing attempt whose projected native item context tags match the objective tag grammar |
| special-order `ShipObjective` | existing one-item native shipping candidate filtered by native tag-set grammar |
| special-order `ReachMineFloorObjective` | existing rolling perfect-mining candidate with exact ordinary/Skull level conversion |

Every bound candidate carries the quest candidate ID, family, runtime type, selected
objective index, and expected current/target counts. The action compiler rebinds those
values to the fresh snapshot. The NPC runtime then:

1. resolves the same quest or special order;
2. verifies NPC, tile, inventory slot, item identity, and progress counters;
3. runs the native `probe:true` callback;
4. verifies that the expected quest is the first receiver in native delivery order;
5. calls native `GameLocation.checkAction`;
6. accepts feedback only when that same objective changes count, completes, or is
   removed.

Cross-map NPC routes retain an exact quest continuation so replanning cannot silently
switch to an ordinary social talk or another quest.

The lost-item pickup binding routes to the declared location and, after a fresh snapshot,
accepts only an `IsSpawnedObject` at the exact quest tile with the exact qualified item
identity. It uses the existing native pickup executor; it does not inject the item or set
`itemFound`.

Drop-box candidates use the resolved native drop-box location and do not treat
`dropBoxTileLocation` as the interaction tile. That field only positions the quest
indicator. The actual interaction target is indexed from the current map's exact
`Action = "DropBox <box_id>"` property. Runtime execution calls
`GameLocation.checkAction`, waits for the order's native mutex and
`QuestContainerMenu`, clicks the projected inventory slot, closes through the menu's
OK button, and verifies inventory, objective count, confirmation, and order state.

## Explicit remaining blockers

The generated `quest-action-coverage-matrix.json` is the omission check for this surface.
It scans native decompiled subclasses and reports 12 ordinary quest runtime types and 9
special-order objective types, with no uncatalogued type. Its 28 stage rows currently
contain 15 bound, 11 blocked, and 2 native observation-only stages.

The following objective bindings remain fail-closed:

- ordinary craft, collect, slay, harvest, construction, secret-item acquisition, accept,
  and type-11 weeding stages;
- special-order collect, gift, Junimo Kart score, and slay
  objectives;
- native color-tag matching for preserved `ColoredObject` inputs. The game checks
  base context tags of the preserved parent, which is not yet projected on inventory
  rows;
- runtime calibration of the NPC and drop-box quest terminals in an isolated save.

The fallback `quest_candidate` and `special_order_candidate` kinds now mean
objective-specific binding is absent. They are not blocked by the obsolete blanket
`quest_native_executor_not_implemented` reason.

## Verification

- focused fishing/quest/coverage tests: 16 passed;
- full regression: Core 1,297 passed and Backend 95 passed;
- full solution build: zero errors and five existing warnings;
- knowledge compiler native scan: 12 ordinary types, 9 objective types, zero catalog
  differences, 28 stage rows with 15 bound, 11 blocked, and 2 observation-only;
- the full knowledge build retains two pre-existing Grandpa method identity blockers,
  unrelated to the quest type scan;
- no live game mutation test was run for this slice.
