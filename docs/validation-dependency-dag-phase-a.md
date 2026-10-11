# Validation dependency DAG Phase A

Issue #200 Phase A adds an observe-only dependency graph to an explicitly
enabled validation trace. It explains a full rebuild; it does not replace one.

## Contract

Every `stardewai.validation_trace.v1` report contains a
`dependency_graph` object with schema
`stardewai.validation_dependency_graph.v1`. Its fixed safety fields are:

```json
{
  "observation_only": true,
  "can_affect_admission": false,
  "incremental_reuse_authorized": false
}
```

A node records its artifact kind and identity, content SHA-256,
producer/version, input artifact identities and hashes, invocation/cache
observations, and cumulative wall/CPU cost. Nested deterministic rebuilds form
explicit edges. External files and context values remain graph roots.

`theoretical_invalidation_sets` walks those edges from each external input to
all descendant nodes. These sets are explanations only. They cannot skip a
producer, inherit a proof, authorize a native replay, or change formal training
admission.

## Covered rollout chain

The current acquisition rollout observation includes:

```text
calendar resolution
-> target-date calendar / unlock / festival / location / facility
-> resource / currency / reservation / processing
-> fishing probability / stochastic retry / daily time-energy
-> opportunity cost / Teacher preference
-> portfolio admission / portfolio commit
-> dispatch validation / execution binding
-> fresh terminal / settlement / rollout checkpoint / rollout proof
```

Snapshot-backed route and wild-tree indexes are represented as invocation-only
derived index nodes. Snapshot facts keep `reuse_scope=snapshot`; all other
observed derived nodes remain invocation scoped.

## Frozen comparison

The Full Shipment Sap recurrence-prefix fixture produced one acyclic graph with
25 producer nodes and 42 external-input invalidation sets. Changing the initial
snapshot theoretically invalidated all 25 descendants. The checkpoint output
SHA-256 remained
`98222532fcb4df7b16847311159095bf5941e87f12188d2b19290906db8d6f12`,
with `verified_recurrence_prefix` and `prefix_proof_verified=true`.

Graph collection increased this diagnostic run from about 24.34 seconds to
about 29.20 seconds because Phase A hashes observed outputs and computes the
complete invalidation explanation. The default path does none of that work:
dependency collection exists only when a validation trace is explicitly
enabled. A direct Release no-trace replay completed in 24.927 seconds with the
same output SHA-256; timing remains observational rather than admission truth.

## Teacher dataset comparison

The independent Teacher supervision export now adds two outer certificates to
the same graph instead of introducing another verifier:

```text
rollout proof
-> training sample admission
-> Teacher supervision dataset
```

Both producers still execute their existing controller recomputation. The
rollout admission certificate binds the proof manifest and proof receipt; the
Teacher dataset certificate additionally binds the admitted receipt and the
verified transition artifacts reached through the existing proof chain.

The frozen one-transition Full Shipment Sap terminal rollout produced one
acyclic graph with 27 producer nodes and 44 external-input invalidation sets.
Changing the initial snapshot theoretically invalidates all 27 nodes. Changing
only the proof manifest invalidates the rollout proof, training sample
admission, and Teacher dataset certificates. The traced and no-trace dataset
files have the same SHA-256:

`c91a75874801bb2d8e1c7459de35862ed0605aeb876a3ba6a563f5bcd90b4579`

The dataset remains `ready_verified_portfolio_supervision_dataset` with one
Teacher preference and one native outcome. It is eligible only as Teacher
training evidence; `formal_product_training_authorized=false` remains
unchanged. The observed run took about 27.43 seconds with the graph and 22.14
seconds without it. These timings are diagnostic samples, not admission facts.

## Non-goals

- no cross-command or cross-run reuse;
- no certificate-based skipping or proof inheritance;
- no selective native replay;
- no planner, compiler, executor, verifier, or admission replacement;
- no claim that #200 Phase B or formal training admission is complete.
