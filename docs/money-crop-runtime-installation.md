# Crop Runtime Reservation Installation

## Scope

PR505 merged as `4405fffb` after exact-head CI `42367028701`, completed review
without findings, and tested/main tree equality. It passes the complete existing
ledger into the original crop Teacher. This successor installs a newly selected
crop and its resource portfolio through the original runtime endpoints, without
starting gameplay, collecting native evidence or authorizing training.

The original PR504 resource receipt remains an immutable **planning replay** with
`runtime_ledger_installed=false`. Do not rewrite it or replace its historical
timestamps with backend timestamps. Actual installation has a separate summary.

## Original Contracts

`CropTeacherRuntimeInstallation.Open` independently rebuilds the original v2
Teacher decision, crop receipt and resource request once. The original resource
builder now shares that verified decision with the installer; its public output
and legacy semantics are unchanged. Installation requires a complete,
source-anchored base ledger without an active crop. Existing active crops must
continue their original complete commitment/layout instead of using this entry.

The installer checks the exact backend base ledger, then posts the original crop
upsert at revision k and the original resource portfolio at k+1. It never assigns
a source hash or edits claims directly. Crop results are independently replayed
through `CropCommitmentLedgerService.Upsert` using the actual history timestamp.
Resource results use the original `ReservationPortfolioCommitEvidenceVerifier`
against that actual crop ledger. Full ledger/history equality preserves every
other root's claims; matching IDs, revisions or root hashes alone are insufficient.
The complete final ledger is read back and compared after commit.

Original source, layout, Teacher request, decision and base-ledger files are
hash-checked before mutations and verification. Callers receive copies of owned
requests/base state, not mutable access to the canonical replay context.

## Entry

Use the existing LiveTrainingLoop executable, not the normal automatic loop:

```powershell
dotnet tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll install-crop-reservations --decision <original-v2-decision.json> --backend-url http://127.0.0.1:5108 --output-dir <new-isolated-evidence-directory>
```

The backend must already contain the exact source snapshot and original complete
base ledger. Use only the isolated experiment backend; this is a ledger mutation,
not a read-only command. The output directory must not exist. Existing evidence
is never overwritten; reusing a decision under a new directory still fails the
exact base revision/ledger check before another mutation.

The command branches before normal loop/options/training setup. It does not call
the bridge, executor, game launcher, training endpoint or model provider. The
normal automatic dispatch now refuses active material/currency claims before
building executable crop artifacts until owned-budget purchase and custody
settlement are connected; unreserved legacy dispatch keeps its original behavior.
This is a temporary consumer boundary, not completion of managed execution.
The original buyer reads native shop balances;
it does not itself consume this installed portfolio or establish acquired-slot
custody. A purchase compiling successfully would not prove correct reservation
settlement. Installing funds and then running that unmanaged continuation is
**not** a supported managed execution path; do not change the old buyer to fit
this new experiment.

The original `CropTeacherRunPlanner` also checks total inventory rather than this
plan's owned seeds. For example, a native stack of three with two reserved for a
different root supplies only one usable seed, not three. A two-tile plan still
needs one purchase. The managed successor must carry that ownership into the
existing acquisition/planting consumer; do not loosen reservations, silently skip
purchase or change the old generic inventory-count rule to suit this experiment.

## Durable Requests And Failure

The exact crop request and endpoint/hash journal are flushed before POST. An
ambiguous reply triggers at most one same-source ledger readback; journal bytes
are rechecked, then the original upsert is independently replayed with exactly
one revision. No fabricated crop API reply and no second upsert are produced.
Missing, intervening or altered ledgers fail. The same-source day cannot complete
this newly selected crop during the repository's normal reconciliation.

Resource commit reuses the original durable portfolio journal and exact-request
lookup. A lost reply performs lookup, not a second commit. HTTP 4xx does not
trigger recovery. An unsuccessful installation keeps its intent, original
requests, failure and observed ledgers, and reports `installation_incomplete`.
It does not cancel or silently roll back an accepted crop if resource installation
fails. Do not blindly retry; use the retained exact evidence for recovery.

Only after both strong verifications and final readback does the separate
`installation-summary.json` report `runtime_ledger_installed=true`.
`execution_authorized`, `native_outcome_verified` and
`formal_product_training_authorized` remain false. These artifacts prove a
backend ledger roundtrip, not seed acquisition, production or a fresh-save score.

## Remaining Mainline

Next connect the installed portfolio to the original supporting-transition
ownership/settlement machinery before activating managed gameplay: neutral routes
preserve/rebind claims, one-unit purchases consume the exact owned currency, real
acquired slots gain custody, existing seeds are not reserved twice, and selected
chest supply uses original transfer/claim relocation. Keep the old initializer's
full-seed guard and exact original receipt requirement; version/opt in the managed
input path instead of replacing old evidence.

Then complete original daily aggregate budgets/calendar, selected-source and
cross-root continuity, Provider/model selection, formal manifest and preflight.
L1 remains `19/19`, `11/11 roots`; formal training stays false. Student fresh-save
21/21 remains a post-training evaluation. No repeated local-source sampling is
required by this installation work.

## Validation

20 new Backend checks cover real file-repository reload, unchanged other-root
claims, exact selected quantities/cost, actual versus planning timestamps,
independent input hashes, no evidence overwrite, automatic unmanaged-dispatch
guard, rejection/tampering and no mutation replay. Two checks use the original
ASP.NET HTTP endpoints, including loss of both accepted replies; the other
transport failures are scripted. No native game is started by these checks.

Local complete Core `2442/2442`, Backend `669/669`, original PowerShell portfolio
recovery `19/19`, and final LiveTrainingLoop Release build (zero warnings/errors)
pass. The actual CLI rejects incomplete arguments before normal loop/training
setup. The original full hermetic also passes. Exact-head CI is recorded at PR
closeout; no raw evidence, test output or local-data is staged.
