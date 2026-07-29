# Skill Experience Source Audit

Scope: vanilla runtime `gainExperience` call sites from the local Stardew Valley decompile. This is an implementation inventory, not a claim that every source is already a candidate or runtime-verified.

## Covered Candidate Families

| native source | skills | current coverage |
|---|---|---|
| `FishingRod.pullFishFromWater` and fishing treasure completion | Fishing, conditional Luck | transparent bounded candidate evidence and observed runtime deltas |
| `GameLocation.breakStone` and `MineShaft.checkStoneForItems` | Mining, conditional Luck | transparent current-stone bounds in rolling mining candidates |
| `GameLocation.monsterDrop` | Combat | exact current native-monster candidate evidence |
| `Crop.harvest` | Farming or Foraging | exact current ordinary crop candidate evidence; hoe-harvest ginger stays outside this executor |
| `Crop.hitWithHoe` / `HoeDirt.performToolAction` ginger branch | Foraging | exact current ginger identity, Hoe slot, energy, `(O)829 x1` debris, soil after-state, and `+7` XP flow through a dedicated candidate/compiler/native-tool chain; runtime validation pending |
| `Bush.performUseAction` / `Bush.shake` | Foraging for ordinary berries; none for tea/walnut branches | every exact current `largeTerrainFeatures` Bush exposes footprint, size/age/shelter/readiness/bloom/cooldown, branch, exact output, Botanist quality, `1 + ForagingLevel / 4` berry quantity and matching XP, and golden-walnut tracker key; candidate/compiler bind a reachable perimeter action tile; runtime uses native `checkAction` and verifies offset, combined debris/inventory output, XP, and nut tracker without direct mutation; runtime validation pending |
| `GiantCrop.performToolAction` | Luck | exact current complete-giant-crop evidence |
| `Tree.performToolAction` / `performTreeFall` | Foraging | exact current tree/moss evidence |
| ordinary stump and hollow-log `ResourceClump.performToolAction` | Foraging | exact farm resource-clump evidence |
| spawned-object `GameLocation.checkAction` / `OnHarvestedForage` | Farming and/or Foraging | exact current pickup evidence |
| twig and artifact/seed-spot `Object.performToolAction` | Foraging | exact current typed-clear evidence; eligible unseen-secret-note artifact outcomes remain fail-closed |
| `CrabPot.checkForAction` | Fishing | exact ready output, deterministic `Book_Crabbing` doubling, inventory gate, `+5` XP, bait/reset state, and native interaction evidence; the ambient RNG catch-size argument is recorded at execution |
| `MilkPail.DoFunction`, `Shears.DoFunction`, and compatible `FarmAnimal` interaction | Farming | all outdoor and animal-house animals expose live eligibility, output quality/unit state, Animal Cracker quantity, tool slot, inventory gate, stats, `-4` energy, `+5` friendship, and `+5` Farming XP; candidate, compiler rebind, moving-target native executor, and both isolated tool smokes are verified |
| `Pan.DoFunction` / `Pan.getPanItems` | Mining and Foraging | active ore point, exact Pan upgrade/enchantments, luck/DaysPlayed/TimesPanned inputs, native deterministic reward multiset, aggregate inventory acceptance, direct ore/diamond receipt stats, Mining XP, Foraging XP, and post-use point status are projected; generic `Farmer.OnItemReceived` quest/special-order/collection callbacks and upgraded-Pan respawn use explicit runtime-observed status; compiler rebind and native copper/steel smokes are verified |
| `FishPond.doAction` output harvest and `ResolveNeeds` request completion | Fishing | every exact vanilla pond exposes output/request branch priority, edge interaction geometry, output unit-state/inventory acceptance, toolbar-bound request items, population-gate before/after state, and exact Fishing XP (`10 + floor(output price * 0.04)` or `20 + SpawnTime * 5`); compiler rebind, native `GameLocation.checkAction`, inventory/pond/XP verification, and isolated output/request smokes are verified; generic output receipt callbacks remain explicitly runtime-observed |
| mine treasure `Chest` collection | Luck call site, effective XP zero | every loaded MineShaft reward chest exposes exact chest/reward identity and native lifecycle; candidate/compiler/runtime chain is implemented. Decompiled `gainExperience(5, 25 + mineLevel)` is ignored by `Farmer.gainExperience` for skill 5, so the exact observed Luck XP delta is zero rather than a trainable skill gain; runtime validation pending |
| Green Rain bush-like `ResourceClump` destruction | Foraging | every exact loaded vanilla index-44/46 clump exposes footprint, Axe slot/damage/hits, exact `+15` XP, day/save/anchor-seeded Moss/Fiber/Mossy Seed outputs, and bounded global-RNG secret-note probability without consuming RNG; candidate/compiler/native executor rebind and verify deterministic effects; hidden isolated ordinary and special-order task-attached runtime validation passed |
| machine `ExperienceGainOnHarvest` | data-selected skill(s) | every loaded machine row exposes the raw token string, every parsed/skipped pair, aggregate effective deltas for all vanilla skill indexes, Luck/nonpositive sink behavior, and the exact order-sensitive Mastery XP delta. Ready-output candidates preserve structured multi-skill JSON; compiler and runtime reparse current machine data and verify all six experience slots plus `MasteryExp`. Hidden/silent isolated vanilla matrix `runtime-machine-output-smoke-20260729-234047` passed a real native-configured machine plus no-config, mixed valid/invalid token, Luck/nonpositive sink, and order-sensitive Mastery-threshold cases. |
| `Object.readBook` through `Object.performUseAction` | one skill, skills 0-4, or no skill depending on current item branch | every inventory category `-102/-103` object exposes the exact native branch: skill book `250`, repeated tagged power book `100`, repeated untagged power book `20` to skills 0-4, Purple Book `250` to skills 0-4, first power-book stat/mail/Well Read consequences, or first Queen of Sauce recipe set. Native tag enumeration order, sequential Mastery XP, permanent skill-level transitions, ordered `newLevels` additions, one-item consumption, Well Read achievement/hatter-mail/dialogue side effects, and custom override blocks are preserved through candidate/compiler/runtime. A bounded wait follows the native one-second reading animation before another action may run. Hidden/silent isolated vanilla matrix `runtime-book-reading-smoke-20260729-220458` passed all six branches plus Well Read; custom override and blocked-gate runtime negatives remain pending. |

## Remaining Runtime Validation

The local-decompile non-debug `gainExperience` call-site enumeration has no remaining mechanical candidate family. Static coverage is not runtime completion: focused/full regression and isolated native smokes for the runtime-pending rows above are still required.

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
