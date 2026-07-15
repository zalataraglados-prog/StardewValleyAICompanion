# Risk

## Risk Level

- Medium-high until isolated runtime smoke validates native input timing and BobberBar control in-game.
- Low for this capability-declaration slice: Core `413/413`, Backend `49/49`, and the runtime harness build pass.

## Residual Risks

- `ControlledInputState` is deliberately scoped to active catch execution and restored on completion, but needs runtime validation to prove no interaction with other input paths.
- BobberBar controller gains are static and should be tuned from runtime diagnostics if difficult fish still escape.
- Junk/special classification is fail-closed by absence of BobberBar success evidence and by explicit junk/special-pull plus BobberBar rejection; future special cases with menus may need additional explicit terminal labels.
- The one-shot hook latch prevents repeated `DoFunction` calls for a single nibble, but still needs runtime smoke validation after the active user-play constraint is lifted.
- Required verified actions are bounded by `--iterations`; if the attempt cap is too low, a run can finish short of the requested verified count and should be treated as incomplete.

## Mitigations

- No direct fish result mutation, RNG manipulation, teleport, direct bobber placement, or direct transition to post-charge casting was added.
- Static tests assert the new fail-closed strings, one-shot hook latch, candidate metadata, controlled input, actual release-power fields, and verified-action loop semantics.
- Runtime validation commands are recorded as pending for the controller to run after the user-play constraint is lifted.

## Executor Capability Reconciliation

- Existing transparent-state, parameter, timeline, menu, collision, budget, and post-state gates remain unchanged.
- Capability-matrix and recovery tests cover nine reconciled IDs and five deliberately incomplete high-level IDs.
- `recovery.stabilize_day` is enabled only for transparent, compiler-backed candidates. Cross-map return-home and already-open prompt takeover remain blocked.
