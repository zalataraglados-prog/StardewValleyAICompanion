#!/bin/sh
set -eu

APP_ROOT="${STARDEWAI_APP_ROOT:-/stardewai-app}"
TRAINING_ROOT="${STARDEWAI_FORMAL_TRAINING_ROOT:-/state/formal-training}"
RUN_ID="${STARDEWAI_FORMAL_RUN_ID:?STARDEWAI_FORMAL_RUN_ID is required}"
SAVE_ROOT="${STARDEWAI_FORMAL_SAVE_ROOT:-/config/xdg/config/StardewValley/Saves}"
SAVE_SLOT="${STARDEWAI_FORMAL_SAVE_SLOT:?STARDEWAI_FORMAL_SAVE_SLOT is required}"
MIN_FREE_SPACE_MB="${STARDEWAI_FORMAL_MIN_FREE_SPACE_MB:-8192}"
MAX_ATTEMPTS="${STARDEWAI_FORMAL_MAX_ATTEMPTS:-2000}"
MAX_PERSISTED_ITERATIONS="${STARDEWAI_FORMAL_MAX_PERSISTED_ITERATIONS:-4}"
SAVE_BOUNDARY_MAX_ATTEMPTS="${STARDEWAI_FORMAL_SAVE_BOUNDARY_MAX_ATTEMPTS:-16}"
BACKEND_URL="http://127.0.0.1:8795"
PRODUCT_URL="http://127.0.0.1:8768"
BRIDGE_URL="http://127.0.0.1:8765"
NATIVE_EXECUTOR_URL="http://127.0.0.1:8767"
RUN_DIR="$TRAINING_ROOT/runs/$RUN_ID"
MANIFEST_PATH="$RUN_DIR/training-run-manifest.json"

case "$RUN_ID" in
  *[!A-Za-z0-9._-]*|'')
    echo "invalid formal run id" >&2
    exit 2
    ;;
esac

case "$MIN_FREE_SPACE_MB" in
  *[!0-9]*|'')
    echo "invalid formal minimum free space" >&2
    exit 2
    ;;
esac
if [ "$MIN_FREE_SPACE_MB" -lt 1024 ]; then
  echo "formal minimum free space must be at least 1024 MB" >&2
  exit 2
fi

case "$MAX_ATTEMPTS" in
  *[!0-9]*|'')
    echo "invalid formal maximum attempts" >&2
    exit 2
    ;;
esac
if [ "$MAX_ATTEMPTS" -lt 1 ]; then
  echo "formal maximum attempts must be at least 1" >&2
  exit 2
fi

case "$MAX_PERSISTED_ITERATIONS" in
  *[!0-9]*|'')
    echo "invalid formal maximum persisted iterations" >&2
    exit 2
    ;;
esac
if [ "$MAX_PERSISTED_ITERATIONS" -lt 1 ] || [ "$MAX_PERSISTED_ITERATIONS" -gt 64 ]; then
  echo "formal maximum persisted iterations must be between 1 and 64" >&2
  exit 2
fi

case "$SAVE_BOUNDARY_MAX_ATTEMPTS" in
  *[!0-9]*|'')
    echo "invalid formal save-boundary maximum attempts" >&2
    exit 2
    ;;
esac
if [ "$SAVE_BOUNDARY_MAX_ATTEMPTS" -lt 1 ] || [ "$SAVE_BOUNDARY_MAX_ATTEMPTS" -gt 128 ]; then
  echo "formal save-boundary maximum attempts must be between 1 and 128" >&2
  exit 2
fi

for path in \
  "$APP_ROOT/StardewAI.Backend" \
  "$APP_ROOT/StardewAI.ProductExecutor" \
  "$APP_ROOT/StardewAI.LiveTrainingLoop" \
  "$TRAINING_ROOT/datasets/policy-decision-trajectories.jsonl" \
  "$TRAINING_ROOT/datasets/formal-policy/policy-dataset-manifest.json" \
  "$TRAINING_ROOT/checkpoints/structured-policy-latest.json" \
  "$SAVE_ROOT/$SAVE_SLOT/$SAVE_SLOT"
do
  if [ ! -f "$path" ]; then
    echo "required formal training input is missing: $path" >&2
    exit 3
  fi
done

for executable in \
  "$APP_ROOT/StardewAI.Backend" \
  "$APP_ROOT/StardewAI.ProductExecutor" \
  "$APP_ROOT/StardewAI.LiveTrainingLoop" \
  "$APP_ROOT/StardewAI.PolicyDataset" \
  "$APP_ROOT/StardewAI.PolicyModel"
do
  if [ ! -x "$executable" ]; then
    echo "formal training executable bit is missing: $executable" >&2
    exit 3
  fi
