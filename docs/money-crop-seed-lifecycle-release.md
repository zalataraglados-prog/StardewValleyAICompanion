# Owned Seed Lifecycle Release

## Accepted Parent

PR514 merged as a3869f978093107b0290e4eb8af640142918e8a4. Final-head CI
42404047790 passed42/42 steps, Core2442/Backend947 without failures or skips,
and tested/main trees matched. Automatic review covered first head af9cb070;
the final initial-clock delta was controller-reviewed and specifically tested.
Do not misattribute the earlier automatic review to final head b3f49044.

Owned planting native-format effects, partial consumption and original durable
settlement are accepted. This is their existing lifecycle-release successor,
not another executor, resource allocator or goal/Teacher acceptance mechanism.

## Existing APIs, Not New Business Rules

The original CropCommitmentLedgerService.Cancel changes the selected crop, not
its resource reservations. Original ReservationPortfolioLedgerService.Commit
releases explicit IDs via original material/currency Cancel methods. Purchase
custody already releases residual purchase cash once enough owned seeds arrive;
that behavior is not reimplemented here.

FileStrategyCommitmentRepository.Get already reconciles, in order, crops,
machine relocations and machine support intents, then saves only a changed
revision. This exact composition is extracted to StrategyCommitmentReadReconciler
for independent readback replay. The repository lock/load/save/cache frame,
three original services, completion rules, order and history are unchanged.
Crop cancellation/completion services are not rewritten to fit this consumer.

The original crop completion rule is LastInSeasonHarvestTotalDay elapsed.
That is calendar closure, not proof of native harvest, target revenue, Teacher
success or a training label. Finishing one seed claim or planting batch does
not close a still-growing crop. Existing native lifecycle/terminal evidence
and L1 acceptance remain separate and unchanged.

## Bound Input And Explicit Cancellation

CropTeacherSeedLifecycleSupport.Open accepts the original active crop's current
snapshot and full ledger files, commitment ID and a new release step ID.
Native snapshot/actor/ledger identity, exact source and all physical input hashes
are checked. Pending prepared material transfer is rejected, never reset.
This scope must originate at the runtime owner's last verified phase; it does
not authorize releasing resources around an unknown native action/journal.

BeginCancellation requires an explicit reason and returns the original cancel
request. VerifyReply independently replays the complete original result at its
actual UpdatedAt before exposing any release request. A lost cancellation reply
can be recovered only from the unique exact original cancel history event,
complete cancel replay and original read-reconciliation replay against durable
readback. Recovery makes no request and cannot resend cancellation.

Completion instead uses VerifyCompletionReadback on the original repository's
actual Get result. It replays the entire original reconciliation, not merely a
completed flag or a copied hash. A still-growing crop is excluded. Retain the
original scope inputs across interruptions; do not replace its active base
with an already-closed ledger and blindly invoke cancellation again.

## Exact Release And Readback

Only active crop_seed_supply:<commitment> material claims with the crop's exact
source owner, earn_money goal and seed identity are selected. Seed claims cannot
carry output custody. Cash additionally binds the existing exact purchase-money
ID, purpose, owner, goal and currency. Foreign/malformed matching claims reject
upstream; other roots, another crop of the same seed and unrelated claims are
not swept into the release. IDs are sorted and requests/results are detached.

If no active owned allocation remains, RequiresRelease is false and Request
refuses to create an empty mutation. Otherwise the existing portfolio commit
releases the exact IDs. VerifyReply uses the original complete mutation evidence
verifier; VerifyReadback requires that verified result first, then independently
replays only the original Get side effects against the entire durable ledger.
Unknown foreign-root progress cannot pass as a source refresh.

Lost portfolio replies must use the existing journal/lookup pipeline before a
reconciling Get, not an inferred successful release or the cancellation fallback.
The original lookup deliberately bypasses Get/Load and does not mutate cache.
The kernel does not call HTTP, create a second
journal, clear pending transfers, alter inventory/money or issue native actions.

## Verification And Continuation

Tests use the accepted seed fixture, original file repository, actual cancellation,
portfolio and latest-ledger HTTP endpoints. Cases cover explicit cancellation,
original expiry, no-op release, lost cancel reply, foreign owner/item/purpose,
pending transfer/stale source, exact replies/readbacks and physical source drift.
The Core composition case verifies the original three-service order/history and
input immutability. These are synthetic protocol checks, not new game evidence.
There are31 new Backend cases and one new Core composition case. Initial local
Core2443 passed2442 existing cases and failed the new expected operation-name
literal; the actual original name includes intent. After fixing only that
assertion, the entire ledger group passed11/11. Initial Backend976 passed974;
one old Museum timeout and a new fixture mixing pre/post-restart repository
caches failed. The fixture now keeps using its restarted instance; no legacy
cache/timeout semantics changed. Final lifecycle31 plus the full old Museum14
passed45/45. All failed logs remain external; do not relabel them as green full
local runs. Complete final-head counts belong to CI.
Final local/CI/review/tree results are recorded at PR closeout. Full hermetic
verification runs on final-head CI, not repeatedly on this computer.

Next: connect planting and these lifecycle results to the unique existing
CropTeacherSeedRuntimeTransaction/managed runtime owner and original durable
intent/journal/lookup/base-marker-final readback flow. Verify each actual prefix
before the next fresh-state dispatch without weakening old source gates or
pretending the stale batch source is current. Preserve the original mechanical
batch rather than splitting it into single-seed queues. Exclude unsupported
unboosted-profile modifiers upstream before mutation, and apply bounded prefix
verification optimizations before the native sample.

Automatic managed activation remains off until connected; then collect a small
explicitly versioned native boundary sample, not accepted13-day/L1 evidence again.
Continue the existing joint calendars/budgets/continuity, Provider/model IO and
formal manifest/preflight afterward. L1 stays19/19 and11/11, formal=false;
fresh-save Student21/21 is post-training evaluation. No game, server, training,
worker or model API is started here. No local-data/raw artifacts are committed.
