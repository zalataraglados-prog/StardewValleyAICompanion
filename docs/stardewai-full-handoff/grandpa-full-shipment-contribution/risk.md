# Full Shipment Residual Risks

1. Modded item registries can change the eligible set. The adapter therefore recalculates from current parsed data and fails closed when the API shape is unreadable.
2. A stale candidate must never survive into execution. Exact `state_hash`, availability, timeline, contribution, and compiler revalidation remain mandatory.
3. Shipping has delayed achievement feedback. The immediate receipt proves the deposit; the pending receipt must settle against the next day-end `basicShipped` state before strategy reward is finalized.
4. The latest runtime smoke skipped overnight settlement for speed. Prior dedicated settlement evidence exists, but the complete multi-day training acceptance run must cover this again.

Mitigations are typed nullable evidence, contradiction checks, no direct state mutation, exact state-hash binding, isolated E-drive runtime tests, and separate executor-calibration versus strategy reward channels.
