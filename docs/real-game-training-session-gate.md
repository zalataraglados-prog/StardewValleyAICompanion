# Real Game Training Session Gate

This slice does not start Stardew Valley. It creates the safety and readiness boundary required before real transparent training can begin.

Process-launch readiness is not policy-data readiness. The authoritative policy gate is
[`TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md).
Even when every launcher check below passes, formal training remains blocked while the
trainer infers a positive label from `candidate.Selected` or a row lacks explicit Teacher,
native-outcome, and Student-observation provenance.

## Current Stage

- `POST /api/v1/training/session/prepare` writes a `training_run_manifest.v1`.
- `POST /api/v1/training/session/launch` starts the isolated SMAPI process only when every launch guard passes.
- `GET /api/v1/training/session/ready-probe` reports whether the backend has a transparent snapshot available.
- `GET /api/v1/training/session/ready-probe?manifest_path=...` binds readiness to a specific training run.
- Game launch defaults to disabled.
- Sound defaults to disabled and is a hard block if enabled.
- Real-game mode requires explicit `allow_game_launch=true`, a SMAPI executable path, a matching working directory, and an isolated save path.
- The executable must be inside the declared training working directory.
- Vanilla `Stardew Valley.exe` is rejected for transparent bridge training; use `StardewModdingAPI.exe`.
- Launch manifests record training environment overrides, including `SDL_AUDIODRIVER=dummy` and `ALSOFT_DRIVERS=null`.
- Launch manifests inject `STARDEWAI_TRAINING_RUN_ID`; the bridge reports it back in `state.environment.training_run_id`.

## Exit Conditions

This stage is complete when:

- Offline/simulated training can prepare a manifest without starting the game.
- Real-game training mode is blocked unless launch permission is explicit.
- Real-game launch requires SMAPI, not the vanilla executable.
- The launcher records sound-disabled environment overrides before process start.
- The ready probe is blocked before any transparent snapshot is ingested.
- The ready probe becomes ready after the bridge posts a valid transparent snapshot.
- Manifest-bound ready probe requires the snapshot run id to match the manifest run id.
- Tests cover all conditions above.
- Policy training separately proves typed supervision provenance and rejects selected-only
  behavior rows; launcher success cannot waive this gate.

## Next Stage

The next implementation slice is the actual bridge handshake:

- Resolve and call `/api/v1/training/session/prepare` against `E:\StardewValleyAICompanion-runtime\Stardew Valley\StardewModdingAPI.exe`.
- Call `/api/v1/training/session/launch` only under explicit operator supervision.
- Confirm the transparent bridge posts snapshots from the launched process and that `snapshot_run_id == run_id`.
- Refuse to train from user play saves.
