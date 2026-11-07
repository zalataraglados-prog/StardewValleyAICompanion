# Owned Seed Lifecycle Runtime

## Accepted Parent And Scope

PR516 merged as c53115f0a1ddfef1e025696b04eed179ee637119. Final-head CI
42412481709 passed all42 steps, Core2443/Backend1010 with no failures or skips,
and tested/main trees matched. Automatic review covered first production head
7af791ad; the final source-guard-only change was controller-reviewed, passed
the original15 guards and final-head CI. Do not attribute that review to bff3f82a.

Owned planting already uses the original shared runtime transaction, maximum
eight-tile batch, exact owned slot, verified fresh-state prefixes and original
settlement. This successor connects the accepted lifecycle kernel to actual
HTTP, durable journals and complete readbacks. It is not another executor,
resource allocator, closure rule or automatic managed activation.

## Explicit Entry

The LiveTrainingLoop command is close-crop-seed-reservations. Required arguments,
each exactly once, are --snapshot, --ledger, --commitment-id, --release-step-id,
--run-id, --mode, --backend-url and --output-dir. Mode is calendar_completion
or explicit_cancellation; only cancellation accepts/requires --reason.
Backend URL must be an HTTP(S) origin without credentials, path, query or fragment.
Unknown, duplicate and incomplete arguments reject before side effects.
The command returns before generic training setup and does not create a dataset.

Inputs are the active crop's last verified immutable source/full ledger, not an
arbitrary latest state around unknown native work. The existing lifecycle scope
validates all owned claims and rejects pending material transfers. The output
must be new; the original create-new, write-through, flush-to-disk writer claims
lifecycle-intent.json before any HTTP. Only its winning writer owns the summary.
The intent binds source hash, physical input hashes, revision, run, mode, exact
crop/release IDs and typed cancellation request. Physical inputs are checked
again before cancellation or the first reconciling calendar read.

## Original Closure And Recovery Ordering

Explicit cancellation posts the original request once, without a preliminary
Get: the original latest-ledger Get can reconcile and change revision. The raw
cancel request and SHA-bound journal are durably written first. A successful full
reply must replay through CropTeacherSeedCancellation.VerifyReply; known reply
drift never falls back to a successful recovery read.

Only a lost/invalid transport reply permits original readback recovery. The
unchanged regular journal/request files, size budget and hashes are checked,
then the original kernel requires a unique exact cancel-history event and full
original cancellation plus read-reconciliation replay. No cancellation resend.
Explicit4xx rejection does not trigger that reconciling Get. A missing original
event, changed journal or unrelated progress keeps the operation incomplete.

Calendar mode intentionally calls the original Get and independently replays
all original crop/relocation/machine-support coordination. A still-active crop
cannot be treated as cancelled or completed. Calendar expiry is not native
harvest, target revenue, Teacher success or a training label.

## Original Resource Release

The verified full closure ledger is saved before releasing anything. Existing
CropTeacherSeedRelease selects only this crop's exact active owned seed/cash
claims. Its original portfolio commit, journal and lookup are reused. Lost
release replies require original lookup BEFORE any Get; a missing lookup never
falls back to cancellation-style recovery or resend. No resources means no
empty commit. Full replies and the final persisted readback are independently
verified, including every other root and original Get side effect.

Evidence includes lifecycle-intent.json, optional crop-cancel-request/journal/
reply/failure files, verified-closure-ledger.json, optional release-reply.json,
original portfolio journals under live-snapshots, original immutable ledgers
under teacher-ledgers, and lifecycle-summary.json. The summary cannot mask a
primary failure. Verified closure/release does not authorize native execution,
native outcomes or formal product training: those flags stay false.

An existing output is never silently resumed by invoking this command again.
Keep its original inputs and journals for the unique managed recovery owner;
do not create a new run/output to bypass an unknown previous mutation. This
slice handles in-process lost replies, not cross-process phase ownership.

## Verification And Unique Next Work

Tests use the real original HTTP endpoints, original file repository and journal
helpers. They cover normal calendar/cancel paths, lost replies and exact lookup
ordering, original foreign-crop expiry, no resources, physical input/journal
drift, incomplete/ambiguous CLI inputs, concurrent intent ownership, revision
conflict, malformed reply recovery and full reply/final readback tampering.
The related lifecycle/planting/transfer/purchase group passed240/240 before the
final calendar pre-read guard; final new runtime cases passed32/32, zero skips.
PR review identified a release callback retaining an unnormalized trailing-slash
origin. Both closure modes reproduced the original404 before the fix. The new
wrapper now shares runtime origin normalization with its detached portfolio
options, preserving the original caller's configuration and journal directory.
Both tests also exercise lost-release lookup. Final runtime cases passed34/34;
the intervening reflected synchronous-exception mismatch was fixed by preserving
the wrapper's asynchronous failure boundary, not changing the expected guard.
Complete final-head CI results belong to PR closeout; these synthetic protocol
tests are not new native gameplay evidence.

The [single scope-selection boundary](money-crop-managed-seed-scopes.md) is the
immediate successor. Then finish the unique retained-before,
partial/pending and crash-recovery owner across purchase/transfer/planting and
lifecycle. Reuse original transactions/journals/proofs; never blindly resend
native work or weaken old source gates. Only after all paths connect, opt in
through an explicit versioned managed entry and collect a small native boundary
sample, not the accepted13-day/L1 evidence again. Then original joint calendars,
aggregate budgets/cross-root continuity, Provider/model IO and manifest/preflight.
L1 stays19/19 and11/11, formal=false. Student fresh-save21/21 is post-training.
No game, server, worker, model API or training starts here; no local-data/raw
artifacts are committed.
