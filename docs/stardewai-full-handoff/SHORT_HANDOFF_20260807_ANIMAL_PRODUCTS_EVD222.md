# StardewAI short handoff: animal products EVD-222

Date: 2026-08-07

## Completed

- Admitted `farm.collect_animal_products` for one exact ready base `StardewValley.FarmAnimal` in the loaded location.
- Registered the existing `CollectAnimalProductSteps` in `DailyPlanCompiler.OptionCandidateCompilerKinds`; there is still one candidate, plan, queue and runtime chain.
- Added upstream rejection for custom animal and product runtime types.
- Runtime feedback now records exact output unit state and all projected native stats.
- Fixed transparent stat projection for the native post-inventory-merge temporary-stack behavior.

## Evidence

- E-drive hidden/silent matrix: `artifacts/runtime-animal-product-smoke/runtime-animal-product-smoke-20260807-101342/summary.json`.
- PASS 4/4 for Milk Pail, Shears, cracker x1/x2, new-slot and full-merge contexts.
- Production execution uses native `BeginUsingTool` and `EndUsingTool`; it does not directly clear produce, add inventory, grant XP, or edit friendship.

## Boundary

Excluded: eggs/truffles and other non-tool harvests, Auto-Grabber, unloaded animals, custom animal/product runtime types, unsupported tools, insufficient inventory and any stale projection. This admission is mechanical collection, not broad livestock strategy or `farm.care_for_pets`.

## Next slice

Audit `farm.care_for_pets` in the same order: transparent facts, sole existing compiler chain, exact native interaction/output evidence, fail-closed boundary, then five-gate admission only if runtime evidence closes every claimed branch.
