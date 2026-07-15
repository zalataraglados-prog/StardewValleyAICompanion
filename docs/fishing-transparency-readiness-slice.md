# Fishing Transparency Readiness Slice

## Scope

This slice adds a read-only `fishing` snapshot domain, an on-demand `profile=fishing` bridge profile, rod-specific candidate contexts, the candidate-to-plan-to-queue path, and an audited bounded runtime executor. The ordinary Beach catch path is runtime-verified by the hidden/silent isolated E: smoke `runtime-fishing-daily-plan-smoke-20260713-174813`.

The profile reads:

- current location identity, `canFishHere()`, map dimensions, fishing level, luck level, and daily luck;
- every tile in the current map for which `GameLocation.isTileFishable(x, y)` returns true;
- `FishingRod.distanceToLand(x, y, location)` and `TryGetFishAreaForTile(...)` for each fishable tile;
- every fishing rod in player inventory, including bait and tackle attachments;
- a separate spawn-rule and special-catch context for every rod slot, so planning never evaluates one rod and executes another;
- the selected rod's cast, bite, nibble, reel, catch, treasure, size, quality, and caught-item state.
- the complete combined `Data/Locations:Default` and current-location `SpawnFishData` rule inventory;
- current deterministic gates for season, fish area, player/bobber rectangle, water depth, fishing level, magic bait, `GameStateQuery`, `Data/Fish` time/weather/level/training/tutorial rules, and catch limits;
- spawn-chance and per-water-depth `Data/Fish` chance previews without executing either random catch roll; tile rows already carry water depth, so this avoids duplicating identical probabilities across every tile.
- base `GameLocation.getFish` special sources: fish-pond catches, active fish-frenzy area/item, tutorial fallback, and trash fallback;
- the actual runtime declaring type for `getFish`, with a fine-grained blocker when a location-specific override has not yet been decoded.

The tile scan has no row cap. It is isolated behind `profile=fishing` so ordinary light and machine snapshots do not pay its map-scan cost. The fishing profile also includes current-location and collision-grid domains because candidate generation must prove a reachable land stand tile. Full snapshots still include the domain by definition.

## Candidate And Compiler Boundary

- `fishing.catch_fish` is one parameterized mechanical action per legal cast context: location, stand tile, fishable bobber tile, rod slot, and cast geometry. The model cannot select a rule or catch result because the native `getFish` call accepts neither.
- Every executable candidate carries `outcome_distribution_complete=true`, canonical `outcome_distribution_json`, all possible qualified item ids, and an explicit probability-completeness status. `expected_qualified_item_id` must remain empty.
- Candidate geometry follows the decompiled cardinal cast ranges and rejects collision-blocked or unreachable stand tiles.
- `DailyPlanCompiler` expands one candidate into `move_to_tile` plus the atomic `catch_fish` executor command.
- The move step carries `max_movement_tiles` from the transparent BFS route distance. Runtime movement no longer overloads the crop-count budget or silently truncates a valid fishing route.
- `ActionQueueCompiler` rechecks map, menu, energy, rod slot, fishable bobber tile, cardinal range, unresolved item queries, and rod-specific location override coverage. It rebuilds the candidate from the same snapshot and requires an exact canonical distribution match, so removing or forging one outcome blocks the queue.
- Unknown item resolvers or unknown/modded `getFish` overrides block upstream. They do not become guessed empty candidates.

## Local Decompile Evidence

