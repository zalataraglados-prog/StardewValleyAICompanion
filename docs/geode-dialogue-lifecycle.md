# Geode Native Dialogue Lifecycle

## Retained Native Finding

PR #412 (`8226ae8c`, merged as `c4280406`) passed exact-head CI
`41740544534` (Core 1112, Backend 381). Its isolated native run
`rk-8226ae8c-1104` verified a real `(O)535` crack producing `(O)549`, canonical
input settlement and an active one-unit output custody row at ledger revision 2.
The new run's initial ledger was revision 0. Two subsequent verified connector
traversals reached ArchaeologyHouse, but no donation happened.

All three post-action observations already had `dialogue_up=true`, no active
menu/event and `player_can_move_now=false`. The first occurrence was immediately
after cracking, not an unhandled museum story. The original custody protection
correctly stopped with `museum_menu_or_dialogue_not_clear` and
`rusty_key_owned_supply_pending_donation_or_transfer`; it did not reacquire,
discard the claim or claim a completed criterion.

The original and copied ProofFarm baseline hashes both remained
`68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d`.
Owned game processes/ports were released. Raw traces remain local, not in Git.

## Native Rule And Shared Fix

The locked 1.6.15 decompile was checked directly:
`DialogueBox.receiveLeftClick` waits for its text/safety transition, clears
`Game1.dialogueUp` for a question, and only then calls
`GameLocation.answerDialogue`. `GameLocation.answerDialogue` itself does not
perform that UI lifecycle. The original geode executor called the latter directly
while the Blacksmith question was open, leaving stale dialogue/control state.

The existing ordinary response executor's hover/button-click sequence is now
one shared helper used by both that executor and the existing geode executor.
Question key/response whitelist ownership remains with each original caller.
The helper checks the actual menu, response key, transition/safety/button state
and selected hover before invoking `DialogueBox.receiveLeftClick`. It does not
take the OS mouse or assign native dialogue/control flags.

The geode state machine waits across native text-reveal clicks and requires the
actual GeodeMenu with dialogue closed before advancing. After the original
native OK-button close, output conservation alone is insufficient: menu,
dialogue and event must be closed, the player movable and not using a tool.
The result retains dialogue/control diagnostics. No native prediction, output,
money, counters, reservation or Teacher behavior is fabricated or loosened.

## Verification And Next Boundary

Focused game-free geode/source/API tests pass 37 cases. The existing geode
mainline and source tests are now included in the game-free CI compile list;
their previous absence there was a coverage gap. Native input/lifecycle wiring
is checked by source guards, not presented as an actual game run.

The final clean version must still run the original isolated current-geode
chain and independently verify donation, exact custody consumption, normal
night/key handoff and source preservation. The prior 8226 trace proves only its
own partial acquisition/custody/navigation and cannot be relabelled as this
fix's complete native proof. The 59-item fixture, even in Year 1, is not a
new-save collection/deadline/Teacher-partition certificate. Unknown artifact
route parsing and the previously listed relocation/joint-budget/criterion
integration gaps remain; frozen coverage and formal-training flags do not change.

## First-Pickup Continuation

The first `ff77caad` run passed CI but its stricter native completion correctly
stopped at an actual non-question first-geode discovery message, not another
ghost menu. Direct decompile confirms `Farmer` queues `geodeFound` plus
`holdUpItemThenMessage` when `(O)535` is received. Returning the held remainder
to inventory can trigger this on a legitimate first pickup.

The bridge now observes `geode_found_mail` and projects that exact native flag
for either a returned `(O)535` remainder or `(O)535` output. The flag is bound
through candidate/compiler/request/live validation; missing context is not
treated as false. Pre-close conservation may wait for that one explicitly
predicted deferred flag; final completion still requires the exact full mail
delta. Other missing or extra mail remains a mismatch.

Only the exact native geode hold-up text (obtained from the native item method),
no speaker/question/responses/event, observed native flag and matching inventory
receipt can delegate to the existing `StartDialogueAdvance` engine. That engine
keeps its native press/release/safety/timeout behavior; no second dialogue engine
or synthetic flag/menu mutation is introduced. An unexpected prompt remains
blocked. Parent timeout cancels only its own child advance. The final receipt
records native flag before/after and whether the tutorial was acknowledged.

The single-player native clock rule was also checked: an ordinary menu pauses
time, and a non-tool hold-up pauses it unless the native farmer forces time.
This is not a new multiplayer/joint-budget proof. The final candidate still
needs a new same-version native run; the earlier failed run is retained and
must not be rewritten as a pass.

