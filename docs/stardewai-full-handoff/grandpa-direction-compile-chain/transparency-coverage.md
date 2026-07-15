# Transparency Coverage

| field or output id | consumer | required for training | transparent source path or reason unavailable | evidence claim id | runtime snapshot/artifact path | output recording path | status |
|---|---|---|---|---|---|---|---|
| `strategy.grandpa_progress.direction_id` | policy, compiler, budget_validator, trainer | yes for strategy plan emission | Derived from `GrandpaTrainingSampleAdapter.CandidateDirections` filtered through Known+unblocked+positive-potential selection; classifier only outputs `requires_direction_selection=true` and defers to snapshot-aware policy; compiler rebuilds candidate set from snapshot via `WorldModelProjector -> GrandpaEvaluationGoalEvaluator -> GrandpaTrainingSampleAdapter` and validates exact direction identity | EVD-080 | strategy_plan candidate directions from transparent snapshot | `small_model_action.v1.parameters[direction_id]`, `strategy_plan_step.v1.direction_id` | static_covered / tests_not_run |
| `strategy.grandpa_progress.direction_domain` | compiler, budget_validator | yes for domain-specific time estimation | Validated for exact equality against live `CandidateDirection.Domain` from adapter; mismatches are rejected | EVD-080 | same candidate direction metadata | `small_model_action.v1.parameters[direction_domain]`, `strategy_plan_step.v1.domain` | static_covered / tests_not_run |
| `strategy.grandpa_progress.potential_points` | policy, compiler, trainer | yes for direction scoring | Validated for exact equality against live `CandidateDirection.PotentialPoints` from adapter; zero or negative potential is rejected | EVD-080 | grandpa evaluation factors from transparent snapshot | `small_model_action.v1.parameters[potential_points]`, `strategy_plan_step.v1.potential_points` | static_covered / tests_not_run |
| `strategy.grandpa_progress.priority_score` | policy, compiler, trainer | yes for direction ranking | Validated for exact equality against live `CandidateDirection.PriorityScore` from adapter | EVD-080 | same grandpa evaluation factors | `strategy_plan_step.v1.priority_score` | static_covered / tests_not_run |
| `strategy.grandpa_progress.feedback_key` | compiler, budget_validator | yes for feedback routing | Validated for exact equality against live `CandidateDirection.FeedbackKey` from adapter | EVD-080 | same candidate direction metadata | `strategy_plan_step.v1.feedback_key` | static_covered / tests_not_run |
| `strategy.grandpa_progress.required_minutes` | budget_validator, trainer | yes for time budget planning | Validated for exact equality against `GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(candidate)`; computed from live candidate domain, not trusted from model | EVD-080 | domain-based heuristic from candidate direction | `strategy_plan_step.v1.required_minutes`, `time_budget.v1.items[].estimated_minutes` | static_covered / tests_not_run |
| `strategy.grandpa_progress.strategic_goal` | policy, compiler | yes for candidate set rebuild | Required to be exactly `grandpa_four_candles_year3`; missing or any other value is blocked with precise reason; the compiler uses `grandpa_four_candles_year3` to rebuild the candidate set via `WorldModelProjector` | EVD-080 | N/A (constant goal id) | `small_model_action.v1.parameters[strategic_goal]` | static_covered / tests_not_run |
| `strategy.grandpa_progress.optional_minutes` | budget_validator | no (always 0) | Hardcoded to 0 in validated strategy steps until a transparent typed source exists; parameter must be present and exactly 0; missing or nonzero is rejected with precise reason | EVD-080 | N/A (always zero) | `strategy_plan_step.v1.optional_minutes` | static_covered / tests_not_run |
| `strategy.grandpa_progress.hard_preconditions` | executor | no (not yet transparent) | `CandidateDirection` has no typed source for hard preconditions; nonempty model-supplied values are rejected as unverified; validated steps emit empty array | EVD-080 | N/A (no transparent source) | `strategy_plan_step.v1.hard_preconditions` | static_covered / tests_not_run |
| `strategy.grandpa_progress.resource_budget` | executor | no (not yet transparent) | `CandidateDirection` has no typed source for resource budget; nonempty model-supplied values are rejected as unverified; validated steps emit empty array | EVD-080 | N/A (no transparent source) | `strategy_plan_step.v1.resource_budget` | static_covered / tests_not_run |
| `strategy.grandpa_progress.executor_handoff_option` | executor | no (not yet transparent) | `CandidateDirection` has no typed source for executor handoff; nonempty model-supplied values are rejected as unverified; validated steps emit empty string | EVD-080 | N/A (no transparent source) | `strategy_plan_step.v1.executor_handoff_option` | static_covered / tests_not_run |
| `strategy.grandpa_progress.fail_closed_direction` | compiler, trainer audit | yes for safe blocking | When no Known+unblocked+positive-potential direction exists, policy emits empty direction_id + block_reason; compiler rebuilds candidate set, finds no match, and blocks with `strategy_direction_absent` | EVD-080 | candidate directions + planner state from transparent snapshot | `small_model_action.v1.parameters[block_reason]`, `action_queue.v1.blocking_reasons` | static_covered / tests_not_run |
| All 12 known grandpa direction IDs | adapter coverage test | yes for completeness verification | Adapter's `BuildDirections()` is the sole authoritative source of direction IDs; the compiler validates against the live candidate set, not a static whitelist; test `All12DirectionIdsAreCoveredByAdapter` proves adapter coverage | EVD-080 | direction ID specs in adapter | `strategy_direction_absent:<id>_not_in_current_snapshot_candidate_set` blocking reason for non-matching IDs | static_covered / tests_not_run |

## Direction ID Coverage

All 12 direction IDs from the adapter are the authoritative source:

| # | Direction ID | Domain | Related Factors | Potential Points Range |
|---|---|---|---|---|
| 1 | earn_money | economy | money_50000..money_1000000 | 0-7 |
| 2 | complete_museum_collection | world_progress | achievement_complete_collection | 0-1 |
| 3 | obtain_skull_key | exploration | skull_key | 0-1 |
| 4 | complete_community_center | world_progress | community_center_access_or_completion, community_center_accessible_bonus | 0-2 |
| 5 | complete_joja_development | world_progress | joja_development_completed | 0-1 |
| 6 | marriage_and_house_upgrade | social | married_or_roommate_house_2 | 0-1 |
| 7 | obtain_rusty_key | world_progress | rusty_key | 0-1 |
| 8 | complete_master_angler | world_progress | achievement_master_angler | 0-1 |
| 9 | complete_full_shipment | economy | achievement_full_shipment | 0-1 |
| 10 | raise_friendships | social | friendships_5, friendships_10 | 0-2 |
| 11 | raise_skill_levels | skills | player_level_15, player_level_25 | 0-2 |
| 12 | earn_pet_love | farm | pet_love | 0-1 |

No new required model input field is missing or unavailable after this slice. All direction metadata flows from the same live `CandidateDirection` source through both policy generation and compiler validation. The compiler independently rebuilds the candidate set to validate, rather than trusting model-supplied values or a static whitelist.
