# evidence.md - Grandpa Direction Daily Candidate Binding (Final)

## Verification Status

This handoff was superseded by a tested controller revision on 2026-07-17. Focused Core tests passed 103/103, full Core tests passed 946/946, Backend tests passed 49/49, and an E-drive isolated native shipping smoke passed its immediate postcondition.

## Catalog Completeness

The catalog covers all 12 direction IDs:
```
complete_community_center complete_joja_development complete_full_shipment
complete_master_angler complete_museum_collection earn_money earn_pet_love
marriage_and_house_upgrade obtain_rusty_key obtain_skull_key
raise_friendships raise_skill_levels
```

Catalog entries are policy-only: no `domain`, `label`, `feedback_key`, or `factor_ids` stored. Entries contain only `direction_id`, `binding_rule_id`, `direct_binding_enabled`, `permitted_option_ids`, `permitted_candidate_kinds`, `required_transparent_fields`, `required_capabilities`, `block_reason_template`, `cc_joja_sensitive`.

Covered by an unrun test definition: `CatalogEntriesArePlanPolicyOnlyNoScoreMetadata` and `All12DirectionsExistInCatalogAndCorrespondToAdapter`.

## State-Hash Binding (Fail-Closed)

- Empty `state_hash` returns `"state_hash_is_empty"` with `StateHashEmptyOrUnknown = true`. Covered by an unrun test definition: `BindRejectsEmptyStateHash`.
- Null snapshot for known state_hash returns `"state_hash_unknown_backend_resolves_no_snapshot"`. Covered by an unrun test definition: `BindRejectsNullSnapshotWithKnownStateHash`.
- Mismatched state hash returns `"state_hash_mismatch_request_state_hash_does_not_match_snapshot_state_hash"`. Covered by an unrun test definition: `BindRejectsStateHashMismatch`.

## CC/Joja Route Commitment (Unresolved)

Both `complete_community_center` and `complete_joja_development` unconditionally block with `cc_joja_route_commitment_unavailable`. No speculative bool traversal. Audit field `CcJojaRouteCommitmentResolved` is always `false`.

Covered by unrun test definitions: `BindBlocksCompleteCommunityCenterWithUnresolvedRouteCommitment`, `BindBlocksCompleteJojaDevelopmentWithUnresolvedRouteCommitment`, and `BindCcJojaRowsAlwaysReportUnresolvedRouteCommitment`.

## No Speculative Field/Capability Checks

`FieldReadableInSnapshot()` and `CapabilityAvailable()` are removed. Eight non-direct directions are unconditionally blocked as planned contract gaps. Covered by `BindEightNonDirectDirectionsAllReturnBlockedWithPlannedRequirements`.

Eight non-direct rows all return non-empty `MissingTransparentFields` and `MissingCapabilities` from their catalog entries.

## Candidate Identity Preserved

Bound candidates retain original `CandidateId`, `Score`, `Rank`, `ExpectedReward`, and all action fields. Covered by unrun test definitions: `BindPreservesCandidateIdScoreRankExpectedRewardAndActions` and `BindPreservesAllSourceCandidateFields`.

## BlockReasons Gate

Candidates with non-empty `BlockReasons` are rejected even when `TimelineStatus` is not `"blocked"`. Covered by an unrun test definition: `BindRejectsCandidateWithBlockReasonsEvenWhenTimelineNotBlocked`.

## Bindings

### earn_money
- Permitted: `sell_or_ship_inventory_item` kind, `economy.sell_items` option
- Does NOT claim grandpa threshold, factor completion, or delta prediction
- Covered by unrun test definitions: `BindEarnMoneyBindsSellShipCandidatesWithProvenance` and `BindEarnMoneyDoesNotClaimGrandpaThresholdReached`

### raise_friendships
- Permitted: `social_talk_current`, `social_gift_current` kinds
- Does NOT promise friendship points
- Covered by unrun test definitions: `BindRaiseFriendshipsBindsSocialCandidatesWithProvenance` and `BindRaiseFriendshipsDoesNotPromiseFriendshipPoints`

