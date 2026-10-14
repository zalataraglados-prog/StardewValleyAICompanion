# Invocation-scoped immutable read session

Issue #199 removes repeated physical reads, full-file hashes, and content-addressed JSON materialization inside one Bootstrap command. It does not add a persistent cache or reuse a live game fact.

## Lifecycle

`StardewAI.GoalConditionedBootstrap` creates one `InvocationReadSession` after parsing the command, captures its final diagnostic snapshot, and disposes it before writing the final validation trace. The default is enabled. The compatibility path is:

```powershell
dotnet run --project experiments/StardewAI.GoalConditionedBootstrap `
  -- <command> <arguments> --read-session disabled
```

`STARDEWAI_READ_SESSION=disabled` provides the same process-wide fallback for existing scripts.

Only existing file arguments declared at invocation start are eligible. The session canonicalizes their paths and keeps raw bytes, stored SHA-256, decoded stored text, verified materialized text, and logical SHA-256 where the content-addressed manifest supplies one. Repeated reads and hashes share those values until the command exits.

Typed DTO instances are intentionally not shared. Most current report contracts are mutable classes, so returning one cached object to multiple validators could let a downstream mutation alter a later read. The session removes file I/O, manifest parsing, and materialization duplication while ordinary `JsonSerializer.Deserialize<T>` remains fail-closed per call.

## Exclusions

The Bootstrap argument policy never registers:

- `output` or `output-root` paths;
- checkpoint paths;
- ledger, lease, or ownership paths;
- validation trace/control arguments;
- a file equal to a prohibited output, or a file below a prohibited output root.

Live HTTP snapshot endpoints are not files and cannot enter the session. Content-addressed materialization is cached only after every blob and the final logical payload pass their existing byte-count and SHA-256 checks. A malformed manifest or corrupted blob still throws and is not materialized into the session.

## Trace semantics

`ValidationTraceOperation.cache_hit` is true when an operation reuses session bytes, a stored hash, or a verified materialization. The command-level `cache_hit` is the OR of its operation results. Phase 0 baseline generation still rejects cache-bearing traces; reproduce the frozen no-reuse baseline with `--read-session disabled`.

Validation trace v2 also records the read session's entry count, retained raw bytes, stored UTF-16 text, independently retained materialized UTF-16 text, hash text, total retained representation bytes, and largest entry. These are lifetime diagnostics captured before disposal. They do not authorize reuse and do not replace process-level working-set, private-memory, managed-heap, allocation, or GC counters.

The checked representative comparison is in `docs/invocation-read-session-sample.json`. It runs the same calendar-resolution command against one frozen fixture in both modes, requires byte-identical output, and compares Phase 0 trace counters. Wall time is reported as a sample only; bytes read and hash bytes are the deterministic evidence.

## Non-goals

This slice does not provide cross-command, cross-process, cross-run, or cross-snapshot reuse. It does not cache a mutable game state, execution receipt under construction, ownership decision, or training admission. It also does not change Planner, Compiler, Executor, Verifier, hosted quality, or native evidence requirements.
