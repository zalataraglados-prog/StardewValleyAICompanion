# Implementation Acceptance

- Concrete base `Quest` plus all 11 ordinary subclasses are represented by runtime type, with type-9 classes unambiguous.
- All nine special-order objective and six reward runtime classes have structured direct-read coverage or explicit per-field unavailability.
- No observer path invokes quest event/probe/mutator/reload methods.
- Ordinary and special-order candidate families remain separate and preserve blocked rows.
- Candidate time and energy are explicit unknown sentinels; option stays preview-only and compiler stays executor-blocked.
- Fixed `quest.advance=120` is removed.
- Compiler output binds stable identity and exact transparent evidence.
- Required output/training fields are recorded or explicitly blocked.
- Tests are added but not executed; no runtime activity occurs.
- Static diff has no conflict markers/whitespace errors, notes and risks are current, and final changes are committed.
