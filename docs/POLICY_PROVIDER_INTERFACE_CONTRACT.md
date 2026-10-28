# StardewAI Policy / Supervision Provider Interface Contract

> Status: pre-training interface contract.
>
> Purpose: keep the runtime and training pipeline replaceable. The built-in Student and Teacher are reference providers, not mandatory implementations.

## 1. Scope

This contract defines the boundary between StardewAI deterministic game authority and replaceable policy / supervision sources.

The following remain StardewAI authorities and are not delegated to a provider:

- authoritative candidate generation;
- hard constraints, permissions, reservations and irreversible-action gates;
- route legality and world-state validation;
- Planner / Compiler;
- Product Executor;
- fresh native outcome verification;
- save/runtime provenance and evidence admission.

A provider may choose or rank only from the candidate set supplied by the host. It may not invent executable actions, coordinates, native inputs, world mutations or unregistered candidates.

## 2. Provider roles

Two independent provider roles exist.

### 2.1 Decision Provider

A Decision Provider supplies runtime preference over an already admitted candidate set.

The built-in learned Student is one implementation. Other implementations may be substituted, including a different local model, heuristic policy, external service, research model, or other compatible source.

The runtime must not depend on a particular model family, framework, checkpoint format, training method or hardware backend.

### 2.2 Supervision Provider

A Supervision Provider supplies optional training/relabel/evaluation preference evidence over the same authoritative candidate context.

The built-in Teacher is one implementation. A deployment may replace it, disable it, or use another compatible supervision source.

The runtime must not require a Supervision Provider in order to execute an already admitted policy. Teacher-off routes therefore remain valid as long as the Decision Provider and deterministic authorities can proceed safely.

Decision and supervision providers are versioned and configured independently. One provider must never be inferred from the identity of the other.

## 3. Transport-neutral request contract

The host must expose a versioned, transport-neutral request that can be adapted to in-process code, local IPC, CLI, HTTP/RPC, or another implementation without changing game authority.

Every decision/supervision request must bind at minimum:

- schema version;
- request ID;
- provider role;
- game/runtime authority version identity;
- goal context visible to the policy;
- Student-visible / policy-visible state context;
- complete admitted candidate set and stable candidate IDs;
- deterministic authority / candidate-set digest;
- request provenance sufficient to reproduce or reject stale responses.

The exact feature representation may evolve, but semantic fields and schema versions must be explicit. Missingness must be represented, not guessed.

## 4. Decision response contract

A Decision Provider response must bind to exactly one request and may contain only:

- provider identity and version;
- request ID / request digest;
- an ordering, utility/score, preference relation, or explicit abstention over candidate IDs present in the request;
- optional provider diagnostics that are non-authoritative;
- provenance needed to reproduce or audit the response.

The host must reject:

- unknown candidate IDs;
- stale request bindings;
- responses that try to introduce executable commands or bypass deterministic admission;
- malformed or non-finite preference data;
- provider output that conflicts with a host hard constraint.

The host owns final admission and dispatch. Provider confidence is never evidence that a game rule is true.

## 5. Supervision response contract

A Supervision Provider response may contain preferred sets, pairwise/partial ordering, abstention/unknown, or other versioned supervision signals over the supplied candidate set.

It must bind to:

- the exact request/candidate denominator;
- the supervision provider identity and version;
- provenance / source identity;
- explicit unavailable, tied, unknown or abstained states where applicable.

Supervision evidence and native game outcomes remain separate channels. A supervision source cannot manufacture a native outcome, and a native outcome alone cannot be silently reinterpreted as a preference label.

## 6. Provider manifest and lifecycle

Every replaceable provider must expose a small machine-readable manifest containing at minimum:

- provider ID and version;
- role: decision, supervision, or both through separate endpoints;
- supported request/response schema versions;
- runtime backend requirements;
- model/checkpoint identity if applicable;
- health/readiness status;
- deterministic/reproducibility metadata where applicable.

Provider artifacts may be opaque to the host, but their identity, version and content hash must be auditable.

Startup, reload, failure and fallback behavior must be explicit. A provider failure must not silently widen game authority.

## 7. Runtime substitution rules

Switching providers must not require changes to:

- TransparentBridge;
- candidate generation;
- hard constraints;
- Planner / Compiler;
- Product Executor;
- native receipt verification.

Provider replacement is valid only if the replacement passes the same interface conformance and behavior-safety tests.

The host may support more than one configured Decision Provider, but exactly one provider is authoritative for a given runtime decision request unless an explicit, versioned arbitration layer is introduced later.

A Decision Provider may abstain. The host then follows the configured deterministic fallback or fails closed; it must not silently invoke the built-in Teacher unless that fallback is explicitly configured.

## 8. Training and experiment neutrality

The provider interface deliberately does not prescribe:

- linear, tree, neural, graph, sequence, language-model or other model families;
- loss function;
- optimizer;
- architecture size/depth;
- embedding representation;
- quantization/precision strategy;
- framework or language;
- local versus external training.

The pre-training Codex experiment must choose the best deployable implementation from evidence, subject only to:

- interface conformance;
- frozen train/validation/test governance;
- deterministic-authority boundaries;
- reproducible evaluation;
- product hardware/runtime limits.

The current built-in linear ranker is a control baseline, not a required final implementation.

## 9. Hardware boundary

Training and experimentation may use the RTX 5070 Laptop GPU.

The selected production Decision Provider must remain usable without CUDA or a discrete GPU on the project deployment baseline. The total system-memory ceiling is 8 GiB for the game, SMAPI, StardewAI runtime and policy provider together.

The interface contract must not assume the training machine's accelerator, instruction set or framework is present at runtime.

## 10. Pre-training exit gate

Before formal Stage 1 training:

1. this interface contract is frozen to a version;
2. the built-in Student and Teacher are exposed through adapters implementing the same provider contract;
3. provider conformance tests cover normal, stale, malformed, unknown-candidate, abstention and provider-failure cases;
4. the Codex model-selection experiment consumes and produces only through the frozen provider/data contracts;
5. the selected provider implementation is independently reproducible and passes the low-spec deployment gate;
6. formal training manifest and preflight bind the provider contract version and selected provider identity.

This gate changes provider replaceability and model-selection discipline only. It does not change Goal Method coverage, Teacher/native evidence denominators, game authority or Product Executor semantics.
