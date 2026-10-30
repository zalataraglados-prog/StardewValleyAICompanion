# Master Angler source stability: v89 cross-review

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

Before `90a1424e`, `CandidateOptionAvailabilityEvaluator.FishingRouting.cs` published a remote
route before opening, whereas its current-location branch requires an eligible
runtime cast. At arrival, absence of a cast is mistaken for absence of the
still-valid objective. The route oscillation guard correctly detects the
downstream symptom; weakening it would hide the planning defect.

Native rule evidence: locked decompile `StardewValley/StardewValley/GameLocation.cs`,
lines 14087–14130, checks ordinary fish time as `start <= time < end`, and weather
against the location. An 1130 cast must not be fabricated for an 1800 window.

## Verdict and reconciled regression contract

**Accept `90a1424e` for the observed pre-window oscillation.** Its upstream
deferral is preferable to travelling at 1130 and waiting at the destination until
1800. `ApplyMasterAnglerSourceWindowTiming` runs after shared fresh intent and
route-budget validation: the remote candidate remains available today but is
`AllowedNow=false`, with explicit source-window timing parameters. The existing
ranker schedules it as `deferred`; the shared daily plan waits rather than
traversing if no ready work is selected. No second selector/executor is added.

`CurrentTeacherFrontierSupport.ReadCurrentCandidates` alone admits available
deferred candidates, so the new frontier's `IsDeferredSourceRoute` check is
essential: deferred routes must contribute neither current Master Angler bindings
nor positive/negative labels. At 1800, fresh evaluation restores the exact route,
and arrival rebinds the exact runtime terminal attempt through the existing
compiler and continuation filter. Genuine window expiration, weather mismatch
and source mismatch still fail closed. Identity filtering remains control-only
and does not rewrite preference eligibility/provenance.

This supersedes the initial regression's demand for **pre-window departure plus
local waiting**, not native fishing legality or exact identity. It does **not**
prove persistent commitment across arbitrary pre-window local replans: the local
branch still requires a legal runtime cast and has no new wait candidate. That
broader original TASK invariant is not implemented by this commit; REVIEW's
upstream-deferral contract addresses the observed v89 path without needing it.

`MasterAnglerSourceStabilityTests.cs` reuses the existing mainline fixture,
validator, availability evaluator, ranker, queue compiler, continuation filter,
target-date intent producer and actual Teacher frontier builder. Synthetic
72-species inventory/lowering/catalog inputs bind by hash and pass the existing
authority validators; they are not native production evidence. Synthetic day 5
and a deliberately restricted sunny window exercise invalidation. Both Town:5
and Forest:9 cover deferral/no-label admission at 1130 and route/terminal action
at 1800. Local and remote expiration/weather/artifact-source rejection and the
unchanged control-only exact-source filter are also covered.

## Verification and limitations

Run from the sandbox (all generated build output stays outside the checkout):

```powershell
dotnet test "tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj" -c Release --artifacts-path "C:\Users\18236\AppData\Local\Temp\opencode\master-angler-source-stability" -p:GamePath="E:\StardewValleyAICompanion-runtime\Stardew Valley" -p:EnableModDeploy=false -p:EnableModZip=false --filter "FullyQualifiedName~SourceStability"
```

Historical red evidence before the production fix: **7 passed / 2 failed** at
the empty local continuation set (commit `14dd491d`). Re-running those original
tests against `90a1424e` gave **7 passed / 2 failed**, now because the compiler
correctly emits `executor.wait_ticks` instead of the demanded early connector.
The regression was therefore reconciled with REVIEW rather than weakened to
accept premature traversal. An intermediate expanded-fixture run failed because
the base snapshot has no `farm` section; the fixture now supplies its explicit
farm-type input for the real intent producer.

The same command with filter
`FullyQualifiedName~LiveTrainingLoopQueueReplanFilterTests|FullyQualifiedName~RouteOscillationGuardTests`
passed **68/68**. Reconciled `SourceStability` tests passed **15/15**, with no
skips. `git diff --check` passed. The queue test source locator uses its compiler-supplied
source path so source guards work with external `--artifacts-path`; initially
seven guards failed only because their binary directory was outside the checkout.

Supplemental run with filter
`FullyQualifiedName~MasterAnglerMainlineTests|FullyQualifiedName~MasterAnglerRouteTimingTests`
passed **47/49**. The two failures are unchanged Ginger Island / Dark Talisman
source guards using `RuntimeHarnessSources.FindRepositoryRoot`, which searches
only parents of the binary directory and cannot locate this checkout from the
external artifacts directory. That helper is outside the existing allowlist;
this review does not claim the supplemental selection is green or change the
helper merely to satisfy it.

The narrow timing-method filter
`FullyQualifiedName~AuthoritativeRemoteIntent|FullyQualifiedName~ConcurrentWindowsKeepDistinctCandidateIdentities`
passed **8/8**. A preceding class-name-only `MasterAnglerRouteTimingTests` filter
matched no tests: that file contributes to the partial `MasterAnglerMainlineTests`
class and must be selected by method name instead.

No production, guard thresholds, training gates or live runtime are changed by
this review patch. No game run is permitted or performed. Offline fixture
coverage cannot prove a fresh native run has eliminated oscillation, general
source commitment, narrow-window scheduling optimality, or Master Angler
completion; formal training remains unauthorized. No additional production
change is required for the scoped v89 fix; broader commitment needs a separate
explicit contract if still required by the controller.
