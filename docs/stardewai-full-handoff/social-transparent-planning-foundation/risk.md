# Risk

- Native runtime social executor (`executor.social_interact`) is implemented in the RuntimeTestHarness and validated by 27 source-guard tests. Blocked results record precise failure reasons and use `executor_calibration` scope, so runtime failures do not affect strategy values.
- Live social query and gift taste rows are available only for proven vanilla runtime methods; modded/overridden NPCs fail closed per row.
- Current route handling only checks same-location snapshot facts and current collision-grid reachability to an adjacent stand tile; no future schedule or cross-map route windows are emitted.
- The only state-changing social call is `Game1.currentLocation.checkAction`; no direct `NPC.checkAction`, `tryToReceiveActiveObject`, `receiveGift`, friendship/counter/inventory/NPC-position mutation is made by the executor.
- Controller-run main-repo validation on 2026-07-15 includes hidden/silent isolated E: runtime PASS at `artifacts/runtime-native-social-smoke/runtime-native-social-smoke-20260715-183304/summary.json`: talk, ordinary DialogueBox advance/close, and gift each produced one applied/verified execution and one training row. Gift `(O)388` changed stack `4 -> 3`, daily/weekly counters `0 -> 1`, and friendship `66 -> 46`.
- Status is **live-runtime-proven for the ordinary talk -> safe dialogue close -> stacked gift path**. Remaining social closure is one-item-to-null gift, blocked/replan cases, duration calibration, and explicit modded/overridden NPC fail-closed runtime cases.
- Do not claim complete-human behavior or whole-project transparency from this module.
- Pre-existing `TASK.md` whitespace diff remains untouched and should not be included in the task commit.
