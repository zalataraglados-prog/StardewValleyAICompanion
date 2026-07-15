# Risk

- Native runtime social executor (`executor.social_interact`) is implemented in the RuntimeTestHarness and validated by 27 source-guard tests. Blocked results record precise failure reasons and use `executor_calibration` scope, so runtime failures do not affect strategy values.
- Live social query and gift taste rows are available only for proven vanilla runtime methods; modded/overridden NPCs fail closed per row.
- Current route handling only checks same-location snapshot facts and current collision-grid reachability to an adjacent stand tile; no future schedule or cross-map route windows are emitted.
- The only state-changing social call is `Game1.currentLocation.checkAction`; no direct `NPC.checkAction`, `tryToReceiveActiveObject`, `receiveGift`, friendship/counter/inventory/NPC-position mutation is made by the executor.
- Controller-run main-repo validation on 2026-07-14: focused social tests 163/163, final Core 462/462, Backend 49/49, RuntimeTestHarness build 0 warnings/0 errors. No game was launched.
- Status is **static-ready, not live-runtime-proven**. Remaining social closure is isolated E: runtime integration: talk smoke, ordinary gift smoke including one-item-to-null, blocked/replan cases, output artifact audit, then duration calibration.
- Do not claim complete-human behavior or whole-project transparency from this module.
- Pre-existing `TASK.md` whitespace diff remains untouched and should not be included in the task commit.
