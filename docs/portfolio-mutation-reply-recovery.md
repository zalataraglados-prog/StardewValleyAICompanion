# Portfolio Mutation Reply Recovery

## Scope And Authority

This completes the Backend reply-loss boundary left by the Product recovery
slice. It covers the existing `commit`, `settle-completed-route` and
`settle-supporting-transition` portfolio operations. It does not create another
ledger, executor, planner or native receipt, and does not authorize replay.

The normal latest-ledger API is deliberately not used for recovery: `Get` can
reconcile completed commitments and write a newer revision. A current inventory,
history marker or guessed delta is not the original accepted RPC result.

## Atomic Compact Record

The original file repository now publishes its accepted portfolio reply and
ledger together, using a create-new, write-through temporary file, `Flush(true)`
and the existing atomic rename. The cache changes only after publication.
The ledger's public DTO/schema remains unchanged. One private
`_last_portfolio_reply` field holds the operation, original state, normalized
typed-request SHA-256, ledger revision/hash and compact accepted reply/hash.
The reply does not duplicate its `ledger` child: there is exactly one current
ledger, not a growing collection of full historical ledgers or snapshots.

Only accepted portfolio mutations replace that last record. Rejected mutations
leave the file unchanged. Other ledger writes preserve the record, but a changed
revision makes its old reply `superseded`. A later portfolio request replaces
the record and makes an earlier lookup conflict. The sequential controller stops
on the first unknown mutation, so one last reply per save/player is sufficient
for this scope. This is not a general concurrent or historical replay service.

Existing ledgers without this optional field still load normally. Lookup reports
`not_found`; it never invents a receipt from their history. Current repository
writes and reads retain the metadata across repository restart. Multiple
independent writers to the same ledger directory remain unsupported, as before.

## Read-Only Lookup

`POST /api/v1/strategy/commitments/reservation-portfolios/lookup-mutation` accepts:

```json
{"operation":"commit","request":{"state_hash":"original-state","expected_ledger_revision":0}}
```

`request` must be the complete original request, not the abbreviated example.
The operation selects the same typed request contract as the mutation endpoint;
hashing uses its canonical serialization, not incidental JSON spacing or order.
The original state must already be in `StateStore`, supplying its save/player
identity. After a Backend process restart, ingest the preserved original snapshot
through the existing snapshot API before lookup; no new game snapshot is needed.
Missing original evidence remains blocked rather than guessed.

Lookup reads the published ledger file directly under the existing repository
lock. It does not call `Get`/reconciliation, populate the cache, append an audit
event, mutate a ledger, capture a snapshot or invoke native execution. Identity,
schema, request, revision and ledger/reply hashes must match. A matching record
rehydrates its actual compact reply with the matching current ledger.

Statuses are `found`, `not_found`, `conflict`, `superseded`, `corrupt`, `rejected`
or `unavailable`. Every wrapper is observation-only: execution, ledger mutation,
native verification and formal training are false; independent verification is
required. A retrieved accepted mutation is not a verified game outcome.

## Original Client Continuation

The existing Rusty Key mutation call now delegates to one shared transport helper.
Before POST it create-new writes and flushes `<name>-rpc-request.json` and
`<name>-rpc-journal.json`, binding the exact wire request hash, operation and
Backend URL. Existing journals refuse replay/overwrite. No whole snapshot is
generated or copied. On RPC failure it retains the failure and performs at most
one 15-second lookup using that unchanged request. Hash/URL/schema drift blocks
the lookup before any request is sent.

A matching `found` wrapper is saved separately. Its reply and ledger are
extracted as raw JSON, retaining timestamps and integer precision rather than
round-tripping through PowerShell. The caller then runs the same canonical C#
commit/settlement receipt builders and independent replay checks as a normal
reply. No verification, custody release or training label is awarded by lookup.
All other statuses or failed lookup retain the journal and stop: no mutation
resend and no native resend. Explicit application rejection also stays rejected:
the shared HTTP helper preserves status/body/cause; HTTP 4xx stops without lookup,
whereas an unknown transport failure or HTTP 5xx may retrieve an already committed
reply. A revision-conflict response cannot be converted into success by lookup.

