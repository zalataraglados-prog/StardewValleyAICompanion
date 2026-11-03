# Product Receipt Recovery

## Existing Failure Boundary

The live loop previously generated the request nonce only in memory immediately
before POST. A lost response therefore lost the exact client request identity.
Product Executor already has the correct pending/final store; this change reuses
it rather than adding another executor or native receipt format.

Client cancellation could also cancel final journal publication after a native
action had already started. The Product service now owns that one dispatch after
durable pending intent, with its configured native timeout. Native dispatch and
the fresh after-snapshot share the bounded service token; final or indeterminate
publication is not cancelled by an abandoned HTTP client. Cancellation before
intent is still honored. A timeout never authorizes a second native action.
The existing atomic store now flushes its write-through temporary file before
publishing pending/final JSON; lookup itself still performs no write.

## Durable Client Request

The existing live dispatch call writes `execution-request-<iteration>-item-<n>.json`
and one adjacent `.json.gz` before POST. Both are create-new and flushed before
dispatch; an existing ordinal or failed write blocks sending. The index binds
run, queue, item, nonce, option, state hash, original endpoint and before-snapshot
reference. It records both the exact wire SHA-256 and the Product-normalized
payload SHA-256, which are deliberately different identities.

The gzip preserves the complete wire body, not a selected-field summary. There
is no extra snapshot capture or full-snapshot hash. Compression is one fast gzip
per request, not the snapshot store's per-property chunk layout. Requests over
8 MiB fail before dispatch instead of being truncated. Reading checks the exact
adjacent blob name, schema, byte budget, decompressed length, raw hash and rebuilt
request identity; reparse points and metadata/payload drift are rejected.

These recovery records deliberately do not match the rolling snapshot deletion
pattern. They remain subject to the existing run/disk budget and explicit archive
review; no new automatic cleanup deletes an unresolved nonce or its wire body.
Keeping a request does not certify that its referenced before-snapshot or all
other evidence is still available. Missing proof must still block downstream
native verification, not be reconstructed from inventory guesses.

## Read-Only Product Lookup

`POST /api/v1/product/receipts/lookup` takes the original request object. It applies
the same capability, actor, run, save-root, nonce and schema authorization as
execution, allowing an expired request only for reading its existing receipt.
Explicit null option/state hashes are rejected by the shared authorization gate.

The lookup reads the existing store without taking the native dispatch lock,
issuing a native/snapshot request, resolving a pending record or rewriting any
file. It reports `missing`, `pending`, `final`, `conflict`, `corrupt` or `rejected`.
Every result has `execution_authorized=false`, `native_dispatch_performed=false`
and `fresh_verification_required=true`. Missing or pending is never permission to
resubmit. A final receipt may itself contain an indeterminate/blocked result.

## Original Dispatch Continuation

After a Product transport/response failure, the original live dispatch call may
make exactly one read-only lookup, bounded to 15 seconds. Harness mode cannot
use this Product recovery path. Only a final response matching the original
run/queue/item/option/before hash, Product source and exact payload hash is handed
back to the existing after-snapshot, feedback and native-outcome verification
chain. No result is marked verified merely because lookup succeeded, and the
lookup observation itself always has `native_outcome_verified=false`.

Pending, unavailable, corrupt or mismatched results preserve the original error,
request journal and lookup diagnostic; they never resend or replan around an
unconfirmed action. Application-blocked final responses remain blocked. This
does not automatically settle a reservation, release custody, restore a lost
Backend mutation response or resume an interrupted multi-step run.

## Operator Command

The existing LiveTrainingLoop executable has a cold command before normal
training options, manifest or historical runtime discovery:

```powershell
dotnet tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll lookup-product-receipt --request-journal <absolute-journal-path> --product-url http://127.0.0.1:8768 --output <new-output-path>
```

The explicitly supplied Product URL must match the recorded execution endpoint.
The command calls only the lookup route and refuses an existing output. Exit 0
means a final stored result was retrieved, not that an action succeeded; all
other lookup states exit 2. Its output is a query observation, never execution,
native proof, Teacher preference, training permission or a settlement request.

## Verification And Remaining Work

Tests exercise the real pre-dispatch helper and server store, lost replies,
pending and wrong-identity refusals, client cancellation, the service deadline,
unchanged pending/final files, authorization/null boundaries, gzip/index drift,
overwrite rejection, in-process real HTTP routing and a separate actual CLI
process. No game or model training is involved in these transport tests.
The current local full Backend run passes 338 tests and game-free Core governance
passes 1059 tests. The existing fishing source guard now follows the journaled
shared send call instead of requiring the old inline transport spelling.

This closes a client identity/final-response retrieval boundary, not all failure
recovery. A host/native crash with only pending evidence is still indeterminate.
The Rusty Key same-version native acquisition/custody/donation/consumption run
remains first after quiet restrictions lift. Backend commit/settlement reply loss
now has its separate [original-ledger recovery boundary](portfolio-mutation-reply-recovery.md);
it is not resolved by this Product lookup. Custody relocation, joint windows/budgets, criterion comparison/native
partitions and the pre-Year-3 deadline remain separate required work. Frozen
coverage counts and `formal_product_training=false` are unchanged.
