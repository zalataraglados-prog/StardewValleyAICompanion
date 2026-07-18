# Grandpa Direction Daily Candidate Binding Coverage

Current as of 2026-07-18.

| Direction | Status | Candidate boundary |
|-----------|--------|--------------------|
| earn_money | Direct | Current legal sell or ship candidates |
| raise_friendships | Direct | Current legal talk or gift candidates |
| complete_master_angler | Direct | Current legal fishing candidates |
| complete_full_shipment | Direct | Only `economy.ship_items` / `ship_inventory_item_to_bin` candidates with exact typed contribution evidence |
| obtain_skull_key | Direct | Only `mining.obtain_skull_key` in ordinary mines 1-120, ending in the native floor-120 reward chest and transparent `player.has_skull_key=true` |
| raise_skill_levels | Direct, runtime pending | Current exact positive-XP candidates only; all local-decompile non-debug `gainExperience` source families are represented, with Luck/nonpositive sink calls kept at zero |
| complete_museum_collection | Direct, runtime pending | Exact `donate_museum_item` candidates with `before + 1 == after <= LibraryMuseum.totalArtifacts`; final donation projects native achievement 5 settlement |
| obtain_rusty_key | Direct, runtime pending | Exact donation progress while below the loaded `museum60` threshold; threshold crossing carries `MarkEventSeen Host 295672`, followed by native Farm event 66 key acquisition |
| complete_community_center | Blocked | Bundle action chain and route commitment are unresolved |
| complete_joja_development | Blocked | Joja action chain and route commitment are unresolved |
| marriage_and_house_upgrade | Blocked | Marriage/house prerequisite candidate chain is missing |
| earn_pet_love | Direct, runtime pending | Exact positive-progress `pet_daily_interaction` or `fill_pet_bowl`; the latter records delayed `Pet.dayUpdate` settlement rather than immediate friendship |

Overall coverage is 9 direct directions and 3 fail-closed planned gaps. Static additions that have not yet been runtime-tested remain marked runtime pending rather than being counted as runtime-complete.

`complete_full_shipment` is not admitted by option name alone. The binder requires all of the following on the ranked candidate: `CanShip == true`, `FullShipmentKnown == true`, `FullShipmentEligible == true`, `FullShipmentCurrentShippedCount == 0`, `FullShipmentAlreadyShipped == false`, and `FullShipmentContributes == true`. Contradictory or unknown evidence blocks the candidate.

The full-shipment transparent state is supplied by `world_progress.shipping_collection` and `world_progress.full_shipment_progress`. The downstream candidate, daily-plan compiler, action-queue compiler, native input executor, immediate receipt, and delayed `basicShipped` settlement recorder already exist.

The Skull Key chain reads `MineShaft.overlayObjects` and requires a live chest containing `SpecialItem.which == 4`. The compiler emits one rolling floor step at a time, the runtime performs the native open-animation/claim sequence, and completion requires an observed `player.has_skull_key` transition. Ordinary mines, Skull Cavern, Quarry Mine `77377`, and Volcano Dungeon remain separate families.

The skill-level chain reads all six permanent/effective skill rows but admits only candidates with complete source-specific experience evidence and at least one positive effective delta. Crop, forage, fishing, mining/combat, machines, animal products, crab pots, fish ponds, panning, Green Rain clumps, bushes, ginger, and inventory books reuse their existing typed action compilers and native executors. Multi-skill actions remain structured; book and machine call order preserves Mastery thresholds. Vanilla Luck calls are explicit zero sinks because `Farmer.gainExperience` returns immediately for skill index 5.

The pet-love chain follows the decompiled `Pet.checkAction`, `Pet.dayUpdate`, `Pet.GrantLoveMailIfNecessary`, and `PetBowl.performToolAction` branches. The daily interaction projects `+12`, `lastPetDay`, `timesPet`, grant state, `petLoveMessage`, `MarniePetAdoption`, and the deterministic gift-trigger roll. The gift item itself remains `runtime_observed_global_rng_selection`. Bowl filling verifies only `watered=false -> true`; projected `+6`, `petLoveMessage`, and `MarniePetAdoption` remain pending until the next native day update. Rain's new-day bowl fill is exposed separately.

The museum chain reads the live shared `museumPieces`, dynamic total, current free display tile, Gunther action tile, private mutex state, exact donatable inventory rows, achievement 5, `museum60` reward state, and events 295672/66. The compiler rechecks every typed projection against the same snapshot. Runtime walks to the counter and drives the native donation menu; menu close invokes `OnDonationMenuClosed`, which releases the mutex and calls `getRewardsForPlayer`. `museum60` marks event 295672; Farm event 66 later runs the native `rustyKey` command. Donation progress must not be mistaken for immediate possession of the key.

Blocked catalog fields describe implementation gaps. The binder does not infer that a missing capability exists from an arbitrary snapshot path.
