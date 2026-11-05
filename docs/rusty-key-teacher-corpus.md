# Rusty Key Teacher Corpus

## Scope

`RustyKeyTeacherCorpusBuilder` groups existing independently verified
`RustyKeyTeacherNativeJoinBuilder` results. It neither executes gameplay nor
creates preferences, trains a model or declares the entire criterion complete.

Request schema `rusty_key_teacher_corpus_request.v1` supplies `corpus_id` and
`sources`, each with a unique `source_id` and `join_receipt_path`. Build calls
the original join verifier for every source, retaining the complete result and
its source hash. Verify rebuilds the request and original joins, then compares
raw persisted JSON, including derived claims. Deserialization alone is not
verification.

The split is the original donation decision's `save:player:day`, computed by
the existing dataset partition function. Decision groups, run IDs, request
nonces and full `run:queue:item` action identities cannot repeat. Renaming a
source, copying its file or using its later key-event day cannot add coverage.
Missing/null sources, changed denominators and forged status/count/partition
or training claims are rejected. Rows are sorted deterministically.

`split_complete` only means independently verified rows cover train,
validation and test. `full_criterion_verified` and
`formal_product_training_authorized` remain false even for a complete split.
Method/supply/deadline obligations and global training authorization remain
with their original owners.

## Existing Coverage Integration

The existing `GoalMethodTeacherCoverageBuilder` accepts source kind
`rusty_key_threshold_decision_corpus`. Its adapter calls the corpus verifier,
requires exactly one catalog-backed Rusty Key method, and contributes each
row's same partition to Teacher comparison and native outcome sets. It does
not set readiness, edit method status or change frozen counters.
The original reconciliation adapter inventory also registers this source kind,
so its next-work report does not incorrectly ask for an already implemented
adapter. A regression first reproduced that missing registration and now
checks the Rusty Key root without assigning its adapter to unrelated roots.

The original coverage intersection still requires an `executable_frontier`
method and all three Teacher/native partitions. Tests cover partial corpus
with executable method, complete corpus with pending method, and their valid
intersection. A synthetic complete split is test data, not native evidence.

## CLI And Retained Evidence

```powershell
dotnet <bootstrap.dll> build-rusty-key-teacher-corpus --request <request.json> --output <corpus.json>
dotnet <bootstrap.dll> verify-rusty-key-teacher-corpus --corpus <corpus.json> --output <replay.json>
```

Local evidence remains outside Git at
`I:\StardewAITraining\rusty-key-terminal-source-20261105`. Public build and
independent Verify of `teacher-corpus.json` and `teacher-corpus-replay.json`
produce SHA-256
`fcecbed9711b9a4f89de04b8de5243ae6da6fc5eb17a438edcd76bbdc48c4c7e`.
The corpus contains only the retained `rk-e8d7a6c3-1104` decision at day 6:
**one train row**, not three partitions or full criterion completion.

Local regression passes `1369/1369` game-free Core tests, including the
direction/museum/corpus family `261/261`; no tests were skipped. The new corpus
suite initially contributed 21 cases. The reconciliation registration check
adds one more (22/22 focused); the real public reconciliation CLI now lists
the adapter as implemented and connected, while retaining the pending method
and missing validation/test work. Checkpoint EVD-338 remains unchanged. Final
PR CI is still required for the committed head.

Remaining work is genuine missing native decision partitions plus the original
method, safe threshold supply and pre-Year-3 responsibilities. Do not rerun
accepted local custody/key/visual gates or impose a complete Day-0 run as a
reverse prerequisite. No worker, paid API, new game, checkpoint change or
formal training was used to implement this adapter.
