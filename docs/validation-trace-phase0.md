# Validation Trace Phase 0

Issue #197 is the measurement-only first slice of #195. It does not enable a cache, skip a rebuild, reuse a mutable game fact, or change training/runtime admission.

## Enabling a trace

Bootstrap traces are opt-in:

```powershell
dotnet StardewAI.GoalConditionedBootstrap.dll <command> `
  --validation-trace artifacts/traces/run.validation-trace.json `
  --validation-trace-scenario single_low_risk_decision `
  <command arguments>
```

`STARDEWAI_VALIDATION_TRACE` and `STARDEWAI_VALIDATION_TRACE_SCENARIO` are equivalent process-level inputs. Runtime evidence scripts only propagate traces to Bootstrap subprocesses when `STARDEWAI_VALIDATION_TRACE_DIR` is explicitly set.

Tracing is default-off. Setup and write failures are swallowed by the diagnostic boundary, stdout remains owned by the original command, and every Phase 0 record has `cache_hit=false`.

Repeated operations are grouped by operation kind, producer/version, and input paths. A group records its first and last operation IDs, invocation count, cumulative wall/bytes/hash/parse/materialization metrics, sampled CPU cost, and a bounded artifact-ID sample. Root command CPU remains exact; operation CPU uses the declared `operation_cpu_sampling_rate`. This preserves hotspot accounting without letting the observer produce an unbounded trace or query process CPU millions of times.

Command-level allocation uses the runtime's process-wide managed-allocation
counter. Operation-level allocation uses the current managed thread and is a
hotspot indicator: nested operations can overlap, and work moved to another
thread is represented by the command total rather than attributed to the
originating operation.

## Rebuilding the baseline

Collect exactly these scenario labels:

```text
single_low_risk_decision
machine_supporting_transition_portfolio
full_shipment_single_layer_proof
anchor_high_risk_evidence_preflight
teacher_dataset_generation_relabel
```

Then aggregate only successful `*.validation-trace.json` files:

```powershell
dotnet StardewAI.GoalConditionedBootstrap.dll build-validation-trace-baseline `
  --trace-root artifacts/validation-trace-phase0-baseline-20261011/traces `
  --output artifacts/validation-trace-phase0-baseline-20261011/validation-trace-baseline.json
```

The builder rejects a failed command, an incomplete trace, a cache hit, or missing schema/command identity. Raw traces stay under ignored `artifacts/`; the comparable aggregate is checked in as `docs/validation-trace-phase0-baseline.json`.

Trace v2 extends the baseline with process allocation, GC, private-memory,
managed-heap, read-session retention, and deterministic-cache producer
metrics. The checked-in 2026-10-11 v1 aggregate remains a historical CPU/I/O
baseline and must not be relabeled with invented memory values. A new v2
aggregate replaces it only after the same five scenarios are actually
collected. Until then, memory acceptance claims remain unavailable.

## Frozen baseline

The 2026-10-11 run covered all five representative scenarios with zero cache hits. Total wall time was 30.04 minutes; `self-test-current-stage-one-collection` accounted for 29.99 minutes, 183.27 GiB read, and 95.6 GiB hashed.

Top measured operation groups were:

| Operation | Invocations | Cumulative wall | Bytes read |
| --- | ---: | ---: | ---: |
| content-addressed JSON read | 616729 | 9.64 min | 87.69 GiB |
| JSON deserialize | 454111 | 7.56 min | 0 |
| frontier file hash | 857757 | 6.19 min | 93.84 GiB |
| content inventory file hash | 14997 | 0.12 min | 1.76 GiB |

The numbers freeze the immediate optimization order:

1. #198 may select conservative local fast checks, but PR quality remains full and unknown impact falls back broad.
2. #199 may deduplicate immutable bytes/hash/materialization/parse only inside one invocation and only under exact content identity.
3. Cross-run certificates, incremental validation, proof rebasing, and snapshot deltas remain #200-#203 work and cannot begin by weakening fresh-state gates.

No wall-clock measurement is training truth. Native evidence, fresh snapshots, actor/ownership/lease checks, execution receipts, settlement, and formal product promotion remain unchanged.

## 2026-10-15 current-authority exit-gate audit

The optimized equal-prefix benchmark remains valid performance evidence, but
the frozen 26-stratum manifest is not current-authority evidence. A current
source scan rebuilt Sap successfully and classified the other 25 strata as
stale at location-route, daily time/energy, calendar, or Teacher-preference
boundaries. Offline planning rebuild now reaches Teacher, commit, and dispatch;
it then correctly rejects historical execution receipts that lack the current
compiled player-location and tile context. Closing #274 therefore requires
fresh native executions followed by force-full equivalence. Historical receipts
must not be rewritten or accepted through weaker equality.
