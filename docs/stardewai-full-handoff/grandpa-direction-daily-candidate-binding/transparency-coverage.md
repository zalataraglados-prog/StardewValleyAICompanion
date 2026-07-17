# Grandpa Direction Daily Candidate Binding Coverage

Current as of 2026-07-17.

| Direction | Status | Candidate boundary |
|-----------|--------|--------------------|
| earn_money | Direct | Current legal sell or ship candidates |
| raise_friendships | Direct | Current legal talk or gift candidates |
| complete_master_angler | Direct | Current legal fishing candidates |
| complete_full_shipment | Direct | Only `economy.ship_items` / `ship_inventory_item_to_bin` candidates with exact typed contribution evidence |
| obtain_skull_key | Direct | Only `mining.obtain_skull_key` in ordinary mines 1-120, ending in the native floor-120 reward chest and transparent `player.has_skull_key=true` |
| raise_skill_levels | Blocked | Unified five-skill level/experience state and skill-gain candidate layer are missing |
| complete_museum_collection | Blocked | Museum completion candidate layer is missing |
| obtain_rusty_key | Blocked | Museum donation/key-acquisition candidate layer is missing |
| complete_community_center | Blocked | Bundle action chain and route commitment are unresolved |
| complete_joja_development | Blocked | Joja action chain and route commitment are unresolved |
| marriage_and_house_upgrade | Blocked | Marriage/house prerequisite candidate chain is missing |
| earn_pet_love | Blocked | Pet interaction/friendship candidate chain is missing |

Overall coverage is 5 direct directions and 7 fail-closed planned gaps.

`complete_full_shipment` is not admitted by option name alone. The binder requires all of the following on the ranked candidate: `CanShip == true`, `FullShipmentKnown == true`, `FullShipmentEligible == true`, `FullShipmentCurrentShippedCount == 0`, `FullShipmentAlreadyShipped == false`, and `FullShipmentContributes == true`. Contradictory or unknown evidence blocks the candidate.

The full-shipment transparent state is supplied by `world_progress.shipping_collection` and `world_progress.full_shipment_progress`. The downstream candidate, daily-plan compiler, action-queue compiler, native input executor, immediate receipt, and delayed `basicShipped` settlement recorder already exist.

The Skull Key chain reads `MineShaft.overlayObjects` and requires a live chest containing `SpecialItem.which == 4`. The compiler emits one rolling floor step at a time, the runtime performs the native open-animation/claim sequence, and completion requires an observed `player.has_skull_key` transition. Ordinary mines, Skull Cavern, Quarry Mine `77377`, and Volcano Dungeon remain separate families.

Blocked catalog fields describe implementation gaps. The binder does not infer that a missing capability exists from an arbitrary snapshot path.
