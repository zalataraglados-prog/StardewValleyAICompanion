# Validation performance telemetry

Issue #274 extends the opt-in validation trace without changing validation,
proof, runtime, or training admission semantics.

## Command measurements

`stardewai.validation_trace.v2` records:

- wall and process CPU time;
- current and peak working set plus private memory;
- process allocation delta and Gen0/Gen1/Gen2 collection deltas;
- physical read, hash, JSON parse, and materialization work;
- invocation read-session retained representations and largest entries;
- deterministic cache entries, retained bytes, maximum entry, and producer
  build/hit counts;
- inclusive operation allocation and collection observations.

Operation allocation is inclusive. Nested operation totals can overlap and
must not be added to infer process allocation; the command-level
`allocated_bytes` value is the authoritative process delta. Operation values
exist to rank producers.

## Representation lifetime

The invocation read session keeps exact content identity. Once both physical
text and its SHA-256 are available, the raw byte array is released. Once a
content-addressed artifact also has a distinct verified logical text, its
stored manifest text can be released. A later request may reread a released
physical representation, but it may not substitute logical content for stored
content or weaken hash verification.

The read-session trace reports both retained and released bytes so memory
reduction is distinguishable from skipped verification. Session caches are
cleared at command completion before the trace report is serialized.

## Safety boundary

Telemetry and representation release are diagnostic/performance behavior only.
They do not authorize cross-command reuse, cross-snapshot facts, proof
inheritance, native replay reuse, or a lower formal-training gate. Cache-off
and force-full validation remain the correctness oracle.

## Next measured step

After quiet/no-test mode ends, collect one focused trace before any full
26-stratum replay. Use it to verify the v2 fields and rank retained entries.
Only then run the three #274 baseline modes and choose between rollout proof
single-computation, unique-proof-first indexing, or further representation
lifetime work from measured cost.