done

if curl -fsS "$BACKEND_URL/health" >/dev/null 2>&1; then
  echo "formal backend port is already in use" >&2
  exit 4
fi
if curl -fsS "$PRODUCT_URL/health" >/dev/null 2>&1; then
  echo "formal product executor port is already in use" >&2
  exit 4
fi

game_pids="$(pgrep -f '^/data/game/StardewModdingAPI( |$)' || true)"
if [ "$(printf '%s\n' "$game_pids" | sed '/^$/d' | wc -l)" -ne 1 ]; then
  echo "expected exactly one attached SMAPI process, got: $game_pids" >&2
  exit 5
fi
game_pid="$game_pids"

mkdir -p "$RUN_DIR" "$TRAINING_ROOT/logs"
request_path="$RUN_DIR/launch-request.json"
cat >"$request_path" <<EOF
{
  "run_id": "$RUN_ID",
  "mode": "formal_product_training",
  "root_path": "$TRAINING_ROOT",
  "dataset_path": "$TRAINING_ROOT/datasets/live-training-feature-rows.jsonl",
  "report_path": "$RUN_DIR/training-report.json",
  "checkpoint_path": "$TRAINING_ROOT/checkpoints/structured-policy-latest.json",
  "policy_trajectory_path": "$TRAINING_ROOT/datasets/policy-decision-trajectories.jsonl",
  "policy_dataset_manifest_path": "$TRAINING_ROOT/datasets/formal-policy/policy-dataset-manifest.json",
  "product_receipt_root": "$RUN_DIR/product-receipts",
  "product_executor_url": "$PRODUCT_URL",
  "native_executor_url": "$NATIVE_EXECUTOR_URL",
  "product_executor_executable_path": "$APP_ROOT/StardewAI.ProductExecutor",
  "live_training_loop_executable_path": "$APP_ROOT/StardewAI.LiveTrainingLoop",
  "target_execution_mode": "dedicated_host_ai",
  "max_attempts": $MAX_ATTEMPTS,
  "max_persisted_iterations": $MAX_PERSISTED_ITERATIONS,
  "required_verified_actions": 0,
  "require_native_save_boundary": true,
  "save_boundary_max_attempts": $SAVE_BOUNDARY_MAX_ATTEMPTS,
  "min_free_space_mb": $MIN_FREE_SPACE_MB,
  "manifest_path": "$MANIFEST_PATH",
  "game_executable_path": "/data/game/StardewModdingAPI",
  "game_working_directory": "/data/game",
  "save_isolation_path": "$SAVE_ROOT",
  "save_slot": "$SAVE_SLOT",
  "bridge_url": "$BRIDGE_URL",
  "backend_url": "$BACKEND_URL",
  "attach_existing_game": true,
  "existing_game_process_id": $game_pid,
  "allow_game_launch": false,
  "sound_enabled": false,
  "window_style": "hidden"
}
EOF

ASPNETCORE_URLS="$BACKEND_URL" nohup "$APP_ROOT/StardewAI.Backend" \
  >"$RUN_DIR/backend.stdout.log" 2>"$RUN_DIR/backend.stderr.log" &
backend_pid=$!
printf '%s\n' "$backend_pid" >"$RUN_DIR/backend.pid"
cleanup_backend=true
cleanup() {
  if [ "$cleanup_backend" = true ]; then
    kill "$backend_pid" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT INT TERM

attempt=0
until curl -fsS "$BACKEND_URL/health" >/dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 120 ] || ! kill -0 "$backend_pid" >/dev/null 2>&1; then
    echo "formal backend failed its startup probe" >&2
    exit 6
  fi
  sleep 1
done

curl -fsS -X POST -H 'Content-Type: application/json' --data-binary "@$request_path" \
  "$BACKEND_URL/api/v1/training/session/prepare" >"$RUN_DIR/prepare-result.json"
if ! grep -q '"blocked":false' "$RUN_DIR/prepare-result.json"; then
  echo "formal prepare was blocked; inspect $RUN_DIR/prepare-result.json" >&2
  exit 7
fi

curl -fsS -X POST -H 'Content-Type: application/json' --data-binary "@$request_path" \
  "$BACKEND_URL/api/v1/training/session/launch" >"$RUN_DIR/launch-result.json"
if ! grep -q '"blocked":false' "$RUN_DIR/launch-result.json" ||
   ! grep -q '"started":true' "$RUN_DIR/launch-result.json"; then
  echo "formal launch was blocked; inspect $RUN_DIR/launch-result.json" >&2
  exit 8
fi

cleanup_backend=false
trap - EXIT INT TERM
echo "formal attached training started: $RUN_ID"
