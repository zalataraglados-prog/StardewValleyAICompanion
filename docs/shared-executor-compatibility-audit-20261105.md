# Shared Executor Compatibility Audit, 2026-11-05

## Change Discipline

Before treating old executor code as incorrect, inspect its introducing commit
and the corresponding local conversation JSON. Preserve the original failure
case, inspect shared callers, and test both the old valid path and the proposed
change. A new validation requirement is not itself a regression. New modules
must adapt to existing execution contracts unless a demonstrated correctness
problem requires changing a shared contract.

No workers or paid API were used. This audit does not resume the paused Master
Angler primary, change coverage counters, or authorize formal training.

## Historical Intent And Narrow Repairs

- `989fa5f7`: the 2026-11-02 04:47:21 UTC conversation records idle pause freezing
  the gap between profession selection, remaining menus and saving. Native
  transitions must retain control. After an exact saved, safe next-day state,
  however, missing follow-up work evidence must not block legacy home/tent sleep
  or disable idle pause. One RuntimePrimitives predicate now governs these
  callers and the read-only pause API. Starting another valid sleep preserves
  the old incomplete proof as blocked, never as a synthetic completion.
- `d33e3c72`: the 2026-11-02 07:49:34 UTC conversation records tent callbacks
  reopening unrelated terminal transactions. Run/queue/item and terminal-state
  protections remain unchanged. Full composite acceptance still needs registered,
  same-run fresh work; native completion alone is not composite completion.
- `deee46e9`: the 2026-11-01 16:50:40 UTC conversation records a genuine 31-tile
  route failing under a 32-tile execution limit after dynamic occupancy changed.
  The bounded 16-tile allowance and compiler refresh remain. A relocation caller
  had inferred physical distance from padded execution time, turning 7 tiles
  into 22. It now consumes compiler-derived shortest-path metadata. The existing
  ledger's geometric route/base-cost contract is unchanged; candidate time and
  profitability calculations separately include the conservative execution
  budget for every connector.
- `48c34fb6`: the 2026-10-29 02:29:37 UTC conversation records unnecessary hidden
  rendering causing heat and fan noise. Suppression remains outside native farm
  events. Locked 1.6.15 `QiPlaneEvent.draw()` increments `textTimer` and
  `finalFadeTimer`, which `tickUpdate()` needs to finish the event. The draw
  prefix now permits native drawing while a FarmEvent exists, including hidden
  windows. It does not write event fields, skip the event or invent an outcome.

Profession provenance, exact geode pickup context, material ownership/reservation
checks, verified partial-progress receipts with `success=false`, and durable
dispatch cancellation protection were retained. Focused tests did not justify
weakening them.

## Verification

- The native-control predicate first reproduced the ready-state ownership error.
  The original relocation test also failed with expected distance 7, actual 22.
- Normal Core shared-path regression: 225 passed; Backend shared-path regression:
  313 passed. Complete game-free Core after adding the omitted night, tent and
  relocation suites: 1477 passed, zero skipped.
- Harness and Release runtime dependencies built with zero warnings/errors using
  explicit E game references. Both PowerShell entrypoints passed AST parsing.
- `Invoke-RuntimeSleepCompatibilitySmoke.ps1` reuses the original Full Shipment
  profession/save scenario, then executes two legacy home sleeps without a
  planning-origin marker. After the fixture warp it uses the existing bounded
  30-tick native control-settle primitive, not fabricated control flags.
- Local run `runtime-sleep-compatibility-20261105-settled-final` passed: real
  QiPlaneEvent, profession selection, saving and normal next-day work; then two
  verified legacy nights (days 169 and 170), both with owned idle pause restored.
  The previous unfulfilled proof remained blocked, the new proof remained
  incomplete, and source-save hashes matched. Only Bridge and Harness loaded.

## Limits And Retained Diagnostics

The initial ambient-mod run included JunimoTestClient and did not establish idle
pause compatibility. Its third-party interaction is not certified by this audit.
Two subsequent isolated diagnostics exposed the draw-dependent native event and
the missing post-warp settle step in the test driver. They are not PASS evidence.
The thin base smoke now offers optional `-IsolateMods`; default callers are not
silently switched to a different mod set, and bundle hashes follow the loaded
mod directory.

Raw runs remain local under `artifacts/runtime-full-shipment-terminal/` and are
not committed. The final native run is a focused local regression, not a new
Teacher/native corpus, global acceptance, full-game regression or owner visual
PASS. The already accepted #381 owner gate is not reopened.
