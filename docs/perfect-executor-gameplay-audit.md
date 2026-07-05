# Perfect Executor Gameplay Audit

This audit applies the mining rule to the rest of the vanilla game surface:

Low-level execution mistakes must not become strategic preference penalties. If a task can be performed by a perfect human-level executor, the model should not learn to avoid it because a weak executor would miss clicks, mistime inputs, walk into walls, lose a fishing minigame, or dodge badly.

The planner should separate:

- hard constraints: time, health, energy, inventory, state hash, menu identity, item identity, shop stock, map passability, irreversible effects.
- calibration factors: random layout, random loot, fish selection, treasure chance, quality rolls, NPC schedule changes, event seed.
- excluded preference penalties: low-level input errors that a perfect executor should not make.

## Coverage

This is a whole-game gameplay-domain audit for the core vanilla interaction surface. It is not a claim that every decompiled method has been converted into an executor. Each row defines how future executors and training feedback must classify similar situations.

| Domain | Executor assumption | Hard constraints | Calibration factors | Must not penalize as preference |
| --- | --- | --- | --- | --- |
| Mining and combat | perfect human movement, weapon timing, ladder use, retreat | time, health, energy, inventory, route to exit/elevator | mine level, layout RNG, ladder discovery, monster mix, ore/loot | bad dodging, missed swings, slow micro pathing |
| Volcano dungeon | perfect human movement/combat/puzzle execution | time, health, energy, exit route, required water/bridge state | generated level, monsters, resource nodes, forge access | bad dodging, missed weapon timing, poor movement |
| Fishing | perfect bite response and bobber control | time, energy, fishable tile, rod/bait/tackle | bite time, selected fish, difficulty, treasure chance, weather/season/time | missed bite, bad bobber control, failed perfect catch due to inputs |
| Navigation/pathing | perfect route follower after passability proof | passability, warps, doors, destination availability, time | route length, temporary obstacles, NPC blockage | walking into walls, wrong turns, slow movement |
| Crop farming | perfect tile targeting/tool use | energy, tool, reachable tiles, inventory | crop quality, extra harvest, mixed seed results, fertilizer | missed tiles, wrong tool timing, slow watering |
| Forestry/resource clumps | perfect axe/pickaxe use and debris collection | energy, tool, reachability, inventory/debris space | drops, tree growth, seed drops, clump health | bad swing timing, missed tiles |
| Machines | perfect interaction once machine state is read | input item, ready state, output space, inventory | machine timers, output item/quality rules | misclicking machine, slow interaction |
| Animals | perfect pet/milk/shear interaction | animal location, tool/item, inventory, time | mood/friendship effects, produce quality | missed pet, bad milking/shearing micro |
| Shops and menus | perfect menu interaction after menu identity proof | stock, price, budget reserve, protected item policy, menu identity | limited stock, shop rules, dialogue variance | misclick purchase, wrong scroll, slow menu navigation |
| NPC/social/quests | perfect route and dialogue/gift delivery after target proof | NPC identity/location, schedule, gift item, quest step, time window | route changes, dialogue variance, quest reward randomness | wrong NPC click, missed dialogue advance |
| Festivals/minigames | perfect minigame inputs only after rules are modeled | event state, entry/exit, time window, rule model | event random seed, scoring/reward rules | bad minigame inputs, missed dialogue/menu inputs |
| Inventory/chests/irreversible actions | perfect slot/menu operation after identity proof | item identity, protected item policy, target container, state hash | stack merge, layout, inventory capacity | wrong slot drag, misclick item |

## Decompiled Anchors

Representative local decompile anchors used for this audit:

- Time: `Game1.timeOfDay` advances in 10-minute steps and caps at `2600`.
- Energy: `Farmer.Stamina` and `Farmer.MaxStamina`.
- Mining: `MineShaft.mineLevel`, `mineRandom`, `findLadder`, ladder fields, monster areas.
- Volcano/combat: `VolcanoDungeon`, monster classes under `StardewValley.Monsters`.
- Fishing: `FishingRod.minFishingBiteTime`, `maxFishingBiteTime`, `baseChanceForTreasure`, `FishingGame`.
- Navigation: `PathFindController`, `GameLocation.isCollidingPosition`, `GameLocation.warps`.
- Crops: `Crop.phaseDays`, `Crop.harvest`, `HoeDirt`.
- Shops: `ShopMenu.forSale`, `itemPriceAndStock`, `safetyTimer`, `ShopBuilder.GetShopStock`.
- Menus/inventory: `ShopMenu`, `Farmer.Items`, item identity.

## Training Rule

For every option, label feedback in three channels:

1. `strategy_value`: goal progress, rewards, unlocks, resource efficiency, novelty.
2. `hard_feasibility`: transparent-state constraints that block or allow execution.
3. `executor_calibration`: elapsed time, resource cost, stochastic outcomes, recovery frequency.

Do not write low-level perfect-executor failures into `strategy_value`. If an executor performs poorly, improve the executor or adjust `executor_calibration`; do not train the model to dislike the gameplay domain.

## Immediate Engineering Implication

The time budget and simulator should query `ExecutionAssumptionRegistry` before scoring risk. A future `MiningPerfectExecutorModel`, `FishingPerfectExecutorModel`, and `NavigationPerfectExecutorModel` should consume these assumptions and produce calibrated duration/resource estimates without injecting low-level control fear into the ranker.
