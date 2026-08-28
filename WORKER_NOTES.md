# Worker Notes

> Archived worker-era notes. This file is not a current task list and must not override source, tests, issues, or the latest handoff records under `docs/stardewai-full-handoff/`.

## 2026-07-13 Fishing Native Cast Closure

- Scope: bounded native `executor.catch_fish` cast, sustained BobberBar control, fail-closed verification, idle cleanup gate, and verified-action-count live loop semantics.
- Active user-play constraint remained in force. I did not launch the game/SMAPI, deploy, build, test, run smoke scripts, touch `E:`, access credentials, alter RNG, force catches, push, reset, clean, rebase, switch branches, or edit the real repository.
- Primary evidence came from `I:\StardewValleyAICompanion-decompile`; controller review blockers are recorded in `CONTROLLER_REVIEW.md` and evidence anchors in `evidence.md`.

## Completed

- Candidate generation now prefers legal reachable maximum-power fishing casts and records target power/max-cast metadata.
- Action queue validation rejects mismatched target power/max-cast metadata against compiled bobber geometry.
- Runtime harness now holds native use-tool input over time, releases at target power, records observed peak/release power, and keeps exact bobber-tile verification.
- BobberBar control now records and applies a decision every relevant update through the controlled input wrapper until terminal success/failure.
- Native hook handling now latches before `DoFunction`, records `hook_attempt_count`, and blocks impossible junk/special pull plus BobberBar combinations.
- Normal fish success requires BobberBar terminal success, fish hold, inventory/collection update, and full idle cleanup; junk/special without BobberBar is a separate terminal path.
- Live loop `--required-verified-actions` counts verified applied executions, not raw attempts; unverified attempts are progress diagnostics and do not append calibration rows.

## Verification

- Static review only in this pass; focused tests were added but not run.
- Pending commands are listed in `test-results.txt` and were not executed.
