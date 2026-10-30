# Independent Golden Walnut source-schedule verification

This is an offline, read-only comparison boundary, not a training admission gate
or a runtime controller. Run it **after** the evidence directory, manifest and
loop-emitted schedule have been frozen. Do not run a rebuilding command over the
original manifest or schedule before comparison: their original bindings are evidence.

## Entry point and inputs

`verify-golden-walnut-fresh-save-source-schedule` in GoalConditionedBootstrap calls
`GoldenWalnutFreshSaveSourceScheduleVerifier.Verify` in Core. The verifier:

1. Calls the existing `GoldenWalnutFreshSaveEpisodeManifestBuilder.BuildFromDirectory`
   to independently recover ordered episode membership from the evidence directory.
2. Compares all manifest properties against the frozen manifest, including ordered
   episode paths and the initial-boundary path. Paths must be the canonical absolute
   paths emitted by the existing manifest writer; relocation or rewriting paths is
   not accepted as the original run binding.
3. Validates the initial boundary with the existing validator and compares the
   save/player identity against explicit caller-supplied expected identities.
4. Calls the existing `GoldenWalnutFreshSaveEpisodeScheduleBuilder.Build` against
   that proven manifest. No second schedule derivation exists.
5. Requires a nonempty source-completion set, then compares every serialized
   property of the existing schedule contract with the loop-emitted schedule.

No loop report or `golden_walnut_source_schedule_verified` flag is consumed.
Fresh failed non-progress continuity witnesses remain governed by the existing
builders; they cannot contribute source or unlock progress.

When `Invoke-ProductBootstrapCalibration.ps1` runs with
`-RequireGoldenWalnutSourceScheduleReady`, it invokes this command after the loop
has frozen its manifest and schedule but before writing a successful calibration
summary. The wrapper supplies save/player anchors from the initial transparent
snapshot, requires a parsed pass receipt, and writes
`golden-walnut-source-schedule-independent-verification.json` beside the product
calibration summary, outside `live-snapshots`. A nonzero verifier exit, malformed
JSON, mismatched identity/path/contract field, or absent receipt fails the wrapper
closed. Ordinary bounded calibration does not invoke or claim this verification.

### Completed v89 invocation (PowerShell, from the repository root)

The product root/run identity come from the task package. Expected save/player
identities below were read from its durable `initial-snapshot.json`; they are not
inferred from the claimed schedule or the loop's success flags.

```powershell
$runId = 'golden-walnut-source-schedule-v89-20261030-185156-source-schedule'
$productRoot = "I:\StardewAITraining\golden-walnut-source-schedule-v89\product-calibration\$runId"
$runRoot = Join-Path $productRoot "training\runs\$runId"
dotnet run --project experiments/StardewAI.GoalConditionedBootstrap/StardewAI.GoalConditionedBootstrap.csproj -c Release --no-build --no-restore -- verify-golden-walnut-fresh-save-source-schedule `
  --episode-directory "$runRoot\live-snapshots" `
  --episode-manifest "$runRoot\golden-walnut-fresh-save-episode-manifest.json" `
  --source-schedule "$runRoot\golden-walnut-fresh-save-source-schedule.json" `
  --schedule-id "golden-walnut-fresh-save:$runId" `
  --run-id $runId --save-id 'ProofFarm' --player-id '-5294839890561161059'
```

Success: exit `0`, JSON on stdout containing `status: pass`, the three absolute
input paths, expected player ID, `training_label_eligible: false`, and the entire
independently reconstructed schedule. Failure: exit `1`, JSON on stderr containing
`status: error`, exception type and a diagnostic (comparison mismatches identify
the field). The command itself writes no files. Capture stdout/stderr outside the
frozen evidence tree if a durable verification receipt is needed. Optional
existing Bootstrap validation tracing is separate and should also target outside
that tree. No `--output` is required or consumed.

## Complete compared schedule contract

- `schema_version`, `schedule_id`, `goal_id`, `game_version`, `training_label_eligible`;
- `source_episode_manifest_sha256`: binds the original manifest logical bytes;
- `source_episode_evidence_root_sha256`: existing builder's ordered hash of initial
  boundary, then each episode and its materialized before/after snapshots;
- `source_run_id`, `source_save_id`, `source_episode_count`;
- `first_island_arrival_total_day`, `planned_first_parrot_unlock_total_day`,
  `planned_western_turtle_unlock_total_day`, `planned_island_house_unlock_total_day`;
- ordered `source_completions`: exact membership and each `source_key`,
  `planned_quantity`, `planned_completion_total_day`.

Missing, additional or duplicate object properties are rejected, including a
missing false-valued eligibility property. Object property order/JSON whitespace
are immaterial to field comparison; evidence bytes remain hash-bound. Array order
is significant. Shared builders validate readable identities, run/save/player
continuity, initial zero-progress boundary, receipt/snapshot binding, chronology,
bounded passive gaps, source quantities and actual native false-to-true history.
Missing/unreadable files, missing content-addressed blobs, malformed JSON, changed
evidence roots, source-free histories and missing native transitions fail closed.

## Verification evidence and limits (2026-10-30)

- Focused verifier/episode/continuity/save-identity/arrival guards: **98 passed**,
  including independent CLI success/failure and content-addressed replay after
  rolling retention. New verifier tests are included in GameFreeGovernance.
  Exact command:

  ```powershell
  dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~GoldenWalnutFreshSaveSourceScheduleVerifierTests|FullyQualifiedName~GoldenWalnutFreshSaveEpisodeScheduleBuilderTests|FullyQualifiedName~GoldenWalnutSaveIdentityTests|FullyQualifiedName~GoldenWalnutFreshSaveEpisodeContinuityVerifierTests|FullyQualifiedName~GoldenWalnutScheduleIdentitySourceGuardTests|FullyQualifiedName~GingerIslandArrivalEvidenceSourceGuardTests"
  ```

- `dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj -c Release --no-restore -p:GameFreeGovernance=true`:
  **604 passed**.
- Required full `dotnet test tests/StardewAI.Core.Tests/StardewAI.Core.Tests.csproj -c Release --no-restore`:
  **3125 passed / 36 failed**. Failures are in untouched registry counts, old
  source-string guards and candidate/recovery/material-transfer fixtures; the
  canonical status documents already record 36 full-Core baseline failures.
  This acceptance item is **not green**; no unrelated fixtures were weakened.
- Both test commands required a process-local `GamePath` pointing to the existing
  isolated SMAPI installation. Without it the local F-drive auto-discovery failed
  before tests. No repository config, deployment or runtime process was changed.
- Required Bootstrap Release `--no-restore` build: **0 warnings / 0 errors**.
- `git diff --check`: passed.
- No completed v89 manifest/schedule comparison was attempted while the run was
  live. Fixture success is not production run success or training authorization.
- This module shares builder semantics and therefore does not independently
  certify the bridge's native truth, cryptographically authenticate provenance,
  prove a final save boundary, complete the full walnut timeline, or satisfy #345.
  Frozen inputs are a precondition; concurrent mutation is outside this boundary.
  No new game rules, training inputs/labels or model outputs were introduced.
