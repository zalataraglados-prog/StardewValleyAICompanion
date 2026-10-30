# Master Angler source stability: v89 regression handoff

## Evidence and diagnosis

Read-only evidence run: `golden-walnut-source-schedule-v89-20261030-185156-source-schedule`,
under the task's `I:/StardewAITraining/golden-walnut-source-schedule-v89/product-calibration/`
root, `training/runs/<run>/live-snapshots/`.

- `stage-one-collection-ranking-0431.json:165428` publishes an available Forest ->
  Town route for `(O)132`, `Town:5`, 1800–2600, at 1130. Its learner rank is 50;
  `stage-one-collection-preference-0431.json:21821` independently orders it first.
- `continuation-refresh-evidence-0431-item-0002.json` retains the exact Town
  continuation, but availability produces no exact runtime attempt and zero
  materialized/selected candidates after arrival, still at 1130.
- `stage-one-collection-ranking-0434.json:138234` instead publishes the reverse
  Town -> Forest route, `Forest:9`, for the same fish/window/time.
  `stage-one-collection-preference-0434.json:21719` independently orders it first.
- Both preference artifacts are `ready`, label-eligible under their existing
  independent preference policy, and explicitly **not** formal-training authorized.
  This is not a learner-rank or authoritative-tie defect.

`CandidateOptionAvailabilityEvaluator.FishingRouting.cs` can publish a remote
route before opening, whereas its current-location branch requires an eligible
runtime cast. At arrival, absence of a cast is mistaken for absence of the
still-valid objective. The route oscillation guard correctly detects the
downstream symptom; weakening it would hide the planning defect.

Native rule evidence: locked decompile `StardewValley/StardewValley/GameLocation.cs`,
lines 14087–14130, checks ordinary fish time as `start <= time < end`, and weather
against the location. An 1130 cast must not be fabricated for an 1800 window.

## Required invariant and test boundary

Keep the target fish **and exact location/source kind/source key and authoritative
window/artifact identity**, not just the fish ID, across fresh replans. A future
opening is not invalidation. Reuse the existing bounded wait, compiler and
continuation path; after each wait revalidate the source against fresh state.
Expired, weather-blocked or no-longer-authoritative sources must still fail
closed. Stability is a control decision, not new Teacher preference evidence.

`MasterAnglerSourceStabilityTests.cs` reuses the existing mainline fixture,
validator, availability evaluator, ranker, queue compiler and continuation filter.
It uses synthetic day 5 and a deliberately restricted sunny authority window to
exercise weather invalidation; it is not a full native fish-data or v89 replay.
Both actual source identities are covered, along with same-fish/different-source
rejection and expiration/weather/artifact-source rejection.

## Verification and remaining production requirement

Run from the sandbox (all generated build output stays outside the checkout):

```powershell
dotnet test "tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj" -c Release --artifacts-path "C:\Users\18236\AppData\Local\Temp\opencode\master-angler-source-stability" -p:GamePath="E:\StardewValleyAICompanion-runtime\Stardew Valley" -p:EnableModDeploy=false -p:EnableModZip=false --filter "FullyQualifiedName~SourceStability"
```

Result: **7 passed, 2 failed, 0 skipped**. Both failures are the intended
`SourceStabilityPreservesChosenSourceAfterArrivalBeforeOpening` regression,
at the empty fresh continuation candidate set. The subsequent wait compilation
assertions have **not** been reached or proved. The production peer must close
this gap; this test-only patch is **not merge-ready as a green fix**.

The same command with filter
`FullyQualifiedName~LiveTrainingLoopQueueReplanFilterTests|FullyQualifiedName~RouteOscillationGuardTests`
passes **68/68**. The queue test source locator now uses its compiler-supplied
source path so source guards work with external `--artifacts-path`; initially
seven guards failed only because their binary directory was outside the checkout.
`git diff --check` was also run. No production, guard thresholds, training gates,
Teacher artifacts or live runtime were changed. No live game run was performed.

Acceptance remains partial: the red regression proves the failure and specifies
the desired wait/continuation contract, but a green production-side result and
end-to-end control-artifact provenance for that new path remain outstanding.
