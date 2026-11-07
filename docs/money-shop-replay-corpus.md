# Money Shop Replay Corpus

## Versioned Extension, Not Another Pipeline

PR #491 is accepted at `6fee622a`, exact-head CI42339542942 all38 steps.
The existing `MoneyThresholdTeacherCorpusBuilder` now accepts explicit request/
corpus v5 for the verified shop replay comparison. Default request/corpus v4,
the old exact-frame comparison path and old serialized bytes remain unchanged.
No alternate corpus builder or fabricated v2 comparison receipt is introduced.

A v5 native-sale source supplies `shop_replay_comparison_receipt_path` instead
of legacy per-criterion admission files. The comparison group is independently
verified once per invocation/source path and reused across matching crossed
criteria. Same-row mixing of legacy admissions and replay groups, a trajectory
under this immediate-sale scope, or v4 replay metadata is rejected upstream.
v5 requires at least one actual replay join; merely changing a version string
cannot upgrade a native-only corpus.

Safe CLI entries are `build-money-threshold-replay-corpus` (explicit v5 only)
and `verify-money-threshold-teacher-corpus` (v4/v5). They require a new JSON
output. The original generic v4 build/writer contract is not changed.

## Shared Native and Preference Rules

The old contract was checked at `0e4086a8` and `6f28fce1`: exact-frame schema,
source/native identity, denominator, target, strict preference, native artifacts
and Revenue prefix are real gates, not bugs to bypass. The common preference
header/pair checks are extracted without changing their predicates or messages.
The original native artifact/prefix validator is reused unchanged. v4 still
requires its comparison source hash to equal its actual native source.

Only v5 consumes the explicit v1 reference-to-actual mapping. It retains the
reference request/selection index and actual request/concrete/source identity,
with the selected binding receipt and digest when needed. The selected actual
episode, native artifacts and prefix must match exactly and satisfy the crossed
authoritative threshold. A different reference hash is accepted only through
the independently verified source mapping, never by rewriting the native hash.

An incomparable or differently selected criterion is not self-labeled. Other
eligible crossed criteria may still join; no all-six eligibility/selection gate
is added. At least one join must match the supplied native sale. H's below50k
counterfactual is not a threshold-success row.

New join/source metadata is nullable and omitted for old records. Corpus Verify
reconstructs each declared source kind and independently rebuilds the complete
result. Existing native save/player/day partition assignment, split/source
deduplication, target authority and formal-training gates remain. Forward and
reverse references to the same native sample cannot be counted twice.

## Actual Retained Evidence

No new game or sale was collected. The existing G outcome crosses all six
thresholds; H remains its independently verified counterfactual. Actual public
v5 Build/Verify joins six comparisons to six native outcomes in **train only**:

- Forward corpus file SHA:
  `57fd1b02c8360adf76dec68edf4e382f975028543dd14a2843e4be53554f4830`.
- Reverse-reference regression file SHA:
  `2b4ac8df2aaa2cdbfefeef4a8849122dfe7c0ac98b258a938df3bebd53f90d54`.
  Its selected actual G source differs from reference H; native hashes stay real.
- Original native-only v4 corpus both verifies and rebuilds byte-identically,
  file SHA `8147889a08179472eaa9917910d74a693f21daed0bc1625d57fac5d144f8c2a3`.

Both v5 artifacts describe the same native sample and are alternate regression
representations, not two independent dataset rows. The original work-plan
consumer accepts v5 and reports6 of18 cells complete, two native collection
waves remaining, zero train comparison recollection waves and12 cells blocked
on missing validation/test native sources. `coverage_complete`, native/Teacher
split completeness and formal training remain false.

Focused tests cover the actual join, v4 wire/Verify compatibility, criterion-
local eligibility and21 rejection cases. Seventeen real corpus/request forgeries
plus output/source aliasing reject; final hermetic/CI results are recorded at
closeout. Raw data, callers and local-data remain outside Git.

## Exact Next Work

Use the original updated18-cell work plan. Collect only the genuinely missing
validation/test native source groups, first checking authoritative native
save/player/day partition assignment. Reuse the original hidden/silent isolated
E runtime, evaluator/compiler/executor, source binding, complete comparison and
this v5 join. Do not recollect G/train, alter a partition label/policy date, or
count the reverse reference as another source. The losing counterfactual need
not cross a threshold and must retain its own actual identity.

After all required criterion joins are proven, add the conditional local Money
method/coverage view through existing contracts, preserving original global
acquisition, shared-ledger, calendar/budget and complete-root obligations at
their proper R2 layers. Current accepted union remains13/19 executable,
13/19 split-complete,13/19 ready,10/11 roots; global Teacher21/Student and formal
training are not complete. No workers/API, product training, server changes or
filesystem cleanup is enabled by this work.