- `StardewValley.Tools/FishingRod.cs:25`: `FishingRod` runtime class.
- `FishingRod.cs:38-188`: bobber, cast/bite/nibble/reel/catch and result fields.
- `FishingRod.cs:411`: a cast is accepted only when `location.canFishHere()` and `location.isTileFishable(tileX, tileY)` are true.
- `FishingRod.cs:595-650`: a hooked junk/special result legally transitions through `isFishing=false` and `pullingOutOfWater=true`; this is not an idle or failed-cast state.
- `FishingRod.cs:1086-1095`: the pull animation reaches `fishCaught=true` before the player acknowledges the held catch.
- `FishingRod.cs:851`: public `distanceToLand(...)` computes clear-water distance.
- `FishingRod.cs:943-1083`: `GetTackle`, `GetBait`, magic-bait/curiosity-lure checks, and attachment capability.
- `GameLocation.cs:2274`: `canFishHere()`.
- `GameLocation.cs:2329`: `isTileFishable(...)`, including water, no-fishing map property, building layer, and fishable-building handling.
- `GameLocation.cs:13791`: `TryGetFishAreaForTile(...)`.
- `GameLocation.cs:13849-14042`: fish selection delegates through location spawn rules, `Data/Fish`, `GameStateQuery`, item queries, distance, season, level, bait, chance, and catch limits.
- `GameLocation.cs:13849-13874`: base catch priority checks fish ponds, then active fish frenzy, then location spawn rules, then trash fallback.
- `GameLocation.cs:13908-13948`: loads `Data/Fish`, derives season/area/rod/bait context, combines default and location fish rules, and randomizes equal-precedence order.
- `GameLocation.cs:13952-14012`: applies inherited/area/season/player/bobber/level/distance/magic-bait gates, spawn chance, `GameStateQuery`, item query resolution, catch limit, and generic fish requirements in order.
- `GameLocation.cs:14042-14186`: applies `Data/Fish` training-rod, tutorial, time, weather, level, water-depth chance, curiosity-lure, targeted-bait, luck, and quantity-modifier behavior.
- `GameStateQuery.cs:2076-2094`: public condition evaluation accepts an explicit `Random`; the adapter supplies a stable local instance instead of consuming `Game1.random`.
- `StardewValley.Internal/ItemQueryResolver.cs:709-810`: resolves item IDs/queries and can choose random results from the context RNG.
- `ItemQueryResolver.cs:822-880`: `ISpawnItemData` may choose a `RandomItemId` and then resolve a random item; arbitrary registered resolvers are therefore inventoried but not executed by the read adapter.
- `Utility.cs:4335-4380`: quantity modifiers accept an explicit `Random`; chance previews use a stable local instance.
- Local `StardewValley.GameData.dll`, type `StardewValley.GameData.Locations.SpawnFishData`: exact fields for chance, season, area/rectangles, level/distance, equipment, catch limit, precedence, inheritance, modifiers, and seeded catch RNG.
- `Railroad.cs:230-243`: necklace precondition, catch item, mail, and quest side effects before base fishing.
- `MineShaft.cs:1163-1232`: mine-area fish/trash choices, training-rod behavior, bait/depth/level chance, quality, and lava-area cave-jelly fallback.
- `IslandSouthEast.cs:273-288`: Stardrop Pool rectangle, one-time walnut state, multiplayer delivery, and null result after collection.
- `Farm.cs:1109-1132`: `FarmFishLocationOverride` target/chance and base-location fallback.
- `IslandLocation.cs:364-383`: deterministic seeded 15% walnut rule, five-drop cap, multiplayer delivery, and base fallback.
- `Farmer.cs:1612`: effective `FishingLevel` includes buffs.

## Remaining Boundary

`fishing.spawn_rules` is now readable when the map tile scan and game content are available. It reports every default/current-location rule, deterministic blockers, eligible fishable-tile indices, direct-item `Data/Fish` requirements, and pending probability stages. `fishing.rod_contexts` repeats this evaluation for each actual rod slot and includes rod-specific special-source coverage.

Direct item IDs are resolved through `ItemRegistry`. Arbitrary item-query resolvers are not executed during a read snapshot because mod-provided resolvers are not guaranteed to be side-effect free. Such rules remain present with `resolution_complete=false`, the raw query, registration state, and their rule key in `unresolved_rule_keys`. Any unresolved rule blocks that rod context from candidate generation because precedence can affect the real catch distribution.

The local decompile contains `getFish` overrides in `Railroad`, `MineShaft`, `IslandSouthEast`, `Farm`, and `IslandLocation`; all five vanilla override families are now projected without executing their catch rolls or side effects. The adapter also checks the runtime method's declaring type. An unknown mod-provided override is not treated as vanilla: it adds `fishing.special_catch_sources.location_override` to `unavailable_fields` and records the runtime/declaring type.

