# Phase 1A Correction

Phase 1A is `Transparent Read-Only Bridge + Typed Planning Preview`.

The corrected control flow is:

```text
Natural Language
  -> GoalSpec
  -> registered OptionSpec
  -> OptionInstance
  -> Plan
  -> Verifier
  -> CommandPreview
```

The user goal must not directly create a new `OptionSpec`. Option specs are predefined, versioned, tested, and registered in `StardewAI.Core.OptionRegistry`.

`CommandPreview` separates:

- `feasibility`: whether the plan itself is feasible, blocked, or unknown.
- `execution_permission`: whether this phase allows execution.

In Phase 1A:

```text
preview_only = true
execution_permission = disabled
```

This means a plan can be `feasible` while execution remains disabled.

Python backend status:

```text
backend/ is deprecated.
src/StardewAI.Backend is the active transport layer.
```

Target projects:

- `src/StardewAI.Contracts`
- `src/StardewAI.TransparentBridge`
- `src/StardewAI.Core`
- `src/StardewAI.Backend`
