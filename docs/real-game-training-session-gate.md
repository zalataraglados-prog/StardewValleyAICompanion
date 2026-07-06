# Real Game Training Session Gate

This slice does not start Stardew Valley. It creates the safety and readiness boundary required before real transparent training can begin.

## Current Stage

- `POST /api/v1/training/session/prepare` writes a `training_run_manifest.v1`.
- `GET /api/v1/training/session/ready-probe` reports whether the backend has a transparent snapshot available.
- Game launch defaults to disabled.
- Sound defaults to disabled and is a hard block if enabled.
- Real-game mode requires explicit `allow_game_launch=true`, a game executable path, and an isolated save path.

## Exit Conditions

This stage is complete when:

- Offline/simulated training can prepare a manifest without starting the game.
- Real-game training mode is blocked unless launch permission is explicit.
- The ready probe is blocked before any transparent snapshot is ingested.
- The ready probe becomes ready after the bridge posts a valid transparent snapshot.
- Tests cover all conditions above.

## Next Stage

The next implementation slice is the actual safe launcher:

- Resolve the isolated Stardew/SMAPI executable path from the training copy.
- Start only the isolated copy.
- Keep sound disabled.
- Confirm the transparent bridge posts snapshots from that launched process.
- Refuse to train from user play saves.
