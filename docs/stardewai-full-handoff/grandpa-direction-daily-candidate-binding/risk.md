# risk.md - Grandpa Direction Daily Candidate Binding (Final)

## Risk Assessment

### HIGH: State hash binding requires exact pre-ingested snapshot
**Risk**: The backend endpoint requires `state_hash` from the request and resolves the snapshot from `StateStore.Snapshots[state_hash]`. If the snapshot for that hash has not been ingested, the request fails with 422.
**Mitigation**: This is intentional (fail-closed). Callers must ensure snapshots are ingested before requesting bindings. The state hash binds the candidate set -- no body snapshot authority.

### HIGH: CC/Joja route commitment remains unresolved
**Risk**: Both `complete_community_center` and `complete_joja_development` are unconditionally blocked with `cc_joja_route_commitment_unavailable`. Transparent state does not prove which route the player committed to. No speculative bool check is performed.
**Mitigation**: Explicitly documented as unresolved. Both rows cannot be bound until new transparent evidence for route commitment is exported.

### HIGH: Blocked directions can never bind until contract gaps closed
**Risk**: 3 of 12 directions are unconditionally blocked as planned contract gaps. No speculative field/capability checking is performed -- the catalog merely records what would be needed. Newly added `raise_skill_levels`, `earn_pet_love`, and museum runtime paths remain runtime-pending and fail closed on incomplete evidence.
**Mitigation**: Intentional design choice. The binder does not speculate about snapshot contents.

### MEDIUM: Provenance parameter deduplication and duplicate rejection
**Risk**: Source candidates carrying parameters with the same names as grandpa provenance parameters (`grandpa_direction_id`, etc.) are handled by exact name match. A single matching occurrence is preserved; a mismatched occurrence rejects with `candidate_provenance_conflict`; a second occurrence of the same name (even with a matching value) rejects with `candidate_provenance_duplicate`.
**Mitigation**: The behavior is deterministic and tested. Source parameter values take precedence for single matching occurrences.

### LOW: Runtime coverage is direction-specific
**Risk**: The binder has full static regression coverage, while each executor direction still relies on its dedicated isolated runtime smoke rather than one multi-day end-to-end run.
**Mitigation**: Full shipment has current native immediate proof and prior delayed settlement proof. Museum donation has static native-lifecycle verification but still needs an isolated runtime smoke, including the delayed Farm event 66 transition. Keep isolated direction smokes and add a multi-day training gate only after all remaining direction chains are complete.
