# Museum Donation Material Admission

## One Execution Chain

Museum donation now uses the controller's existing commitment ledger in both
local and remote candidate generation. `MuseumDonationMaterialReservationGuard`
is a museum-specific wrapper over `MaterialInventoryGraphReader` and
`MaterialSupplyProjection`, not a second material allocator or executor. The
projection is constructed once for a candidate batch, not once per item.

An active reservation for another goal reduces the spendable quantity. A
two-item stack with one reserved item can donate the spare; a fully reserved
stack cannot. Wrong ledger/save/player identity, missing material evidence or
inconsistent item/stack projections fail closed. The native museum fields,
counter interaction, menus and final inventory/reward verifier are unchanged.

The compiler re-evaluates the current ledger and replaces caller-supplied guard
metadata with fresh derived bindings. The existing dispatch-readiness service
then checks ledger ID/revision, active reservation identities, actor and state
before donation. The actual live loop now requests that existing endpoint for
`executor.donate_museum_item`; it does not gain another dispatch authority.

Legacy no-ledger compiler calibration remains representable, but cannot pass
managed dispatch. A plan carrying managed bindings without its ledger is rejected.
Backend runtime compilation continues to load the real repository ledger.

Local Teacher and imported queues are not necessarily registered in the Backend
queue store. The live loop therefore POSTs the exact pending item to the existing
dispatch-readiness route, which evaluates the Backend's current ingested snapshot
and repository ledger with the same readiness service. GET remains available for
stored queues; both handlers share the same evaluation path. POST does not store
an imported queue, recompile it, reselect a candidate or authorize native execution.
It also avoids checking a stored item that differs from the item about to run.
Missing snapshot or mismatched item identity fails closed. This fixes PR #398's
reviewed local-Teacher/imported-queue 404 without bypassing ledger admission.

This guard protects reservations through the dispatch check. It does not create
a durable lease for a newly acquired donation output or prove every subsequent
consumer respects such a lease; full output custody remains a separate gap.

## Teacher And Replay

The existing collection Teacher accepts `--strategy-ledger` when compiling a
managed donation context. Its preference records the retained source path and
SHA-256, and trajectory source references carry that same path. Independent
replay reads the actual file, validates its snapshot identity and rebuilds the
preference/receipt; altered or omitted ledger evidence is rejected.

Managed museum counterfactuals are rechecked with the existing candidate
generator against that ledger, including unique ledger-ID/revision bindings.
Teacher preference and runtime tie-control compilation receive the same ledger.
Legacy unbound calibration sources do not acquire managed status from strings.
None of this turns native success or resource admission into a preference label;
#394's explicit strict-pair admission remains in force.

The live loop stores ledger evidence by exact content SHA-256 under
`live-snapshots/teacher-ledgers`. Identical content reuses a file, changed content
gets another immutable file, and an existing hash path with different bytes is
rejected. This avoids duplicating a full ledger for every iteration.

## Isolation Correction

`LiveTrainingOptions` construction no longer reads historical E-drive pointers.
CLI parsing preserves the legacy resume defaults only when no explicit root,
run/artifact ID, manifest or `--no-manifest` was supplied. Explicit manifest paths
still take precedence intentionally. Library callers with a new root no longer
silently write into a historical run.

A repeated local test exposed this default-path defect before push. Two newly
generated mock-ledger files were placed in a July historical run directory; no
pre-existing save, manifest or training file was overwritten. The mock file
intentionally corrupted by the rejection test was restored to its verified
content hash. Both small generated files remain for reviewed cleanup; nothing
was deleted. The corrected tests explicitly bind a temporary manifest and assert
the evidence path is under their temporary root.

## Verification

- Local museum/admission focused tests: `43/43`; Backend complete: `279/279`.
- The live dispatch method is exercised against the real Backend HTTP handlers
  with unregistered museum/crafting items. Current-ledger drift still blocks;
  posted state/actor/binding/revision changes cannot use a stored positive copy.
  These are in-process HTTP regressions, not another native game trajectory.
- The existing focused independent Teacher self-test verifies retained-ledger
  propagation, valid replay and rejection of changed/missing ledger artifacts.
- Hidden/silent E-drive native run
  `museum-reservation-2e24b81b-20261103` completed seven selected queues and nine
  verified actions: native donation `59 -> 60`, normal committed night
  `472 -> 473`, Farm event `66`, actual Rusty Key. Source save tree SHA remained
  `93153ed126876c240d963a484ac6bb6c880e16b4999c26c5502271721f2be79e`.
- Summary:
  `I:\StardewAITraining\museum-reservation\museum-reservation-2e24b81b-20261103\summary.json`,
  SHA-256 `e0906a0dab9f721b176e86aaa6f124f83ccdd803615938ffe1d23ae630183b6c`.
  Offline receipt checks passed `34`; processes and ports were reclaimed.

The native case proves the empty-ledger managed positive path on candidate
`2e24b81b`, not live contested-inventory execution, visual frame rate, new Teacher
production partitions or the later Teacher-source changes. Reservation conflicts
are code-level regressions; Teacher ledger source propagation has hermetic replay
and endpoint-method tests. These evidence scopes are not concatenated into one
new native trajectory. The boundary save is Year 5, so it does not prove the
pre-Year-3 deadline or acquisition of all 60 items. No formal training occurred.

## Remaining Rusty Key Work

Produce real rolling supply/donation proposals from the complete authoritative
undonated-item context. Use safe held items first; otherwise reuse existing
acquisition routes. Preserve the distinction between a complete threshold supply
certificate and a valid next rolling step: missing a full 60-item same-day
portfolio must not itself prohibit legal partial progress. Continue through the
same compiler/executor, not an alternative donation macro.

Then close acquired-output custody until donation, joint timing/opening windows,
the following-morning key handoff and this criterion's independent Teacher/native
partitions with actual deadline evidence. Coverage counts and the formal launcher
remain locked; frozen Pet Love/Skull Key and unrelated roots are not reopened.
