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