Mine candidates include the Curiosity Lure factor, special-fish probability by water depth, area-80 cave-jelly/trash fallthrough, training-rod trash-only behavior, and the ordinary-rule fallback multiplier for other mine areas. Base tutorial/trash fallbacks are explicit candidates with `unresolved_composed_fallthrough` probability status because the combined preceding rule-failure probability is not guessed. An already-collected IslandSouthEast Stardrop Pool reserves its tiles and emits no invented normal catch.

`executor.catch_fish` has a SMAPI-side legal-input state machine and observed-result recording: it validates the candidate again, starts the native rod cast through the normal timing/release transition, and drives BobberBar only through `Game1.oldMouseState`. It does not manually advance rod/menu updates. Direct `getFish`, item injection, catch-progress mutation, teleport, and OS input injection remain forbidden substitutes.

The isolated runtime smoke legally traversed `FarmHouse -> Farm -> BusStop -> Town -> Beach`, moved to stand tile `(44,25)`, cast to fishable tile `(44,29)`, completed BobberBar, and added observed `(O)152` to inventory. The result was `applied/verified`, and one training feature row was written. The compiler-approved action carried 17 possible qualified item ids, no expected item, and `outcome_distribution_complete=True`; `(O)152` was verified as a distribution member and the runtime recorded `candidate_item_match=unconstrained`. The JSONL row itself contains both the canonical distribution input and observed catch output.

Runtime coverage now includes three special-source paths in addition to ordinary Beach fishing. Fish frenzy passed with `(O)128`, a real BobberBar, and a singleton compiled distribution. An isolated occupied FishPond passed through native construction safety checks, caught `(O)128` without BobberBar, and reduced its transparent occupant count from 1 to 0. Mine level 100 passed as mine area 80: the compiler emitted exactly lava eel, cave jelly, and trash 167-172, while the native run observed `(O)168` without BobberBar. Each run wrote both the canonical distribution input and actual output to JSONL.

The random mine lava-eel branch has not yet been observed on area 80, so only the mine non-fish branch and complete distribution are runtime-proven there. Volcano/other vanilla override paths, active Farm redirection, and unknown third-party item resolvers or `getFish` overrides still require separate smokes or remain fail-closed. These remaining gaps are not evidence that broad fishing policy training is ready.

## Tests

- `FishingTileScannerTests` proves complete deterministic map-order enumeration and invalid-dimension behavior.
- `FishingSpawnRuleEvaluatorTests` covers geometry, season/magic bait, level, condition blockers, `Data/Fish` parsing, time, weather, training rod, tutorial, and malformed rows.
- `FishingSnapshotIngestTests` proves the backend accepts readable spawn-rule inventory and still rejects a non-readable field carrying a guessed default array.
- `FishingMainlineTests` proves rod-specific aggregate mechanical candidates flow through ranking, daily-plan expansion, and queue compilation; truncating the canonical distribution is rejected; incomplete override contexts block upstream; deterministic fish-frenzy priority is retained; mine special/jelly/trash and ordinary fallback probabilities stay distinct; base trash fallback is explicit; and an exhausted Stardrop Pool cannot invent a normal catch.
- `RuntimeCatchFishExecutorTests` proves request serialization and LiveTrainingLoop parameter mapping, rejects forbidden direct-catch mutations, and checks legal update-tick input control, cancellation, and actual caught-item recording.
- `Invoke-RuntimeFishingDailyPlanSmoke.ps1` runtime PASS: `artifacts/runtime-fishing-daily-plan-smoke/runtime-fishing-daily-plan-smoke-20260713-174813/summary.json`; route 4 segments, movement and catch both verified, observed `(O)152` belongs to the 17-item compiled distribution, and both input distribution and output label are present in the dataset row.
- Special runtime PASS: fish frenzy `artifacts/runtime-fishing-frenzy-smoke/runtime-fishing-frenzy-smoke-20260713103210/summary.json`, FishPond `artifacts/runtime-fishing-pond-smoke/runtime-fishing-pond-smoke-20260713104128/summary.json`, and MineShaft area 80 non-fish `artifacts/runtime-fishing-mine-area80-smoke/runtime-fishing-mine-area80-smoke-20260713104810/summary.json`.
