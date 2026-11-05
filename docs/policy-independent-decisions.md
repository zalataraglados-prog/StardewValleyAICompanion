# Independent Policy Decisions

Use the original PolicyDataset CLI's explicit `--independent-decisions` mode
for sampled, independently reset Teacher boundaries that share a base save.
Combine it with `--no-horizon-observations`; any supplied horizon observation
path is rejected before input or output I/O. It does not authorize training.

The existing continuous-save default and its chronological/observation
backfilling remain unchanged. The original four-parameter public Build entry
is retained; a five-required-parameter overload adds the explicit boolean
without changing optional/default-literal call resolution. No executor or
Teacher replay/admission rule is changed.

Independent mode uses the same original backfiller one row at a time with no
continuity observations. It clears derived day/season/year/Grandpa values to
null/pending, including previously supplied derived claims. Immediate observed
feedback, original save/run/split identities, SHA policy, deduplication,
immutable versions and independent Teacher/source replay remain enforced.
Manifest `independent_decisions=true` records the scope; its default false is
omitted to preserve legacy serialized manifests. Row schema and split key are
not changed. This is not another dataset format or learner.

Example invocation uses actual retained Skills rows; all outputs stay outside
Git and an existing diagnostic output must not be overwritten:

```powershell
dotnet tools/StardewAI.PolicyDataset/bin/Release/net8.0/StardewAI.PolicyDataset.dll --input I:/StardewAITraining/skills-local-20261105/policy-splits-input.jsonl --output-root NEW_OUTPUT --no-horizon-observations --independent-decisions
```

The original default is intentional continuous-trajectory behavior, introduced
by `b326bf8c97a336529d922f1a572145e13f60c54d`; the retained 2026-08-01 chat JSON
and original handoff describe observed horizon closure and unknown pending
returns. Do not globally disable inference for real continuous play or treat
independent boundary dates as evidence of overnight progress.

Skills' first default-mode combined output is a retained diagnostic, not the
accepted dataset. The corrected six-row output has two rows per original
partition and zero long-horizon closures; full provenance and hashes are in
[Skills local acceptance](skills-local-acceptance.md). Full new-save/long-horizon
evaluation and formal checkpoint gates still require their own evidence.