The `5e1f3f9b` native attempt verified the new mail side effect but stopped while
the reused ordinary dialogue engine failed to finish the confirmation. The
parent now retains its child's actual failure reasons. For this exactly bound
internal tutorial only, the same existing advance state machine can issue the
native menu click directly, as the question-response path already does, instead
of depending on a physical button override during a composite operation.
Readiness, typing, instance/speaker checks and timeouts remain the original
engine's; ordinary callers retain their previous input backend. This switch is
private execution state, not a caller/model permission to skip unsafe prompts.

## Latest Native Boundary

The clean `45645d96` run `rk-45645d96-1104` verified the exact first-geode
tutorial acknowledgement, `geodeFound`, menu/dialogue closure and movable
player, with money 975 and cracked counter 43. It acquired `(O)549`, settled
the input, retained one output in canonical custody, reached the museum and
donated the 60th item with native `museum60` and prerequisite event 295672.
The donation then consumed the custody through the original settlement hook.

The run did not complete the key/night handoff: after the first connector
home, the original recovery producer offered its already implemented
`recovery:wait_for_native_control_settle` while native `CanMove` recovered.
The script incorrectly accepted only `recovery:native_save_boundary`. Both
Rusty Key test entry points now share one exact candidate selector accepting
that existing bounded wait or the normal boundary, never unrelated waits,
unavailable candidates or ambiguous choices. Each still compiles via the
original daily-plan path and consumes a step of the existing finite budget,
then observes and replans. No native control flag is forced.

This is actual acquisition/custody/donation evidence, not a completed
key criterion, full collection route, Teacher comparison or training approval.
The failed run remains retained; the final script version needs a fresh run.
The shared handoff selector/evidence tests pass 41 cases; the actual source
pipeline plus bounded orchestration checks pass 15, including both handoff
candidates through the existing adapter. Fresh native-calendar fixtures pass
18 checks. These checks do not launch the game or certify the night/key.

The subsequent clean `8fe21913` run reached the same verified acquisition,
donation and custody consumption. Its existing 30-tick native-control wait
also applied and verified, and the fresh observation restored `CanMove`.
The CLI correctly kept `verified_actions=0` because observation is not productive
work; the adapter's unconditional one-work-action target then caused exit 2
(`primary_training_target_not_met`). An initial zero-target script correction
was itself rejected by the original queue-receipt startup validator in
`18b59881`; no waiting action ran in that attempt. That was an adapter mistake,
not a new native executor failure. The correction now retains the required
one-action target and recognizes exit 2 only for the exact verified/completed
native-control observation with a fresh snapshot and a report bound to the
same run/queue/before hash, exactly one attempt, zero work actions and that exact
stop reason. Other nonzero exits, report drift and non-observation target failures
remain failures. This changes orchestration only, not native runtime binaries.
The regression covers receipt mismatch, incomplete observation, wrong action,
bound-report drift and unrelated exits; retained real wait artifacts can also
be replayed through the adapter without modifying those originals or a game.
All 30 game-free runtime cases pass, plus one adapter replay using the retained
real `8fe21913` wait receipt and CLI report (31 locally). That replay verifies
this adapter decision, not another native game action or a completed key chain.
A script-only continuation
may reuse the clean `8fe21913` bundle only after proving no compiled-source or
build-configuration diff and rechecking every retained binary hash/version;
the script commit and binary-source commit must remain separately attributable.

## Morning Handoff

`40dad9f6` verified 13 continuous steps through actual acquisition/custody,
donation/consumption, returning home, bounded post-connector waits and normal
native sleep/save to Spring 8. Its night transaction observed native commit,
expected day and safe readiness but remained `ready_for_safe_next_day_action`;
it was not complete and the key was not received.

At 06:00 the script requested the Farm connector before checking control
settling. The existing live safety layer correctly executed its bounded wait
instead, and the strict adapter rejected the request/receipt mismatch. The
handoff now queries the same recovery producer first during this morning path.
Only its exact available native-control wait can take priority. If absent,
the original Farm connector is selected; a sleep candidate from that query
must never consume another day. The selector, native executor, per-step fresh
replan and finite overall budget are unchanged.

The default game-free regression now passes 34 cases; local replay of the real
morning receipt/report adds one (35). Every mocked adapter invocation also runs
the actual `LiveTrainingOptions.Parse`, including its rejection of a zero work
target. Expected mocked failure exits are reset only in test cleanup; production
failure handling is unchanged. This fixes CI's red host exit after all 30 earlier
checks had passed. The final continuous night/key native trace is still required.