### complete_master_angler
- Permitted: `catch_fish` kind, `fishing.catch_fish` option
- Does NOT promise specific catch or achievement
- Covered by unrun test definitions: `BindCompleteMasterAnglerBindsCatchFishCandidatesWithProvenance` and `BindCompleteMasterAnglerDoesNotPromiseSpecificCatchOrAchievement`

### complete_full_shipment
- Permitted: `ship_inventory_item_to_bin` kind, `economy.ship_items` option
- Requires exact typed evidence that the item is eligible, has never been shipped, contributes to completion, and can currently be shipped
- Unknown or contradictory evidence fails closed with `candidate_direction_evidence_rejected`
- Covered by `FullShipmentDirectionBindsOnlyExactContributingCandidate`, `FullShipmentDirectionRejectsAlreadyShippedCandidateEvenWhenContributionFlagConflicts`, and `BindFullShipmentRejectsCandidateWithoutExactContributionEvidence`

## Corrected Readiness and Provenance

### Readiness
- One valid candidate = `ready` (not `full`). Covered by an unrun test definition: `BindSingleCandidateIsReadyNotFull`.
- Rejects: `TimelineStatus == "blocked"`, `BlockReasons` non-empty, `Available == false`, `AllowedNow != true`, `AllowedToday != true`. Covered by unrun test definitions: `BindSkipsBlockedCandidates`, `BindRejectsCandidateWithBlockReasonsEvenWhenTimelineNotBlocked`, `BindSkipsCandidatesWhereAllowedNowIsFalse`, `BindSkipsCandidatesWhereAllowedTodayIsNotTrue`, `BindSkipsUnavailableCandidates`.

### Provenance Parameters
Added once without overwriting existing names: `grandpa_direction_id`, `grandpa_source_state_hash`, `grandpa_related_factor_ids`, `grandpa_binding_rule_id`. Duplicate provenance names (second occurrence of the same name, even with matching values) reject with `candidate_provenance_duplicate:<candidate_id>:<name>`.
Covered by unrun test definitions: `BindDoesNotIncludeDuplicateGrandpaProvenanceParams`, `BindDoesNotAddProvenanceIfAlreadyPresentOnSourceCandidate`, `BindRejectsDuplicateMatchingProvenance`, and `BindRejectsDuplicateConflictingProvenance`.

### Array Cloning
`Parameters`, `GateReasons`, `BlockReasons`, `TimelineReasons` are independently allocated (cloned) to prevent output-input aliasing.
Covered by an unrun test definition: `BindClonesCandidateArraysToPreventAliasing`.

### Missing Fields Populated
Blocked results populate `MissingTransparentFields` and `MissingCapabilities` from the catalog entry. Verified for all eight non-direct rows by `BindEightNonDirectDirectionsAllReturnBlockedWithPlannedRequirements`.

### Metadata Sourced from Adapter
Domain, label, feedback_key, related_factor_ids, potential_points, priority_score, known, blocked are all from the adapter's `CandidateDirection`, not the catalog.
Covered by an unrun test definition: `BindDirectionMetadataIsSourcedFromAdapterNotCatalog`.

## No Fabrication

`BindDoesNotFabricateTileItemShopTimeEnergyQuantity` (unrun test definition) confirms minimal input candidates do not gain fabricated tile, item, shop, time, energy, or quantity values.

## Handoff to DailyPlanCompiler

`BindBoundCandidateHandoffToDailyPlanCompilerDoesNotFail` (unrun test definition) confirms bound candidate feeds into `DailyPlanCompiler.Compile()` producing a valid plan envelope.

## Build/Tests

- Focused Core: 103/103 passed.
- Full Core: 946/946 passed.
- Backend: 49/49 passed.
- E-drive isolated native shipping immediate postcondition: passed.
