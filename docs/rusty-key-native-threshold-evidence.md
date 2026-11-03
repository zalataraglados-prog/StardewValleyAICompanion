# Rusty Key Native Threshold Evidence

## Scope

The 2026-11-03 run proves a shared native capability, not completion of the
`obtain_rusty_key` root. The baseline is an isolated Year 5 save with a debug
boundary of 59 donations, item `(O)96` in slot 11 and a Town museum approach.
Opening-hour setup and museum setup finish before the measured native chain.
There is no direct key, event or location setup after that boundary.

It does **not** prove acquisition of 60 distinct safe items, the Year 3 deadline,
Teacher comparison/native-outcome partitions, policy permission to donate, or
formal training admission. Existing confirmation and autonomous allowlist gates
remain unchanged. Previously frozen Pet Love and Skull Key evidence is untouched.

## Captured Run

- Candidate commit: `ba9bb50e9b1a2a84e13f4da3e5ba1015307cf5a8`.
- Root: `I:\StardewAITraining\rusty-key-threshold\rusty-key-threshold-20261103-190522`.
- Summary SHA-256: `226e5600f21433f7a178190373a29379c81878f97c0df937f2cdf942b3d5f1ea`.
- Source save tree SHA-256 before/after:
  `93153ed126876c240d963a484ac6bb6c880e16b4999c26c5502271721f2be79e`.
- Exact isolated Mods: TransparentBridge and RuntimeTestHarness. Their bundle
  hashes and Backend/LiveTrainingLoop binary hashes are in the local summary.
- Hidden, silent, render suppressed, one game, no training or worker calls.
- All game/backend processes and ports were reclaimed after completion.

The seven selected queues produced nine applied/verified native steps:

| Queue | Native Result |
| --- | --- |
| 00 | Town connector and donation: `59 -> 60`, one inventory item consumed, `museum60` and host event `295672` applied; actual key remains false |
| 01 | Museum exit to Town |
| 02 | Town to BusStop |
| 03 | BusStop to Farm |
| 04 | Farm to owned FarmHouse |
| 05 | Native bed/sleep, SaveGameMenu/save commit, total day `472 -> 473`, next morning `06:00` |
| 06 | Normal FarmHouse exit to Farm, followed by the existing runtime story-priority interrupt completing native event `66`; actual key and event-seen state true, story inactive |

No separate donation, travel, sleep or story executor was introduced.
The native event receipt records `native_event_input_only`,
`boundary=event_completed`, `skipEvent_not_used`, event ID `66 -> empty` and
event-seen `false -> true`. The normal next-day connector completes the same
end-of-night transaction. The source baseline is preserved by directory hashes.

## Verification And Reuse

`scripts/Invoke-RuntimeRustyKeyThresholdSmoke.ps1` composes existing shared runtime
evidence helpers, candidates, DailyPlan and native executors. Its local summary
is not itself a production Teacher adapter or an independent admission certificate.

`scripts/Test-RuntimeRustyKeyEvidence.ps1` checks positive and negative evidence
semantics without a game. With `-CapturedRunDirectory` it revalidates this run's
stored native receipts and before/after snapshots. Compressed snapshots must be
read through `RuntimeLogicalJson.ps1` and the existing C# content-addressed reader;
the JSON manifest must never be mistaken for the materialized snapshot.

The enhanced offline check passes for the original capture without relaunching a
game. All raw run data stays outside Git; only implementation, tests and this
index are committed. A future criterion adapter must bind its own exact
authority, save/player, start-state, deadline and partition identities before it
may consume a local result for a broader claim.

## Next Gap

Keep WIP=1 on Rusty Key. Reuse the museum authority, acquisition portfolio and
reservation ledger to satisfy only the live donation deficit with distinct
undonated items, preserving task/crafting/shipment and other active commitments.
Then close the criterion's independent Teacher/native partitions and deadline.
Do not promote it to the complete 95-item Museum goal, restart unrelated roots,
or attach Teacher-global/new-save completion as a prerequisite to this local
native capability.
