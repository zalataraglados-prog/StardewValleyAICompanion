# Skill Experience Source Audit

Scope: vanilla runtime `gainExperience` call sites from the local Stardew Valley decompile. This is an implementation inventory, not a claim that every source is already a candidate or runtime-verified.

## Covered Candidate Families

| native source | skills | current coverage |
|---|---|---|
| `FishingRod.pullFishFromWater` and fishing treasure completion | Fishing, conditional Luck | transparent bounded candidate evidence and observed runtime deltas |
| `GameLocation.breakStone` and `MineShaft.checkStoneForItems` | Mining, conditional Luck | transparent current-stone bounds in rolling mining candidates |
| `GameLocation.monsterDrop` | Combat | exact current native-monster candidate evidence |
| `Crop.harvest` | Farming or Foraging | exact current crop candidate evidence; unsupported hoe-harvest ginger remains excluded |
| `GiantCrop.performToolAction` | Luck | exact current complete-giant-crop evidence |
| `Tree.performToolAction` / `performTreeFall` | Foraging | exact current tree/moss evidence |
| ordinary stump and hollow-log `ResourceClump.performToolAction` | Foraging | exact farm resource-clump evidence |
| spawned-object `GameLocation.checkAction` / `OnHarvestedForage` | Farming and/or Foraging | exact current pickup evidence |
| twig and artifact/seed-spot `Object.performToolAction` | Foraging | exact current typed-clear evidence; eligible unseen-secret-note artifact outcomes remain fail-closed |
| `CrabPot.checkForAction` | Fishing | exact ready output, deterministic `Book_Crabbing` doubling, inventory gate, `+5` XP, bait/reset state, and native interaction evidence; the ambient RNG catch-size argument is recorded at execution |
| `MilkPail.DoFunction`, `Shears.DoFunction`, and compatible `FarmAnimal` interaction | Farming | all outdoor and animal-house animals expose live eligibility, output quality/unit state, Animal Cracker quantity, tool slot, inventory gate, stats, `-4` energy, `+5` friendship, and `+5` Farming XP; candidate, compiler rebind, moving-target native executor, and both isolated tool smokes are verified |
| `Pan.DoFunction` / `Pan.getPanItems` | Mining and Foraging | active ore point, exact Pan upgrade/enchantments, luck/DaysPlayed/TimesPanned inputs, native deterministic reward multiset, aggregate inventory acceptance, direct ore/diamond receipt stats, Mining XP, Foraging XP, and post-use point status are projected; generic `Farmer.OnItemReceived` quest/special-order/collection callbacks and upgraded-Pan respawn use explicit runtime-observed status; compiler rebind and native copper/steel smokes are verified |
| `FishPond.doAction` output harvest and `ResolveNeeds` request completion | Fishing | every exact vanilla pond exposes output/request branch priority, edge interaction geometry, output unit-state/inventory acceptance, toolbar-bound request items, population-gate before/after state, and exact Fishing XP (`10 + floor(output price * 0.04)` or `20 + SpawnTime * 5`); compiler rebind, native `GameLocation.checkAction`, inventory/pond/XP verification, and isolated output/request smokes are verified; generic output receipt callbacks remain explicitly runtime-observed |

## Missing Mechanical Candidate Families

These are required before native skill-source enumeration can be called complete:

| native source | skills | required slice |
|---|---|---|
| `HoeDirt.performToolAction` ginger branch | Foraging | hoe-harvest candidate distinct from ordinary player crop harvest |
| `Bush.shake` berry harvest | Foraging | harvestable bush state, season/day/quantity, native interaction executor |
| mine treasure `Chest` collection | Luck | exact eligible chest identity, current mine level, native chest lifecycle, output and XP verification |
| Green Rain bush-like `ResourceClump` destruction | Foraging | runtime type/state, deterministic drops, tool budget, dedicated clearance contract |
| skill books, repeated power books, and Purple Book | one or multiple skills | inventory/consumption policy, exact context-tag branch, native read animation/result executor |
| machine `ExperienceGainOnHarvest` | data-selected skill(s) | expose parsed machine data XP pairs and bind them to the existing native output-collection candidate |

## Non-Policy Sources

- `DebugCommands` XP calls are fixture/debug-only and must never become policy candidates.
- `Multiplayer.globalChatInfoMessage` XP forwarding is transport for an already selected source, not a separate action family.
- `Farmer.gainExperience` is the sink, not a source.

## Exit Conditions

1. Every non-debug vanilla call site above maps to a covered candidate family or an explicit unavailable reason derived from current state.
2. Every enabled candidate carries structured skill id, minimum, maximum, condition, and projection status fields; multi-skill actions carry one row per skill.
3. The action compiler rebinds those fields from a fresh snapshot.
4. Runtime records observed before/after XP for every projected skill and keeps executor failures out of strategy reward.
5. Focused and full regressions plus isolated native smokes pass before `family_coverage_partial` can become complete.