For an interrupted operator session, dot-source the existing evidence helpers
and `scripts/lib/RuntimePortfolioMutationRecovery.ps1`, then call
`Get-RuntimePortfolioMutationReply` with the original Backend context, journal
and a new observation output path. This is read-only retrieval, not automatic
multi-step run resumption. Do not repeat the mutation helper on the same journal.
No new cleanup policy removes an unresolved request.

## Live Material Settlement Hooks

The first slice above covered the rolling PowerShell caller, not every client of
the same Backend endpoint. A subsequent caller audit found the Museum custody
and Rain Totem live hooks still posted directly. Both now use one shared C#
`PostJournaledPortfolioSettlementAsync` helper before continuing their existing
completed-ID and failure checks. Their canonical request builders and native
verification gates are unchanged; this is not another settlement service.

Before POST, the helper create-new writes and flushes the full UTF-8 request and
the same `stardewai.portfolio_rpc_journal.v1` index understood by the PowerShell
reader. Names are derived from the exact request SHA-256 within the existing run
snapshot directory; an existing request refuses sending. The request budget is
8 MiB, the index read budget 16 KiB, and no snapshot is copied or hashed. These
records do not match rolling snapshot cleanup names. Explicit UTF-8 decoding in
the PowerShell reader avoids locale-dependent request decoding.

The existing C# HTTP helper now retains error status and accepts an optional
cancellation token. Explicit 4xx rejects stop without lookup. Unknown transport,
malformed replies or 5xx allow one lookup with a 15-second token. Before querying,
the original index/body, regular-file status, size and hash are checked. The
observation must be read-only and match the actual portfolio, route source,
supporting native receipt hash, committed revision and resulting ledger/state.
It is retained separately; the original hooks still verify completed reservation
IDs. Recovery is not permission to mark a native action verified or train it.

Unknown, rejected, corrupt or mismatched lookup keeps the original failure and
sets the existing settlement-failed/retry-required marker. That marker does not
authorize native or mutation replay. Request/lookup paths remain in the execution
diagnostic. The same PowerShell read-only helper can read these C# journals; it
does not resume the run or bypass missing original snapshot/proof requirements.
An unavailable pre-mutation ledger read, implicit mutations inside other planner
endpoints, arbitrary service/process recovery and multi-step run restart are not
claimed closed by this client-boundary change.

## Verification And Remaining Work

Repository tests cover all three operations, restart, exact replies, empty cache,
unchanged published bytes/timestamps, compact storage, rejection, later revisions,
request/actor/state mismatch and corrupt/legacy files. Actual Backend HTTP tests
lose a successful response after the real repository publishes, then query once
and verify no second mutation or audit. Client transport-double tests cover all
three operations, durable-before-send, raw precision, one lookup, unknown results,
unavailable service, changed request, explicit rejection and replay refusal.
These are game-free tests, not a new native trajectory or Teacher preference.
The final local targeted portfolio suite passes 75 tests, the client transport
suite passes 19 checks and the existing Rusty Key evidence suite passes 31 checks.
The PR quality workflow runs the complete Backend and game-free governance suites
on its exact submitted head; its result must be checked separately before merge.

The live-hook extension passes the complete local Backend suite (379 tests).
It exercises both actual hook methods against the real Backend/repository, lost
or malformed accepted replies, explicit rejection, unavailable/cancelled/missing
lookup, wrong bindings, claimed writes, journal/body drift and pre-send replay
refusal. One no-window PowerShell subprocess reads a real C# journal and preserves
the exact raw reply using a query transport double. All are synthetic game-state
transport tests, not native gameplay or a model-training trajectory.

Pending native/host crashes, custody relocation, joint scheduling and the Rusty
Key same-version acquisition/custody/donation/consumption trace still require
their own evidence. The native trace remains first after quiet restrictions lift;
criterion comparison/native partitions and the pre-Year-3 deadline are not
promoted by this transport work. Frozen counts remain 5/19 executable, 6/19
split-complete, 5/19 CoverageGateReady and 4/11 root-ready;
`formal_product_training=false`.
