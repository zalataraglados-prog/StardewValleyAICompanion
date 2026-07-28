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
- `SlayMonsterQuest.OnMonsterSlain` matches `Monster.Name.Contains(monsterName)`;
  quest ID 15 additionally accepts names containing `Slime`, `Jelly`, or `Sludge`.
- `SlayObjective.OnMonsterSlain` matches any configured
  `Monster.Name.Contains(targetName)` and honors `ignoreFarmMonsters`.
- `ItemHarvestQuest.OnItemReceived` accepts the exact qualified item ID or, when
  its target starts with `-`, the exact native item category. Its `Number` field
  is a remaining count and decreases by the native `numberAdded`.
- `ResourceCollectionQuest.OnItemReceived` accepts only the exact qualified item
  ID and increases `numberCollected` only when the item is actually received.
- `CollectObjective` listens to `SpecialOrder.onItemCollected` and applies the
  native comma-group/slash-alternative context-tag grammar to the received item.
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
| ordinary slay quest | rolling ordinary-mine search plus exact-name native mining combat |
| ordinary item-harvest quest | current mature `Grab` crop whose qualified item ID or category matches the native target |
| ordinary resource-collection quest | exact current spawned-object, farm-debris, or ready-machine receipt; exact clearable wood/stone or farm-bush source; or rolling current-mine resource source/debris receipt |
| special-order `CollectObjective` | current mature `Grab` crop, farm debris, or ready machine output whose native context tags match the objective |
| special-order `DeliverObjective` | context-tag-matched inventory item plus native NPC delivery |
| special-order `DonateObjective` | exact `DropBox <box_id>` map Action, adjacent stand tile, and native `QuestContainerMenu` insertion/confirmation |
| special-order `FishObjective` | existing fishing attempt whose projected native item context tags match the objective tag grammar |
| special-order `GiftObjective` | existing native social-gift candidate filtered by exact item context tags and native minimum-like-level ordering |
| special-order `ShipObjective` | existing one-item native shipping candidate filtered by native tag-set grammar |
| special-order `ReachMineFloorObjective` | existing rolling perfect-mining candidate with exact ordinary/Skull level conversion |
| special-order `SlayObjective` | rolling ordinary/Skull mine search plus exact-name native mining combat |

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

Slay candidates retain task identity while reusing the rolling mining planner. A
matching live monster is selected before general floor work. If none is present, the
planner uses the existing recovery, opening, descent, and deadline-exit primitives,
then replans from the next transparent snapshot. Vanilla order identity selects
ordinary mines for `Clint`/`Wizard2` and Skull Cavern for
`DesertFestivalMarlon1`; unknown or modded slay sources fail closed until their mine
family is authoritative. Compiler and runtime recheck the same task, counts, mine
family, live monster identity, and native name rule. Mummy knockdown is an
intermediate step; matching quest progress is required after the bomb finalizer.

Cross-map NPC routes retain an exact quest continuation so replanning cannot silently
switch to an ordinary social talk or another quest.

The lost-item pickup binding routes to the declared location and, after a fresh snapshot,
accepts only an `IsSpawnedObject` at the exact quest tile with the exact qualified item
identity. It uses the existing native pickup executor; it does not inject the item or set
`itemFound`.

The item-harvest binding is deliberately bounded to an immediately mature `Grab` crop.
The bridge projects its qualified harvest ID, native category, and context tags; the
compiler rechecks the same live crop and quest remaining count. Runtime accepts feedback
only when native harvest decreases `ItemHarvestQuest.Number` or completes the quest.
Scythe harvest remains a two-step harvest-then-debris-pickup path and is not credited on
the harvest frame.

Resource acquisition distinguishes source actions from receipt actions. Clearing an
obstacle or breaking a mine stone may create the required debris, but cannot claim task
progress. The next fresh snapshot must select the exact debris pickup; only native item
receipt may increase the ordinary quest count. Current exact spawned objects are direct
receipts. The special-order binding currently follows the same strict rule for mature
`Grab` crops and verifies the native objective count after harvest.
Ready machine outputs expose their exact native context tags, revalidate the live held
item before execution, and verify the ordinary quest or special-order count only after
native machine collection updates the inventory.
Farm debris follows the same receipt rule and carries the live item's native context
tags. A matching farm bush is only an ordinary-quest source step; native shake must
produce debris, and a fresh snapshot must bind that exact debris before progress can be
claimed.

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
contain 21 bound, 5 blocked, and 2 native observation-only stages.

The following objective bindings remain fail-closed:

- ordinary craft, construction, secret-item acquisition, accept,
  and type-11 weeding stages;
- Junimo Kart score objectives;
- acquisition families not yet attached to the bounded collect stages, including
  scythe-created crop debris, fishing trash, ginger, non-farm and special-order bush
  source planning, giant crops, monster drops, resource clumps, and modded sources;
- native color-tag matching for preserved `ColoredObject` inputs. The game checks
  base context tags of the preserved parent, which is not yet projected on inventory
  rows;
- runtime calibration of the NPC and drop-box quest terminals in an isolated save.

The fallback `quest_candidate` and `special_order_candidate` kinds now mean
objective-specific binding is absent. They are not blocked by the obsolete blanket
`quest_native_executor_not_implemented` reason.

## Verification

- focused resource/collect filter: 8 passed;
- full regression: Core 1,315 passed and Backend 95 passed;
- knowledge compiler native scan: 12 ordinary types, 9 objective types, zero catalog
  differences, 28 stage rows with 21 bound, 5 blocked, and 2 observation-only;
- the full knowledge build retains two pre-existing Grandpa method identity blockers,
  unrelated to the quest type scan;
- serial full solution Release rebuild: zero errors and seven existing warnings emitted;
- no live game mutation test was run for this slice.
