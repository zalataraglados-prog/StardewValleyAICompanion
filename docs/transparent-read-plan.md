# Transparent Read Plan

## Goal

Full transparent reading is a first-class project target. The AI engine should consume game facts from `StardewAI.TransparentBridge` through a canonical state contract, not through guesses, OCR, screenshots, process memory inspection, or manual user assertions.

The current repository is still an early skeleton. Until a reader exists for a fact, the fact must be present as `status: "unavailable"` when it belongs to the canonical target, with `value: null`, `confidence: 0`, and a concrete `reason`.

## CanonicalState Contract

`schemas/json/snapshot.schema.json` is the slice A source of truth for the target state shape. It defines:

- `state.game`
- `state.player`
- `state.farm`
- `state.locations`
- `state.npcs`
- `state.quests`
- `state.world_progress`
- `state.menus`
- `state.mods`
- `state.modded_state`

Every game-data leaf inside those domains must use the transparent field envelope:

```json
{
  "value": null,
  "status": "unavailable",
  "source": { "kind": "unavailable", "path": "state.farm.crops" },
  "adapter": "StardewAI.TransparentBridge",
  "read_at_tick": 0,
  "confidence": 0,
  "reason": "reader_not_implemented"
}
```

Required envelope metadata:

- `value`
- `status`: `available`, `derived`, or `unavailable`
- `source`
- `adapter`
- `read_at_tick`
- `confidence`

Additional semantics:

- `status: "available"` means the value came from a transparent game/API source and must not be null.
- `status: "derived"` means the value was computed from other transparent fields and must include `derivation`.
- `status: "unavailable"` means the value is intentionally unknown, must be null, must have `confidence: 0`, and must include `reason`.

## Slice Boundaries

Slice A owns only the schema contract and this plan. It must not modify `src/` or `backend/`.

Bridge implementation slices should adapt their collectors to this contract. Backend slices should validate snapshots against this contract and reject guessed or unwrapped game facts.

## Validation Target

Minimum schema validation for slice A:

```powershell
cd I:\StardewValleyAICompanion
python -m json.tool schemas\json\snapshot.schema.json > $null
python - <<'PY'
import json
from pathlib import Path
from jsonschema import Draft202012Validator

schema = json.loads(Path("schemas/json/snapshot.schema.json").read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
print("snapshot schema ok")
PY
```
