using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;

namespace StardewAI.Core.OptionRegistry
{
    public sealed class CandidateOptionAvailabilityEvaluator
    {
        private readonly OptionRegistry optionRegistry;
        private readonly Verifier.Verifier verifier;
        private readonly ActionQueueCompiler compiler;

        public CandidateOptionAvailabilityEvaluator()
            : this(new OptionRegistry(), new Verifier.Verifier())
        {
        }

        public CandidateOptionAvailabilityEvaluator(OptionRegistry optionRegistry, Verifier.Verifier verifier)
        {
            this.optionRegistry = optionRegistry;
            this.verifier = verifier;
            compiler = new ActionQueueCompiler(optionRegistry, verifier);
        }

        public OptionAvailabilityEnvelope Evaluate(
            SnapshotEnvelope snapshot,
            string[] candidateOptionIds,
            bool includeExecutorCalibrationOptions = false)
        {
            var candidates = candidateOptionIds.Length > 0
                ? candidateOptionIds.Select(optionId => new OptionAvailabilityCandidate { OptionId = optionId }).ToArray()
                : DefaultCandidates(includeExecutorCalibrationOptions);

            return Evaluate(snapshot, candidates, includeExecutorCalibrationOptions);
        }

        public OptionAvailabilityEnvelope Evaluate(
            SnapshotEnvelope snapshot,
            OptionAvailabilityCandidate[] candidates,
            bool includeExecutorCalibrationOptions = false)
        {
            var effectiveCandidates = candidates.Length > 0
                ? candidates
                : DefaultCandidates(includeExecutorCalibrationOptions);
            return new OptionAvailabilityEnvelope
            {
                StateHash = snapshot.StateHash,
                CurrentTime = ReadStateFieldInt(snapshot, "time", "time"),
                Options = effectiveCandidates.Select(candidate => EvaluateOne(snapshot, candidate)).ToArray()
            };
        }

        private OptionAvailabilityCandidate[] DefaultCandidates(bool includeExecutorCalibrationOptions)
        {
            return optionRegistry.All
                .Where(option => includeExecutorCalibrationOptions || option.TrainingRole != TrainingRoles.ExecutorCalibration)
                .Select(option => new OptionAvailabilityCandidate { OptionId = option.OptionId })
                .OrderBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .ToArray();
        }

        private OptionAvailability EvaluateOne(SnapshotEnvelope snapshot, OptionAvailabilityCandidate candidate)
        {
            OptionSpec option;
            try
            {
                option = optionRegistry.GetRequired(candidate.OptionId);
            }
            catch (KeyNotFoundException)
            {
                return new OptionAvailability
                {
                    OptionId = candidate.OptionId,
                    Parameters = candidate.Parameters,
                    Available = false,
                    Status = "blocked",
                    BlockingReasons = new[] { "unknown_option_id" },
                    HardBlockReasons = new[] { "unknown_option_id" },
                    AvailabilityNotes = new[] { "candidate_rejected_before_model_scoring" }
                };
            }

            var safety = verifier.Verify(snapshot, option);
            var reasons = new List<string>(safety.BlockingReasons);
            var notes = new List<string>();
            var compilerReasons = IsUnboundSocialCandidate(candidate)
                ? Array.Empty<string>()
                : CompilerProbeBlockingReasons(snapshot, candidate);
            var economicCandidates = EconomicCandidates(snapshot, option.OptionId);
            var eventCandidates = EventCandidates(snapshot, option.OptionId, safety.MissingStateFactors, candidate.Parameters);
            var socialCandidates = SocialCandidates(snapshot, option.OptionId, safety.MissingStateFactors);
            var valueReasons = safety.MissingStateFactors.Length == 0
                ? ValueGateBlockingReasons(snapshot, option.OptionId, economicCandidates)
                : Array.Empty<string>();
            var eventCandidateReasons = option.OptionId == "mining.reach_depth" || safety.MissingStateFactors.Length == 0
                ? EventCandidateGateBlockingReasons(option.OptionId, eventCandidates, candidate.Parameters.Length > 0)
                : Array.Empty<string>();
            var socialCandidateReasons = safety.MissingStateFactors.Length == 0
                ? SocialCandidateGateBlockingReasons(option.OptionId, socialCandidates, candidate.Parameters.Length > 0)
                : Array.Empty<string>();
            reasons.AddRange(compilerReasons);
            reasons.AddRange(valueReasons);
            reasons.AddRange(eventCandidateReasons);
            reasons.AddRange(socialCandidateReasons);
            var executorEnabled = IsExecutorEnabled(option.OptionId);
            var previewOnly = IsPreviewOnly(option.OptionId, option.TrainingRole, executorEnabled);

            if (!executorEnabled)
            {
                reasons.Add(ExecutorDisabledReason(option.OptionId));
            }

            if (previewOnly)
            {
                notes.Add("preview_only_candidate_not_runtime_executable");
            }

            if (option.TrainingRole == TrainingRoles.ExecutorCalibration)
            {
                notes.Add("executor_calibration_option_excluded_from_default_policy_ranking");
            }

            var hasMissingState = safety.MissingStateFactors.Length > 0;
            var hasParameterBlock = compilerReasons.Length > 0;
            var hasValueBlock = valueReasons.Length > 0;
            var hasEventCandidateBlock = eventCandidateReasons.Length > 0;
            var hasSocialCandidateBlock = socialCandidateReasons.Length > 0;
            var status = hasMissingState
                ? "blocked"
                : hasParameterBlock ? "blocked"
                : hasValueBlock ? "blocked"
                : hasEventCandidateBlock ? "blocked"
                : hasSocialCandidateBlock ? "blocked"
                : previewOnly ? "preview_available" : "available";

            return new OptionAvailability
            {
                OptionId = option.OptionId,
                Available = !hasMissingState && !hasParameterBlock && !hasValueBlock && !hasEventCandidateBlock && !hasSocialCandidateBlock && !previewOnly,
                Status = status,
                PreviewOnly = previewOnly,
                ExecutorEnabled = executorEnabled,
                TrainingRole = option.TrainingRole,
                BehaviorCategory = option.BehaviorCategory,
                CompilerResponsibility = option.CompilerResponsibility,
                RequiredStateFactors = option.RequiredStateFactors,
                Parameters = candidate.Parameters,
                MissingStateFactors = safety.MissingStateFactors,
                BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                HardBlockReasons = safety.BlockingReasons,
                PreconditionResults = safety.PreconditionResults,
                AvailabilityNotes = notes.ToArray(),
                EconomicCandidates = economicCandidates,
                EventCandidates = eventCandidates,
                SocialCandidates = socialCandidates
            };
        }

        private static string[] SocialCandidateGateBlockingReasons(string optionId, EventCandidate[] socialCandidates, bool hasBoundParameters)
        {
            if (optionId != "social.talk_npc" && optionId != "social.gift_npc")
            {
                return Array.Empty<string>();
            }

            if (hasBoundParameters)
            {
                return Array.Empty<string>();
            }

            return EventCandidateAvailabilityReasons(
                socialCandidates,
                "no_social_current_state_candidates",
                "no_available_social_current_state_candidates");
        }

        private static bool IsUnboundSocialCandidate(OptionAvailabilityCandidate candidate)
        {
            return candidate.Parameters.Length == 0 &&
                (candidate.OptionId == "social.talk_npc" || candidate.OptionId == "social.gift_npc");
        }

        private static string[] EventCandidateGateBlockingReasons(string optionId, EventCandidate[] eventCandidates, bool hasBoundParameters)
        {
            if (optionId == "mining.reach_depth")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_mining_reach_depth_candidates",
                    "no_available_mining_reach_depth_candidates");
            }

            if (hasBoundParameters)
            {
                return Array.Empty<string>();
            }

            if (optionId == "executor.interact")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_interact_endpoint_candidates",
                    "no_available_interact_endpoint_candidates");
            }

            if (optionId == "exploration.visit_location")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_route_connector_candidates",
                    "no_available_route_connector_candidates");
            }

            if (optionId == "executor.clear_obstacle")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_clear_obstacle_candidates",
                    "no_available_clear_obstacle_candidates");
            }

            if (optionId == "executor.plant_seed")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_plant_seed_candidates",
                    "no_available_plant_seed_candidates");
            }

            if (optionId == "fishing.catch_fish")
            {
                return EventCandidateAvailabilityReasons(
                    eventCandidates,
                    "no_fishing_candidates",
                    "no_available_fishing_candidates");
            }
            if (optionId == "quest.advance")
            {
                return QuestCandidateGateBlockingReasons(eventCandidates);
            }

            return Array.Empty<string>();
        }

        private static string[] QuestCandidateGateBlockingReasons(EventCandidate[] eventCandidates)
        {
            if (eventCandidates.Length == 0)
            {
                return new[] { "no_quest_current_state_candidates" };
            }

            if (eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return eventCandidates
                .SelectMany(candidate => candidate.BlockReasons)
                .Concat(new[] { "no_available_quest_current_state_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] EventCandidateAvailabilityReasons(EventCandidate[] eventCandidates, string emptyReason, string noneAvailableReason)
        {
            if (eventCandidates.Length == 0)
            {
                return new[] { emptyReason };
            }

            if (eventCandidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return eventCandidates
                .SelectMany(candidate => candidate.BlockReasons)
                .Concat(new[] { noneAvailableReason })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate[] EventCandidates(SnapshotEnvelope snapshot, string optionId, string[] missingStateFactors, SmallModelActionParameter[] parameters)
        {
            if (missingStateFactors.Length > 0)
            {
                return Array.Empty<EventCandidate>();
            }

            if (optionId == "farm.maintain_crops")
            {
                return FarmMaintenanceCandidates(snapshot);
            }

            if (optionId == "farm.process_machines")
            {
                return MachineProcessingCandidates(snapshot);
            }

            if (optionId == "executor.clear_obstacle")
            {
                return ClearObstacleCandidates(snapshot);
            }

            if (optionId == "executor.plant_seed")
            {
                return PlantSeedCandidates(snapshot);
            }

            if (optionId == "exploration.visit_location")
            {
                return RouteConnectorCandidates(snapshot);
            }

            if (optionId == "executor.interact")
            {
                return InteractEndpointCandidates(snapshot);
            }

            if (optionId == "recovery.stabilize_day")
            {
                return RecoveryCandidates(snapshot);
            }

            if (optionId == "fishing.catch_fish")
            {
                return FishingEventCandidateBuilder.Build(snapshot);
            }

            if (optionId == "mining.reach_depth")
            {
                return MiningReachDepthCandidateBuilder.Build(snapshot, parameters);
            }

            if (optionId == "quest.advance")
            {
                return QuestCandidates(snapshot);
            }

            if (optionId == "economy.ship_items")
            {
                return ShipCandidates(snapshot);
            }

            return Array.Empty<EventCandidate>();
        }

        private static EventCandidate[] SocialCandidates(SnapshotEnvelope snapshot, string optionId, string[] missingStateFactors)
        {
            return missingStateFactors.Any(factor => factor != "npcs.schedules")
                ? Array.Empty<EventCandidate>()
                : SocialCandidateBuilder.Build(snapshot, optionId);
        }

        private EventCandidate[] RecoveryCandidates(SnapshotEnvelope snapshot)
        {
            var candidates = new List<EventCandidate>();
            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                var closeMenuReasons = CloseMenuCandidateBlockReasons(snapshot);
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:close_blocking_menu",
                    Kind = "recovery_close_menu",
                    Available = closeMenuReasons.Length == 0,
                    LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                    ExpectedEffect = "menu_not_blocking_execution",
                    EstimatedTicks = 10,
                    BlockReasons = closeMenuReasons
                });
            }

            if (time >= 2400)
            {
                var homeContext = ReadStateFieldValue(snapshot, "current_location", "home_context");
                var homeLocation = homeContext.HasValue ? ReadString(homeContext.Value, "home_location_id") : string.Empty;
                var currentLocationIsHome = homeContext.HasValue && ReadBool(homeContext.Value, "current_location_is_home") == true;
                var bedX = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_x") ? ReadInt(homeContext.Value, "bed_tile_x") : 0;
                var bedY = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_y") ? ReadInt(homeContext.Value, "bed_tile_y") : 0;
                var bedTileHasBed = homeContext.HasValue && ReadBool(homeContext.Value, "bed_tile_has_bed") == true;
                var bedStandTile = currentLocationIsHome ? FindBestStandTile(snapshot, bedX, bedY) : null;
                var sleepImmediatelyBlocks = new List<string>();
                if (!homeContext.HasValue || string.IsNullOrWhiteSpace(homeLocation))
                {
                    sleepImmediatelyBlocks.Add("recovery_home_route_target_unavailable");
                }
                else if (currentLocationIsHome)
                {
                    if (ActiveMenuOpenForCandidate(snapshot))
                    {
                        sleepImmediatelyBlocks.Add("sleep_prompt_menu_must_be_clear");
                    }

                    if (SleepPromptOpenForCandidate(snapshot))
                    {
                        sleepImmediatelyBlocks.Add("recovery_sleep_prompt_already_open");
                    }

                    if (!bedTileHasBed)
                    {
                        sleepImmediatelyBlocks.Add("recovery_bed_tile_not_confirmed");
                    }
                    else if (bedStandTile is null)
                    {
                        sleepImmediatelyBlocks.Add("recovery_bed_adjacent_stand_tile_unavailable");
                    }
                    else
                    {
                        sleepImmediatelyBlocks.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "exploration.visit_location",
                            Parameters = new[]
                            {
                                Parameter("target_tile_x", bedStandTile.X.ToString()),
                                Parameter("target_tile_y", bedStandTile.Y.ToString())
                            }
                        }));
                    }
                }
                else
                {
                    sleepImmediatelyBlocks.Add("recovery_cross_map_home_route_unverified");
                }

                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:sleep_immediately",
                    Kind = "recovery_sleep_immediately",
                    Available = sleepImmediatelyBlocks.Count == 0,
                    LocationId = string.IsNullOrWhiteSpace(homeLocation) ? ReadStateFieldString(snapshot, "player", "location_id") : homeLocation,
                    TileX = currentLocationIsHome ? bedStandTile?.X : null,
                    TileY = currentLocationIsHome ? bedStandTile?.Y : null,
                    ExpectedEffect = currentLocationIsHome
                        ? bedStandTile is null
                            ? "bed_tile=" + bedX + "," + bedY + ";sleep_not_executed"
                            : "move_to_bed_adjacent=" + bedStandTile.X + "," + bedStandTile.Y + ";step_onto_sleep_touch_tile=" + bedX + "," + bedY + ";touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed"
                        : "route_to_home=" + homeLocation + ";sleep_not_executed",
                    EstimatedTicks = currentLocationIsHome ? 240 : 900,
                    BlockReasons = sleepImmediatelyBlocks.Distinct(StringComparer.Ordinal).ToArray()
                });
                return candidates.ToArray();
            }

            if (time >= 2200)
            {
                var homeContext = ReadStateFieldValue(snapshot, "current_location", "home_context");
                var homeLocation = homeContext.HasValue ? ReadString(homeContext.Value, "home_location_id") : string.Empty;
                var currentLocationIsHome = homeContext.HasValue && ReadBool(homeContext.Value, "current_location_is_home") == true;
                var bedX = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_x") ? ReadInt(homeContext.Value, "bed_tile_x") : 0;
                var bedY = homeContext.HasValue && HasNumber(homeContext.Value, "bed_tile_y") ? ReadInt(homeContext.Value, "bed_tile_y") : 0;
                var bedTileHasBed = homeContext.HasValue && ReadBool(homeContext.Value, "bed_tile_has_bed") == true;
                var bedStandTile = currentLocationIsHome ? FindBestStandTile(snapshot, bedX, bedY) : null;
                var returnHomeBlocks = new List<string>();
                if (!homeContext.HasValue || string.IsNullOrWhiteSpace(homeLocation))
                {
                    returnHomeBlocks.Add("recovery_home_route_target_unavailable");
                }
                else if (currentLocationIsHome)
                {
                    if (ActiveMenuOpenForCandidate(snapshot))
                    {
                        returnHomeBlocks.Add("sleep_prompt_menu_must_be_clear");
                    }

                    if (SleepPromptOpenForCandidate(snapshot))
                    {
                        returnHomeBlocks.Add("recovery_sleep_prompt_already_open");
                    }

                    if (!bedTileHasBed)
                    {
                        returnHomeBlocks.Add("recovery_bed_tile_not_confirmed");
                    }
                    else if (bedStandTile is null)
                    {
                        returnHomeBlocks.Add("recovery_bed_adjacent_stand_tile_unavailable");
                    }
                    else
                    {
                        returnHomeBlocks.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                        {
                            OptionId = "exploration.visit_location",
                            Parameters = new[]
                            {
                                Parameter("target_tile_x", bedStandTile.X.ToString()),
                                Parameter("target_tile_y", bedStandTile.Y.ToString())
                            }
                        }));
                    }
                }
                else
                {
                    returnHomeBlocks.Add("recovery_cross_map_home_route_unverified");
                }

                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:return_home",
                    Kind = "recovery_return_home",
                    Available = returnHomeBlocks.Count == 0,
                    LocationId = string.IsNullOrWhiteSpace(homeLocation) ? ReadStateFieldString(snapshot, "player", "location_id") : homeLocation,
                    TileX = currentLocationIsHome ? bedStandTile?.X : null,
                    TileY = currentLocationIsHome ? bedStandTile?.Y : null,
                    ExpectedEffect = currentLocationIsHome
                        ? bedStandTile is null
                            ? "bed_tile=" + bedX + "," + bedY + ";sleep_not_executed"
                            : "move_to_bed_adjacent=" + bedStandTile.X + "," + bedStandTile.Y + ";step_onto_sleep_touch_tile=" + bedX + "," + bedY + ";touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed"
                        : "route_to_home=" + homeLocation + ";sleep_not_executed",
                    EstimatedTicks = currentLocationIsHome ? 240 : 900,
                    BlockReasons = returnHomeBlocks.Distinct(StringComparer.Ordinal).ToArray()
                });
                candidates.Add(new EventCandidate
                {
                    CandidateId = "recovery:sleep_before_collapse",
                    Kind = "recovery_sleep_before_collapse",
                    Available = false,
                    LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                    ExpectedEffect = "day_safely_ended",
                    EstimatedTicks = 120,
                    BlockReasons = new[] { "recovery_terminal_sleep_already_covered_by_return_home" }
                });
                return candidates.ToArray();
            }

            candidates.Add(new EventCandidate
            {
                CandidateId = "recovery:refresh_plan_after_stabilization",
                Kind = "recovery_refresh_plan",
                Available = !ActiveMenuOpenForCandidate(snapshot),
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "executor.wait_ticks=30;urgent_risks_rechecked",
                EstimatedTicks = 30,
                EnergyCost = 0,
                BlockReasons = ActiveMenuOpenForCandidate(snapshot) ? new[] { "intervening_menu_must_be_cleared_first" } : Array.Empty<string>()
            });
            return candidates.ToArray();
        }

        private EventCandidate[] InteractEndpointCandidates(SnapshotEnvelope snapshot)
        {
            var shopActionTiles = ReadStateFieldValue(snapshot, "current_location", "shop_action_tiles");
            if (!shopActionTiles.HasValue || shopActionTiles.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            return shopActionTiles.Value.EnumerateArray()
                .Where(tile => tile.ValueKind == JsonValueKind.Object && HasNumber(tile, "tile_x") && HasNumber(tile, "tile_y"))
                .Select(tile => InteractEndpointCandidate(snapshot, tile, locationId))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY ?? 0)
                .ThenBy(candidate => candidate.TileX ?? 0)
                .Take(32)
                .ToArray();
        }

        private EventCandidate InteractEndpointCandidate(SnapshotEnvelope snapshot, JsonElement tile, string locationId)
        {
            var x = ReadInt(tile, "tile_x");
            var y = ReadInt(tile, "tile_y");
            var parsed = tile.TryGetProperty("parsed", out var parsedValue) && parsedValue.ValueKind == JsonValueKind.Object
                ? parsedValue
                : default;
            var parsedKind = parsed.ValueKind == JsonValueKind.Object ? ReadString(parsed, "kind") : string.Empty;
            var expectedActionType = parsedKind switch
            {
                "legacy_buy" => "Buy",
                "joja_shop" => "JojaShop",
                "dialogue_shop" => ReadString(tile, "action").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                "direct_or_dialogue_shop" => ReadString(tile, "action").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                _ => "OpenShop"
            };
            if (string.IsNullOrWhiteSpace(expectedActionType))
            {
                expectedActionType = "OpenShop";
            }

            var standTile = FindBestStandTile(snapshot, x, y);
            var reasons = new List<string>();
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                reasons.Add("interact_menu_must_be_clear");
            }

            if (RouteActionBranchBlockedAtCandidateTile(snapshot, x, y))
            {
                reasons.Add("interact_unsupported_action_branch_at_target");
            }

            if (!TargetActionBranchMatchesForCandidate(snapshot, x, y, expectedActionType))
            {
                reasons.Add("interact_expected_action_type_mismatch");
            }

            var ownerNpc = parsed.ValueKind == JsonValueKind.Object
                ? ReadString(parsed, "owner_npc")
                : string.Empty;
            var ownerServiceArea = parsed.ValueKind == JsonValueKind.Object && parsed.TryGetProperty("owner_service_area", out var area) && area.ValueKind == JsonValueKind.Object
                ? area
                : default;
            var ownerServiceStatus = tile.TryGetProperty("owner_service_status", out var status) && status.ValueKind == JsonValueKind.Object
                ? status
                : default;
            if (OwnerServiceStatusBlocks(ownerServiceStatus) ||
                (ownerServiceStatus.ValueKind != JsonValueKind.Object &&
                    !string.IsNullOrWhiteSpace(ownerNpc) &&
                    !CurrentLocationContainsNpcAtServiceCounter(snapshot, ownerNpc, x, y, ownerServiceArea)))
            {
                reasons.Add("interact_shop_owner_npc_not_at_service_counter");
            }

            var serviceTimeStatus = tile.TryGetProperty("service_time_status", out var timeStatus) && timeStatus.ValueKind == JsonValueKind.Object
                ? timeStatus
                : default;
            if (ServiceTimeStatusBlocks(serviceTimeStatus))
            {
                reasons.Add("interact_shop_service_time_blocked");
            }

            if (standTile is null)
            {
                reasons.Add("interact_no_adjacent_route_stand_tile");
            }
            else
            {
                reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", standTile.X.ToString()),
                        Parameter("target_tile_y", standTile.Y.ToString())
                    }
                }));
            }

            var shopId = parsed.ValueKind == JsonValueKind.Object
                ? ReadString(parsed, "shop_id")
                : string.Empty;
            var distance = standTile is not null
                ? Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - standTile.X) + Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - standTile.Y)
                : 0;
            var currentTime = NullableReadInt(serviceTimeStatus, "current_time") ?? ReadStateFieldInt(snapshot, "time", "time");
            var openTime = NullableReadInt(serviceTimeStatus, "open_time");
            var effectiveOpenTime = NullableReadInt(serviceTimeStatus, "effective_open_time") ?? openTime;
            var closeTime = NullableReadInt(serviceTimeStatus, "close_time");
            var allowedNow = serviceTimeStatus.ValueKind == JsonValueKind.Object
                ? ReadBool(serviceTimeStatus, "allowed_now")
                : (bool?)(reasons.Count == 0);
            var allowedToday = ServiceCouldOpenToday(serviceTimeStatus, currentTime);
            var waitCost = WaitCostTicks(currentTime, effectiveOpenTime, allowedNow, allowedToday);
            var gateReasons = ServiceTimeBlockReasons(serviceTimeStatus)
                .Concat(OwnerServiceBlockReason(ownerServiceStatus))
                .Concat(reasons.Where(reason =>
                    reason.StartsWith("interact_shop_", StringComparison.Ordinal) ||
                    reason.StartsWith("interact_unsupported_", StringComparison.Ordinal) ||
                    reason.StartsWith("interact_expected_", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var suffix = string.IsNullOrWhiteSpace(shopId) ? expectedActionType : expectedActionType + ":" + shopId;
            return new EventCandidate
            {
                CandidateId = "interact:" + locationId + ":" + x + "," + y + ":" + suffix,
                Kind = "interact_endpoint",
                Available = reasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = standTile is not null
                    ? "move_to_adjacent=" + standTile.X + "," + standTile.Y + ";preview_interact=" + expectedActionType
                    : "preview_interact=" + expectedActionType,
                ShopId = shopId,
                EstimatedTicks = standTile is not null ? Math.Max(30, distance * 60 + 30) : 30,
                EnergyCost = 0,
                AvailabilityClass = serviceTimeStatus.ValueKind == JsonValueKind.Object && ReadBool(serviceTimeStatus, "time_gate_known") == true
                    ? "windowed_available"
                    : "state_gated",
                AllowedNow = allowedNow,
                AllowedToday = allowedToday,
                NextOpenTime = allowedNow == false && allowedToday == true ? effectiveOpenTime : null,
                EffectiveOpenTime = effectiveOpenTime,
                ClosesAt = closeTime,
                WaitCost = waitCost,
                GateReasons = gateReasons,
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).Where(reason => reason != "queue_global_compiler_block").ToArray()
            };
        }

        private static bool? ServiceCouldOpenToday(JsonElement serviceTimeStatus, int currentTime)
        {
            if (serviceTimeStatus.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (ReadBool(serviceTimeStatus, "allowed_now") == true)
            {
                return true;
            }

            var openTime = NullableReadInt(serviceTimeStatus, "effective_open_time") ?? NullableReadInt(serviceTimeStatus, "open_time");
            var closeTime = NullableReadInt(serviceTimeStatus, "close_time");
            if (!openTime.HasValue || !closeTime.HasValue)
            {
                return null;
            }

            return currentTime < closeTime.Value;
        }

        private static int? WaitCostTicks(int currentTime, int? effectiveOpenTime, bool? allowedNow, bool? allowedToday)
        {
            if (allowedNow == true || allowedToday != true || !effectiveOpenTime.HasValue || currentTime >= effectiveOpenTime.Value)
            {
                return null;
            }

            return Math.Max(0, GameTimeMinutesBetween(currentTime, effectiveOpenTime.Value) * 60);
        }

        private static int GameTimeMinutesBetween(int fromTime, int toTime)
        {
            var fromHours = fromTime / 100;
            var fromMinutes = fromTime % 100;
            var toHours = toTime / 100;
            var toMinutes = toTime % 100;
            return Math.Max(0, (toHours * 60 + toMinutes) - (fromHours * 60 + fromMinutes));
        }

        private static IEnumerable<string> ServiceTimeBlockReasons(JsonElement serviceTimeStatus)
        {
            if (serviceTimeStatus.ValueKind != JsonValueKind.Object ||
                !serviceTimeStatus.TryGetProperty("block_reasons", out var blockReasons) ||
                blockReasons.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return blockReasons.EnumerateArray()
                .Where(reason => reason.ValueKind == JsonValueKind.String)
                .Select(reason => reason.GetString() ?? string.Empty)
                .Where(reason => !string.IsNullOrWhiteSpace(reason));
        }

        private static IEnumerable<string> OwnerServiceBlockReason(JsonElement ownerServiceStatus)
        {
            var reason = ReadString(ownerServiceStatus, "block_reason");
            return string.IsNullOrWhiteSpace(reason) ? Array.Empty<string>() : new[] { reason };
        }

        private static bool OwnerServiceStatusBlocks(JsonElement ownerServiceStatus)
        {
            if (ownerServiceStatus.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return ReadBool(ownerServiceStatus, "owner_required") == true &&
                ReadBool(ownerServiceStatus, "in_service_area") == false;
        }

        private static bool ServiceTimeStatusBlocks(JsonElement serviceTimeStatus)
        {
            return serviceTimeStatus.ValueKind == JsonValueKind.Object &&
                ReadBool(serviceTimeStatus, "allowed_now") == false;
        }

        private static bool CurrentLocationContainsNpcAtServiceCounter(SnapshotEnvelope snapshot, string npcName, int actionTileX, int actionTileY, JsonElement ownerServiceArea)
        {
            var positions = ReadStateFieldValue(snapshot, "npcs", "positions");
            if (!positions.HasValue || positions.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return positions.Value.EnumerateArray().Any(npc =>
                npc.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(npc, "name"), npcName, StringComparison.OrdinalIgnoreCase) &&
                IsNpcAtShopServiceCounter(npc, actionTileX, actionTileY, ownerServiceArea));
        }

        private static bool IsNpcAtShopServiceCounter(JsonElement npc, int actionTileX, int actionTileY, JsonElement ownerServiceArea)
        {
            var npcX = ReadInt(npc, "tile_x");
            var npcY = ReadInt(npc, "tile_y");
            if (ownerServiceArea.ValueKind == JsonValueKind.Object)
            {
                var areaX = ReadInt(ownerServiceArea, "x");
                var areaY = ReadInt(ownerServiceArea, "y");
                var areaWidth = ReadInt(ownerServiceArea, "width");
                var areaHeight = ReadInt(ownerServiceArea, "height");
                if (areaWidth > 0 && areaHeight > 0)
                {
                    return npcX >= areaX && npcX < areaX + areaWidth &&
                        npcY >= areaY && npcY < areaY + areaHeight;
                }
            }

            var distance = Math.Abs(npcX - actionTileX) + Math.Abs(npcY - actionTileY);
            return distance <= 2 && npcY <= actionTileY;
        }

        private static CandidateTile? FindBestStandTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return new[]
                {
                    new CandidateTile(targetX + 1, targetY),
                    new CandidateTile(targetX - 1, targetY),
                    new CandidateTile(targetX, targetY + 1),
                    new CandidateTile(targetX, targetY - 1)
                }
                .Where(tile => !CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .FirstOrDefault();
        }

        private MachineStandTileSelection FindBestMachineStandTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var occupiedMachineTiles = FarmMachineTileKeys(snapshot);
            var candidates = new[]
                {
                    new CandidateTile(targetX + 1, targetY),
                    new CandidateTile(targetX - 1, targetY),
                    new CandidateTile(targetX, targetY + 1),
                    new CandidateTile(targetX, targetY - 1)
                }
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .ToArray();
            var sawMachineOccupied = false;
            var sawCollisionBlocked = false;
            var sawCompilerBlocked = false;
            foreach (var tile in candidates)
            {
                if (occupiedMachineTiles.Contains(TileKey(tile.X, tile.Y)))
                {
                    sawMachineOccupied = true;
                    continue;
                }

                if (CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
                {
                    sawCollisionBlocked = true;
                    continue;
                }

                var compilerBlocks = CompilerProbeBlockingReasons(snapshot, MachineStandTileProbeCandidate(snapshot, tile))
                    .Where(reason => reason != "missing_required_state")
                    .ToArray();
                if (compilerBlocks.Length > 0)
                {
                    sawCompilerBlocked = true;
                    continue;
                }

                return new MachineStandTileSelection(tile, Array.Empty<string>());
            }

            var reasons = new List<string>();
            if (sawMachineOccupied)
            {
                reasons.Add("machine_adjacent_stand_tile_occupied_by_machine");
            }
            if (sawCollisionBlocked)
            {
                reasons.Add("machine_adjacent_stand_tile_blocked_by_collision_grid");
            }
            if (sawCompilerBlocked)
            {
                reasons.Add("machine_adjacent_stand_tile_blocked_by_movement_compiler_probe");
            }
            if (reasons.Count == 0)
            {
                reasons.Add("machine_adjacent_stand_tile_unavailable");
            }

            return new MachineStandTileSelection(null, reasons.ToArray());
        }

        private static ISet<string> FarmMachineTileKeys(SnapshotEnvelope snapshot)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var machine in machines.Value.EnumerateArray())
            {
                if (machine.ValueKind == JsonValueKind.Object)
                {
                    result.Add(TileKey(ReadInt(machine, "tile_x"), ReadInt(machine, "tile_y")));
                }
            }

            return result;
        }

        private static OptionAvailabilityCandidate MachineStandTileProbeCandidate(SnapshotEnvelope snapshot, CandidateTile tile)
        {
            if (RoutePathPreviewAvailable(snapshot))
            {
                return new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", tile.X.ToString()),
                        Parameter("target_tile_y", tile.Y.ToString()),
                        Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id"))
                    }
                };
            }

            return new OptionAvailabilityCandidate
            {
                OptionId = "executor.move_to_tile",
                Parameters = new[]
                {
                    Parameter("target_tile_x", tile.X.ToString()),
                    Parameter("target_tile_y", tile.Y.ToString())
                }
            };
        }

        private static bool RoutePathPreviewAvailable(SnapshotEnvelope snapshot)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            return grid.HasValue &&
                grid.Value.ValueKind == JsonValueKind.Object &&
                ReadInt(grid.Value, "width") > 0 &&
                ReadInt(grid.Value, "height") > 0;
        }

        private static bool ActiveMenuOpenForCandidate(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return false;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                return !string.Equals(activeMenu.Value.GetString(), "none", StringComparison.OrdinalIgnoreCase);
            }

            return activeMenu.Value.ValueKind == JsonValueKind.Object && ReadBool(activeMenu.Value, "is_open") == true;
        }

        private static bool SleepPromptOpenForCandidate(SnapshotEnvelope snapshot)
        {
            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            return prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open") == true;
        }

        private static string[] CloseMenuCandidateBlockReasons(SnapshotEnvelope snapshot)
        {
            if (SleepPromptOpenForCandidate(snapshot))
            {
                return new[] { "close_menu_sleep_prompt_unsupported" };
            }

            var type = ActiveMenuTypeForCandidate(snapshot);
            if (string.IsNullOrWhiteSpace(type))
            {
                return new[] { "close_menu_type_unknown" };
            }

            return IsSafeCloseMenuType(type)
                ? Array.Empty<string>()
                : new[] { "close_menu_type_not_whitelisted" };
        }

        private static string ActiveMenuTypeForCandidate(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return string.Empty;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                var value = activeMenu.Value.GetString() ?? string.Empty;
                return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
            }

            return activeMenu.Value.ValueKind == JsonValueKind.Object ? ReadString(activeMenu.Value, "type") : string.Empty;
        }

        private static bool IsSafeCloseMenuType(string type)
        {
            return type is "GameMenu" or "InventoryMenu" or "QuestLog" or "MapPage" or "ProfileMenu" or "ShopMenu";
        }

        private static bool RouteActionBranchBlockedAtCandidateTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var row = ReadRouteActionBranchCandidateRow(snapshot, targetX, targetY);
            return row.HasValue && ReadBool(row.Value, "route_training_blocked") == true;
        }

        private static bool TargetActionBranchMatchesForCandidate(SnapshotEnvelope snapshot, int targetX, int targetY, string expectedActionType)
        {
            var row = ReadRouteActionBranchCandidateRow(snapshot, targetX, targetY);
            return row.HasValue && string.Equals(ReadString(row.Value, "branch"), expectedActionType, StringComparison.OrdinalIgnoreCase);
        }

        private static JsonElement? ReadRouteActionBranchCandidateRow(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "tile_x") == targetX && ReadInt(row, "tile_y") == targetY)
                {
                    return row;
                }
            }

            return null;
        }

        private static bool CollisionGridBlocksTile(SnapshotEnvelope snapshot, int x, int y)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width > 0 && height > 0 && (x < 0 || y < 0 || x >= width || y >= height))
            {
                return true;
            }

            if (!grid.Value.TryGetProperty("notable_tiles", out var notableTiles) || notableTiles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object &&
                    ReadInt(tile, "tile_x") == x &&
                    ReadInt(tile, "tile_y") == y &&
                    ReadBool(tile, "collision_blocked") == true)
                {
                    return true;
                }
            }

            return false;
        }

        private static int DirectionFromTo(int fromX, int fromY, int toX, int toY)
        {
            if (toX > fromX)
            {
                return 1;
            }

            if (toX < fromX)
            {
                return 3;
            }

            return toY > fromY ? 2 : 0;
        }

        private sealed class CandidateTile
        {
            public CandidateTile(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private readonly struct MachineStandTileSelection
        {
            public MachineStandTileSelection(CandidateTile? tile, string[] blockReasons)
            {
                Tile = tile;
                BlockReasons = blockReasons;
            }

            public CandidateTile? Tile { get; }
            public string[] BlockReasons { get; }
        }

        private EventCandidate[] RouteConnectorCandidates(SnapshotEnvelope snapshot)
        {
            var routeConnectors = ReadStateFieldValue(snapshot, "locations", "route_connectors");
            if (!routeConnectors.HasValue ||
                routeConnectors.Value.ValueKind != JsonValueKind.Object ||
                !routeConnectors.Value.TryGetProperty("connectors", out var connectors) ||
                connectors.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadString(routeConnectors.Value, "location_id");
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = ReadStateFieldString(snapshot, "player", "location_id");
            }

            var startX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var startY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var routeCandidates = connectors.EnumerateArray()
                .Where(connector => connector.ValueKind == JsonValueKind.Object && HasNumber(connector, "tile_x") && HasNumber(connector, "tile_y"))
                .Select(connector => RouteConnectorCandidate(snapshot, connector, locationId, startX, startY))
                .ToArray();
            return routeCandidates
                .Concat(RouteRepairClearObstacleCandidates(snapshot, routeCandidates))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY ?? 0)
                .ThenBy(candidate => candidate.TileX ?? 0)
                .Take(32)
                .ToArray();
        }

        private EventCandidate RouteConnectorCandidate(SnapshotEnvelope snapshot, JsonElement connector, string locationId, int startX, int startY)
        {
            var x = ReadInt(connector, "tile_x");
            var y = ReadInt(connector, "tile_y");
            var kind = ReadString(connector, "kind");
            var targetLocation = ReadString(connector, "target_location");
            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("target_tile_x", x.ToString()),
                Parameter("target_tile_y", y.ToString())
            };
            if (!string.IsNullOrWhiteSpace(targetLocation))
            {
                parameters.Add(Parameter("target_location", targetLocation));
            }

            var blockReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "exploration.visit_location",
                Parameters = parameters.ToArray()
            });
            var distance = Math.Abs(startX - x) + Math.Abs(startY - y);
            return new EventCandidate
            {
                CandidateId = "route:" + locationId + ":" + x + "," + y + ":" + kind,
                Kind = "route_connector_tile",
                Available = blockReasons.Length == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = "player.tile=" + x + "," + y + ";route_connector=" + kind,
                EstimatedTicks = Math.Max(60, distance * 60),
                EnergyCost = 0,
                BlockReasons = blockReasons
            };
        }

        private EventCandidate[] RouteRepairClearObstacleCandidates(SnapshotEnvelope snapshot, IEnumerable<EventCandidate> routeCandidates)
        {
            var blockedRouteTargets = routeCandidates
                .Where(candidate => candidate.TileX.HasValue &&
                    candidate.TileY.HasValue &&
                    candidate.BlockReasons.Any(RouteBlockedByCollision))
                .ToArray();
            if (blockedRouteTargets.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var clearCandidates = ClearObstacleCandidates(snapshot)
                .Where(candidate => candidate.Available && candidate.TileX.HasValue && candidate.TileY.HasValue)
                .ToArray();
            if (clearCandidates.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            return blockedRouteTargets
                .SelectMany(route => clearCandidates
                    .Where(clear => ClearCandidateRepairsRoute(snapshot, route, clear))
                    .Select(clear => new EventCandidate
                    {
                        CandidateId = "route-repair:" + route.CandidateId + ":" + clear.CandidateId,
                        Kind = clear.Kind,
                        Available = true,
                        LocationId = clear.LocationId,
                        TileX = clear.TileX,
                        TileY = clear.TileY,
                        ExpectedEffect = "route_repair_for=" + route.CandidateId + ";" + clear.ExpectedEffect,
                        EstimatedTicks = clear.EstimatedTicks,
                        EnergyCost = clear.EnergyCost,
                        AvailabilityClass = "route_repair_clearable_obstacle",
                        BlockReasons = Array.Empty<string>()
                    }))
                .ToArray();
        }

        private static bool ClearCandidateRepairsRoute(SnapshotEnvelope snapshot, EventCandidate route, EventCandidate clear)
        {
            if (!route.TileX.HasValue || !route.TileY.HasValue || !clear.TileX.HasValue || !clear.TileY.HasValue)
            {
                return false;
            }

            var startX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var startY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var blocked = ReadBlockedCollisionTileKeys(grid.Value);
            blocked.Remove(TileKey(clear.TileX.Value, clear.TileY.Value));
            return PathExists(startX, startY, route.TileX.Value, route.TileY.Value, width, height, blocked, ReadUnsupportedRouteActionTileKeys(snapshot));
        }

        private static bool RouteBlockedByCollision(string reason)
        {
            return reason is "route_path_target_blocked_by_collision_grid" or
                "route_path_blocked_by_collision_grid" or
                "route_graph_start_connector_blocked_by_collision_grid" or
                "route_graph_start_segment_blocked_by_collision_grid";
        }

        private static HashSet<string> ReadBlockedCollisionTileKeys(JsonElement collisionGrid)
        {
            var blockedTiles = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty("notable_tiles", out var notableTiles) || notableTiles.ValueKind != JsonValueKind.Array)
            {
                return blockedTiles;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object && ReadBool(tile, "collision_blocked") == true)
                {
                    blockedTiles.Add(TileKey(ReadInt(tile, "tile_x"), ReadInt(tile, "tile_y")));
                }
            }

            return blockedTiles;
        }

        private static HashSet<string> ReadUnsupportedRouteActionTileKeys(SnapshotEnvelope snapshot)
        {
            var unsupportedTiles = new HashSet<string>(StringComparer.Ordinal);
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return unsupportedTiles;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object && ReadBool(row, "route_training_blocked") == true)
                {
                    unsupportedTiles.Add(TileKey(ReadInt(row, "tile_x"), ReadInt(row, "tile_y")));
                }
            }

            return unsupportedTiles;
        }

        private static bool PathExists(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (!TileInBounds(startX, startY, width, height) ||
                !TileInBounds(targetX, targetY, width, height) ||
                blockedTiles.Contains(startKey) ||
                blockedTiles.Contains(targetKey) ||
                extraBlockedTiles.Contains(startKey) ||
                extraBlockedTiles.Contains(targetKey))
            {
                return false;
            }

            var queue = new Queue<CandidateTile>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startKey };
            queue.Enqueue(new CandidateTile(startX, startY));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return true;
                }

                foreach (var next in AdjacentTiles(current.X, current.Y))
                {
                    var key = TileKey(next.X, next.Y);
                    if (!TileInBounds(next.X, next.Y, width, height) ||
                        blockedTiles.Contains(key) ||
                        extraBlockedTiles.Contains(key) ||
                        !seen.Add(key))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static IEnumerable<CandidateTile> AdjacentTiles(int x, int y)
        {
            yield return new CandidateTile(x + 1, y);
            yield return new CandidateTile(x - 1, y);
            yield return new CandidateTile(x, y + 1);
            yield return new CandidateTile(x, y - 1);
        }

        private static bool TileInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private static string TileKey(int x, int y)
        {
            return x.ToString() + "," + y.ToString();
        }

        private EventCandidate[] FarmMaintenanceCandidates(SnapshotEnvelope snapshot)
        {
            return WateringCandidates(snapshot)
                .Concat(HarvestCropCandidates(snapshot))
                .Concat(HarvestGiantCropCandidates(snapshot))
                .Concat(PickupDebrisCandidates(snapshot))
                .Concat(PlantSeedCandidates(snapshot).Select(candidate => new EventCandidate
                {
                    CandidateId = "farm-maintenance:" + candidate.CandidateId,
                    Kind = candidate.Kind,
                    Available = candidate.Available,
                    LocationId = candidate.LocationId,
                    TileX = candidate.TileX,
                    TileY = candidate.TileY,
                    ExpectedEffect = "farm_maintenance_plant_seed=true;" + candidate.ExpectedEffect,
                    ItemId = candidate.ItemId,
                    QualifiedItemId = candidate.QualifiedItemId,
                    SlotIndex = candidate.SlotIndex,
                    Quantity = candidate.Quantity,
                    ShopId = candidate.ShopId,
                    EstimatedTicks = candidate.EstimatedTicks,
                    EnergyCost = candidate.EnergyCost,
                    AvailabilityClass = candidate.AvailabilityClass,
                    AllowedNow = candidate.AllowedNow,
                    AllowedToday = candidate.AllowedToday,
                    NextOpenTime = candidate.NextOpenTime,
                    EffectiveOpenTime = candidate.EffectiveOpenTime,
                    ClosesAt = candidate.ClosesAt,
                    WaitCost = candidate.WaitCost,
                    GateReasons = candidate.GateReasons,
                    BlockReasons = candidate.BlockReasons
                }))
                .Concat(ClearObstacleCandidates(snapshot).Select(candidate => new EventCandidate
                {
                    CandidateId = "farm-maintenance:" + candidate.CandidateId,
                    Kind = candidate.Kind,
                    Available = candidate.Available,
                    LocationId = candidate.LocationId,
                    TileX = candidate.TileX,
                    TileY = candidate.TileY,
                    ExpectedEffect = "farm_maintenance_clear_obstacle=true;" + candidate.ExpectedEffect,
                    ItemId = candidate.ItemId,
                    QualifiedItemId = candidate.QualifiedItemId,
                    SlotIndex = candidate.SlotIndex,
                    Quantity = candidate.Quantity,
                    ShopId = candidate.ShopId,
                    EstimatedTicks = candidate.EstimatedTicks,
                    EnergyCost = candidate.EnergyCost,
                    AvailabilityClass = candidate.AvailabilityClass,
                    AllowedNow = candidate.AllowedNow,
                    AllowedToday = candidate.AllowedToday,
                    NextOpenTime = candidate.NextOpenTime,
                    EffectiveOpenTime = candidate.EffectiveOpenTime,
                    ClosesAt = candidate.ClosesAt,
                    WaitCost = candidate.WaitCost,
                    GateReasons = candidate.GateReasons,
                    BlockReasons = candidate.BlockReasons
                }))
                .ToArray();
        }

        private EventCandidate[] PlantSeedCandidates(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "planting_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("hoe_dirt_tiles", out var tiles) ||
                tiles.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            if (string.IsNullOrWhiteSpace(locationId))
            {
                locationId = ReadString(context.Value, "location_id");
            }

            var seedStacks = SeedInventoryStacks(snapshot);
            var cropCatalog = CropCatalogBySeed(snapshot);
            var seedCosts = SeedUnitCosts(snapshot);
            return tiles.EnumerateArray()
                .Where(tile => tile.ValueKind == JsonValueKind.Object && HasNumber(tile, "tile_x") && HasNumber(tile, "tile_y"))
                .SelectMany(tile => PlantSeedCandidatesForTile(snapshot, tile, string.IsNullOrWhiteSpace(locationId) ? "current_location" : locationId, seedStacks, cropCatalog, seedCosts))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ThenBy(candidate => candidate.ItemId, StringComparer.Ordinal)
                .Take(64)
                .ToArray();
        }

        private IEnumerable<EventCandidate> PlantSeedCandidatesForTile(
            SnapshotEnvelope snapshot,
            JsonElement tile,
            string locationId,
            IReadOnlyDictionary<string, int> seedStacks,
            IReadOnlyDictionary<string, CropCatalogEntry> cropCatalog,
            IReadOnlyDictionary<string, int> seedCosts)
        {
            if (!tile.TryGetProperty("seed_results", out var seedResults) ||
                seedResults.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            var x = ReadInt(tile, "tile_x");
            var y = ReadInt(tile, "tile_y");
            var hasCrop = ReadBool(tile, "has_crop") == true;
            foreach (var result in seedResults.EnumerateArray().Where(result => result.ValueKind == JsonValueKind.Object))
            {
                var seedId = ReadString(result, "seed_id");
                if (string.IsNullOrWhiteSpace(seedId))
                {
                    continue;
                }

                var blockReasons = new List<string>();
                if (hasCrop)
                {
                    blockReasons.Add("plant_seed_target_already_has_crop");
                }

                if (ReadBool(result, "hard_rule_allows_planting") != true)
                {
                    blockReasons.Add("plant_seed_not_allowed_by_transparent_context");
                }

                var seedStack = seedStacks.TryGetValue(seedId, out var stack) ? stack : 0;
                if (seedStack <= 0)
                {
                    blockReasons.Add("plant_seed_inventory_seed_missing");
                }

                if (ReadBool(result, "can_mature_before_season_end_with_paddy_if_eligible") == false)
                {
                    blockReasons.Add("seed_would_not_mature_before_season_end");
                }

                blockReasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "executor.plant_seed",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", x.ToString()),
                        Parameter("target_tile_y", y.ToString()),
                        Parameter("seed_id", seedId)
                    }
                }));

                var qualifiedItemId = "(O)" + seedId;
                var adjustedGrowDays = NullableReadInt(result, "adjusted_grow_days_with_paddy_if_eligible");
                var daysRemaining = NullableReadInt(result, "days_remaining_in_season");
                cropCatalog.TryGetValue(seedId, out var crop);
                var expectedFirstHarvestValue = crop.HarvestUnitSalePrice > 0
                    ? crop.HarvestUnitSalePrice.Value * Math.Max(1, crop.HarvestMinStack ?? 1)
                    : (int?)null;
                var estimatedFirstHarvestQuantity = EstimatedFirstHarvestQuantity(crop);
                var estimatedFirstHarvestValue = crop.HarvestUnitSalePrice > 0 && estimatedFirstHarvestQuantity.HasValue
                    ? crop.HarvestUnitSalePrice.Value * estimatedFirstHarvestQuantity.Value
                    : (double?)null;
                var seedUnitCost = seedCosts.TryGetValue(seedId, out var cost) ? cost : (int?)null;
                var conservativeNetValue = expectedFirstHarvestValue.HasValue && seedUnitCost.HasValue
                    ? expectedFirstHarvestValue.Value - seedUnitCost.Value
                    : (int?)null;
                var estimatedNetValue = estimatedFirstHarvestValue.HasValue && seedUnitCost.HasValue
                    ? estimatedFirstHarvestValue.Value - seedUnitCost.Value
                    : (double?)null;
                var regrowHarvestCount = EstimatedRegrowHarvestCount(crop, adjustedGrowDays, daysRemaining);
                var totalHarvestCount = EstimatedTotalHarvestCount(adjustedGrowDays, daysRemaining, regrowHarvestCount);
                var expectedSeasonHarvestValue = expectedFirstHarvestValue.HasValue && totalHarvestCount.HasValue
                    ? expectedFirstHarvestValue.Value * totalHarvestCount.Value
                    : (int?)null;
                var estimatedSeasonHarvestValue = estimatedFirstHarvestValue.HasValue && totalHarvestCount.HasValue
                    ? estimatedFirstHarvestValue.Value * totalHarvestCount.Value
                    : (double?)null;
                var expectedSeasonHarvestNetValue = expectedSeasonHarvestValue.HasValue && seedUnitCost.HasValue
                    ? expectedSeasonHarvestValue.Value - seedUnitCost.Value
                    : (int?)null;
                var estimatedSeasonHarvestNetValue = estimatedSeasonHarvestValue.HasValue && seedUnitCost.HasValue
                    ? estimatedSeasonHarvestValue.Value - seedUnitCost.Value
                    : (double?)null;
                yield return new EventCandidate
                {
                    CandidateId = "plant:" + locationId + ":" + x + "," + y + ":" + seedId,
                    Kind = "plant_seed_tile",
                    Available = blockReasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect = "current_location.planting_context[" + x + "," + y + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases;seed_id=" + seedId +
                        (adjustedGrowDays.HasValue ? ";adjusted_grow_days=" + adjustedGrowDays.Value : string.Empty) +
                        (daysRemaining.HasValue ? ";days_remaining_in_season=" + daysRemaining.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestItemId) ? ";harvest_item_id=" + crop.HarvestItemId : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestItemQualifiedId) ? ";harvest_item_qualified_id=" + crop.HarvestItemQualifiedId : string.Empty) +
                        (crop.HarvestUnitSalePrice.HasValue ? ";harvest_unit_sale_price=" + crop.HarvestUnitSalePrice.Value : string.Empty) +
                        (crop.HarvestMinStack.HasValue ? ";harvest_min_stack=" + crop.HarvestMinStack.Value : string.Empty) +
                        (crop.HarvestMaxStack.HasValue ? ";harvest_max_stack=" + crop.HarvestMaxStack.Value : string.Empty) +
                        (crop.HarvestMaxIncreasePerFarmingLevel.HasValue ? ";harvest_max_increase_per_farming_level=" + FormatNumber(crop.HarvestMaxIncreasePerFarmingLevel.Value) : string.Empty) +
                        (crop.ExtraHarvestChance.HasValue ? ";extra_harvest_chance=" + FormatNumber(crop.ExtraHarvestChance.Value) : string.Empty) +
                        (crop.HarvestMinQuality.HasValue ? ";harvest_min_quality=" + crop.HarvestMinQuality.Value : string.Empty) +
                        (crop.HarvestMaxQuality.HasValue ? ";harvest_max_quality=" + crop.HarvestMaxQuality.Value : string.Empty) +
                        (!string.IsNullOrWhiteSpace(crop.HarvestMethod) ? ";harvest_method=" + crop.HarvestMethod : string.Empty) +
                        (crop.RegrowDays.HasValue ? ";regrow_days=" + crop.RegrowDays.Value : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_value=" + expectedFirstHarvestValue.Value : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_quantity=" + Math.Max(1, crop.HarvestMinStack ?? 1) : string.Empty) +
                        (expectedFirstHarvestValue.HasValue ? ";expected_first_harvest_value_basis=conservative_min_stack_only" : string.Empty) +
                        (estimatedFirstHarvestQuantity.HasValue ? ";estimated_first_harvest_quantity=" + FormatNumber(estimatedFirstHarvestQuantity.Value) : string.Empty) +
                        (estimatedFirstHarvestValue.HasValue ? ";estimated_first_harvest_value=" + FormatNumber(estimatedFirstHarvestValue.Value) : string.Empty) +
                        (estimatedFirstHarvestValue.HasValue ? ";estimated_first_harvest_value_basis=mean_stack_plus_extra_chance_quality0_no_farming_scaling" : string.Empty) +
                        (regrowHarvestCount.HasValue ? ";estimated_regrow_harvest_count=" + regrowHarvestCount.Value : string.Empty) +
                        (totalHarvestCount.HasValue ? ";estimated_total_harvest_count=" + totalHarvestCount.Value : string.Empty) +
                        (expectedSeasonHarvestValue.HasValue ? ";expected_season_harvest_value=" + expectedSeasonHarvestValue.Value : string.Empty) +
                        (estimatedSeasonHarvestValue.HasValue ? ";estimated_season_harvest_value=" + FormatNumber(estimatedSeasonHarvestValue.Value) : string.Empty) +
                        (seedUnitCost.HasValue ? ";seed_unit_cost=" + seedUnitCost.Value : string.Empty) +
                        (conservativeNetValue.HasValue ? ";expected_first_harvest_net_value=" + conservativeNetValue.Value : string.Empty) +
                        (estimatedNetValue.HasValue ? ";estimated_first_harvest_net_value=" + FormatNumber(estimatedNetValue.Value) : string.Empty) +
                        (expectedSeasonHarvestNetValue.HasValue ? ";expected_season_harvest_net_value=" + expectedSeasonHarvestNetValue.Value : string.Empty) +
                        (estimatedSeasonHarvestNetValue.HasValue ? ";estimated_season_harvest_net_value=" + FormatNumber(estimatedSeasonHarvestNetValue.Value) : string.Empty) +
                        (estimatedSeasonHarvestValue.HasValue ? ";season_harvest_value_basis=first_harvest_value_times_transparent_regrow_count_seed_cost_once" : string.Empty) +
                        (regrowHarvestCount.HasValue ? ";regrow_estimate_basis=adjusted_grow_days_days_remaining_regrow_days" : string.Empty) +
                        (estimatedNetValue.HasValue ? ";net_value_basis=transparent_seed_unit_cost_subtracted" : string.Empty),
                    ItemId = seedId,
                    QualifiedItemId = qualifiedItemId,
                    SlotIndex = NullableReadInt(result, "slot_index"),
                    Quantity = seedStack,
                    EstimatedTicks = 60,
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_planting_context",
                    BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
                };
            }
        }

        private static IReadOnlyDictionary<string, int> SeedInventoryStacks(SnapshotEnvelope snapshot)
        {
            var seedInventory = ReadStateFieldValue(snapshot, "player", "seed_inventory");
            if (!seedInventory.HasValue || seedInventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, int>(StringComparer.Ordinal);
            }

            return seedInventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .GroupBy(item => ReadString(item, "item_id"), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => Math.Max(0, ReadInt(item, "stack"))),
                    StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, int> InventoryStacksByQualifiedId(SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            return inventory.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && ReadBool(item, "is_empty") != true)
                .Select(item => new
                {
                    QualifiedId = NormalizeObjectQualifiedId(ReadString(item, "qualified_item_id"), ReadString(item, "item_id")),
                    Stack = Math.Max(0, ReadInt(item, "stack"))
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.QualifiedId) && item.Stack > 0)
                .GroupBy(item => item.QualifiedId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => item.Stack),
                    StringComparer.OrdinalIgnoreCase);
        }

        private readonly struct CropCatalogEntry
        {
            public CropCatalogEntry(
                string harvestItemId,
                string harvestItemQualifiedId,
                int? harvestUnitSalePrice,
                int? harvestMinStack,
                int? harvestMaxStack,
                double? harvestMaxIncreasePerFarmingLevel,
                double? extraHarvestChance,
                int? harvestMinQuality,
                int? harvestMaxQuality,
                string harvestMethod,
                int? regrowDays)
            {
                HarvestItemId = harvestItemId;
                HarvestItemQualifiedId = harvestItemQualifiedId;
                HarvestUnitSalePrice = harvestUnitSalePrice;
                HarvestMinStack = harvestMinStack;
                HarvestMaxStack = harvestMaxStack;
                HarvestMaxIncreasePerFarmingLevel = harvestMaxIncreasePerFarmingLevel;
                ExtraHarvestChance = extraHarvestChance;
                HarvestMinQuality = harvestMinQuality;
                HarvestMaxQuality = harvestMaxQuality;
                HarvestMethod = harvestMethod;
                RegrowDays = regrowDays;
            }

            public string HarvestItemId { get; }
            public string HarvestItemQualifiedId { get; }
            public int? HarvestUnitSalePrice { get; }
            public int? HarvestMinStack { get; }
            public int? HarvestMaxStack { get; }
            public double? HarvestMaxIncreasePerFarmingLevel { get; }
            public double? ExtraHarvestChance { get; }
            public int? HarvestMinQuality { get; }
            public int? HarvestMaxQuality { get; }
            public string HarvestMethod { get; }
            public int? RegrowDays { get; }
        }

        private static IReadOnlyDictionary<string, CropCatalogEntry> CropCatalogBySeed(SnapshotEnvelope snapshot)
        {
            var cropCatalog = ReadStateFieldValue(snapshot, "farm", "crop_catalog");
            if (!cropCatalog.HasValue || cropCatalog.Value.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, CropCatalogEntry>(StringComparer.Ordinal);
            }

            return cropCatalog.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .GroupBy(item => ReadString(item, "seed_id"), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var item = group.First();
                        return new CropCatalogEntry(
                            ReadString(item, "harvest_item_id"),
                            ReadString(item, "harvest_item_qualified_id"),
                            NullableReadInt(item, "harvest_unit_sale_price"),
                            NullableReadInt(item, "harvest_min_stack"),
                            NullableReadInt(item, "harvest_max_stack"),
                            NullableReadDouble(item, "harvest_max_increase_per_farming_level"),
                            NullableReadDouble(item, "extra_harvest_chance"),
                            NullableReadInt(item, "harvest_min_quality"),
                            NullableReadInt(item, "harvest_max_quality"),
                            ReadString(item, "harvest_method"),
                            NullableReadInt(item, "regrow_days"));
                    },
                    StringComparer.Ordinal);
        }

        private static IReadOnlyDictionary<string, int> SeedUnitCosts(SnapshotEnvelope snapshot)
        {
            return ActiveShopSeedUnitCosts(snapshot)
                .Concat(PreviewShopSeedUnitCosts(snapshot))
                .GroupBy(item => item.SeedId, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(item => item.UnitCost),
                    StringComparer.Ordinal);
        }

        private static IEnumerable<(string SeedId, int UnitCost)> ActiveShopSeedUnitCosts(SnapshotEnvelope snapshot)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var entry in entries.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object))
            {
                var seedId = ReadString(entry, "item_id");
                var price = ReadInt(entry, "price");
                if (!string.IsNullOrWhiteSpace(seedId) && price > 0)
                {
                    yield return (seedId, price);
                }
            }
        }

        private static IEnumerable<(string SeedId, int UnitCost)> PreviewShopSeedUnitCosts(SnapshotEnvelope snapshot)
        {
            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (!shops.HasValue ||
                shops.Value.ValueKind != JsonValueKind.Object ||
                !shops.Value.TryGetProperty("shops", out var shopArray) ||
                shopArray.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var shop in shopArray.EnumerateArray().Where(shop => shop.ValueKind == JsonValueKind.Object))
            {
                if (!shop.TryGetProperty("stock_preview", out var preview) ||
                    preview.ValueKind != JsonValueKind.Object ||
                    !preview.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var entry in entries.EnumerateArray().Where(entry => entry.ValueKind == JsonValueKind.Object))
                {
                    var seedId = ReadString(entry, "item_id");
                    var price = ReadInt(entry, "price");
                    if (!string.IsNullOrWhiteSpace(seedId) && price > 0)
                    {
                        yield return (seedId, price);
                    }
                }
            }
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static double? EstimatedFirstHarvestQuantity(CropCatalogEntry crop)
        {
            if (!crop.HarvestMinStack.HasValue && !crop.HarvestMaxStack.HasValue && !crop.ExtraHarvestChance.HasValue)
            {
                return null;
            }

            var minStack = Math.Max(1, crop.HarvestMinStack ?? 1);
            var maxStack = Math.Max(minStack, crop.HarvestMaxStack ?? minStack);
            var meanStack = (minStack + maxStack) / 2.0;
            var extraChance = Math.Clamp(crop.ExtraHarvestChance ?? 0, 0, 0.9);
            var expectedExtra = extraChance <= 0 ? 0 : extraChance / (1 - extraChance);
            return meanStack + expectedExtra;
        }

        private static int? EstimatedRegrowHarvestCount(CropCatalogEntry crop, int? adjustedGrowDays, int? daysRemaining)
        {
            if (!crop.RegrowDays.HasValue || crop.RegrowDays.Value <= 0 || !adjustedGrowDays.HasValue || !daysRemaining.HasValue)
            {
                return null;
            }

            var remainingAfterFirstHarvest = daysRemaining.Value - adjustedGrowDays.Value;
            if (remainingAfterFirstHarvest < crop.RegrowDays.Value)
            {
                return 0;
            }

            return remainingAfterFirstHarvest / crop.RegrowDays.Value;
        }

        private static int? EstimatedTotalHarvestCount(int? adjustedGrowDays, int? daysRemaining, int? regrowHarvestCount)
        {
            if (!adjustedGrowDays.HasValue || !daysRemaining.HasValue || daysRemaining.Value < adjustedGrowDays.Value)
            {
                return null;
            }

            return 1 + Math.Max(0, regrowHarvestCount ?? 0);
        }

        private static EventCandidate[] WateringCandidates(SnapshotEnvelope snapshot)
        {
            var crops = ReadStateFieldValue(snapshot, "farm", "crops");
            if (!crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return crops.Value.EnumerateArray()
                .Where(crop => crop.ValueKind == JsonValueKind.Object && ReadBool(crop, "needs_watering") == true)
                .Select(crop =>
                {
                    var x = ReadInt(crop, "tile_x");
                    var y = ReadInt(crop, "tile_y");
                    return new EventCandidate
                    {
                        CandidateId = "water:Farm:" + x + "," + y,
                        Kind = "water_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = "farm.crops[" + x + "," + y + "].needs_watering=false",
                        EstimatedTicks = 60,
                        EnergyCost = 2
                    };
                })
                .ToArray();
        }

        private static EventCandidate[] HarvestCropCandidates(SnapshotEnvelope snapshot)
        {
            var crops = ReadStateFieldValue(snapshot, "farm", "crops");
            if (!crops.HasValue || crops.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return crops.Value.EnumerateArray()
                .Where(crop => crop.ValueKind == JsonValueKind.Object && ReadBool(crop, "ready_for_harvest") == true)
                .Select(crop =>
                {
                    var x = ReadInt(crop, "tile_x");
                    var y = ReadInt(crop, "tile_y");
                    var harvestItemId = ReadString(crop, "harvest_item_id");
                    var harvestMethod = ReadString(crop, "harvest_method");
                    var effect = "farm.crops[" + x + "," + y + "].ready_for_harvest=false" +
                        (!string.IsNullOrWhiteSpace(harvestItemId) ? ";harvest_item_id=" + harvestItemId : string.Empty) +
                        (!string.IsNullOrWhiteSpace(harvestMethod) ? ";harvest_method=" + harvestMethod : string.Empty) +
                        ";harvest_executor_status=runtime_verified";
                    return new EventCandidate
                    {
                        CandidateId = "harvest:Farm:" + x + "," + y,
                        Kind = "harvest_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = effect,
                        EstimatedTicks = 60,
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_ready_for_harvest_runtime_verified"
                    };
                })
                .ToArray();
        }

        private static EventCandidate[] HarvestGiantCropCandidates(SnapshotEnvelope snapshot)
        {
            var clumps = ReadStateFieldValue(snapshot, "farm", "resource_clumps");
            if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return clumps.Value.EnumerateArray()
                .Where(clump => clump.ValueKind == JsonValueKind.Object && ReadBool(clump, "is_giant_crop") == true)
                .Select(clump =>
                {
                    var x = ReadInt(clump, "tile_x");
                    var y = ReadInt(clump, "tile_y");
                    var id = ReadString(clump, "giant_crop_id");
                    var health = ReadInt(clump, "health");
                    var effect = "farm.resource_clumps[" + x + "," + y + "].is_giant_crop=false" +
                        (!string.IsNullOrWhiteSpace(id) ? ";giant_crop_id=" + id : string.Empty) +
                        ";required_tool=axe" +
                        ";resource_clump_health=" + health +
                        ";harvest_giant_crop_executor_status=runtime_verified";
                    return new EventCandidate
                    {
                        CandidateId = "harvest-giant-crop:Farm:" + x + "," + y,
                        Kind = "harvest_giant_crop_tile",
                        Available = true,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = effect,
                        EstimatedTicks = Math.Max(3, health) * 60,
                        EnergyCost = Math.Max(1, health),
                        AvailabilityClass = "transparent_giant_crop_resource_clump_runtime_verified"
                    };
                })
                .ToArray();
        }

        private static EventCandidate[] PickupDebrisCandidates(SnapshotEnvelope snapshot)
        {
            var debris = ReadStateFieldValue(snapshot, "farm", "debris");
            if (!debris.HasValue || debris.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return debris.Value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .Select(item =>
                {
                    var index = ReadInt(item, "debris_index");
                    var tile = FirstDebrisChunkTile(item);
                    var qualifiedItemId = ReadString(item, "qualified_item_id");
                    var itemId = ReadString(item, "item_id");
                    var blockReasons = new List<string>();
                    if (tile is null)
                    {
                        blockReasons.Add("pickup_debris_no_chunk_tile");
                    }

                    if (string.IsNullOrWhiteSpace(qualifiedItemId) && string.IsNullOrWhiteSpace(itemId))
                    {
                        blockReasons.Add("pickup_debris_item_id_unavailable");
                    }

                    if (!InventoryMayAcceptItem(snapshot, qualifiedItemId, itemId, ReadInt(item, "item_quality")))
                    {
                        blockReasons.Add("pickup_debris_inventory_cannot_accept_item");
                    }

                    var x = tile?.X ?? 0;
                    var y = tile?.Y ?? 0;
                    var distance = tile is null ? 0 : Math.Abs(playerX - x) + Math.Abs(playerY - y);
                    return new EventCandidate
                    {
                        CandidateId = "pickup-debris:Farm:" + index + ":" + x + "," + y + ":" + (string.IsNullOrWhiteSpace(qualifiedItemId) ? itemId : qualifiedItemId),
                        Kind = "pickup_debris_item",
                        Available = blockReasons.Count == 0,
                        LocationId = "Farm",
                        TileX = tile?.X,
                        TileY = tile?.Y,
                        ExpectedEffect = "farm.debris[" + index + "].chunk_count_decreases_or_removed=true" +
                            (!string.IsNullOrWhiteSpace(qualifiedItemId) ? ";qualified_item_id=" + qualifiedItemId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(itemId) ? ";item_id=" + itemId : string.Empty) +
                            ";debris_index=" + index +
                            ";pickup_executor_status=runtime_collect",
                        ItemId = itemId,
                        QualifiedItemId = qualifiedItemId,
                        Quantity = Math.Max(1, ReadInt(item, "chunk_count")),
                        EstimatedTicks = Math.Max(60, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_debris_runtime_collect",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
                    };
                })
                .OrderBy(candidate => candidate.TileY ?? int.MaxValue)
                .ThenBy(candidate => candidate.TileX ?? int.MaxValue)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate[] MachineProcessingCandidates(SnapshotEnvelope snapshot)
        {
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return machines.Value.EnumerateArray()
                .Where(machine => machine.ValueKind == JsonValueKind.Object)
                .SelectMany(machine =>
                {
                    var x = ReadInt(machine, "tile_x");
                    var y = ReadInt(machine, "tile_y");
                    var heldItem = machine.TryGetProperty("held_item", out var held) && held.ValueKind == JsonValueKind.Object
                        ? held
                        : default;
                    var outputQualifiedId = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadString(heldItem, "qualified_item_id")
                        : string.Empty;
                    var outputItemId = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadString(heldItem, "item_id")
                        : string.Empty;
                    var outputQuality = heldItem.ValueKind == JsonValueKind.Object
                        ? ReadInt(heldItem, "quality")
                        : 0;
                    var outputStack = heldItem.ValueKind == JsonValueKind.Object
                        ? Math.Max(1, ReadInt(heldItem, "stack"))
                        : 1;
                    var outputSalePrice = heldItem.ValueKind == JsonValueKind.Object
                        ? Math.Max(0, ReadInt(heldItem, "sale_price"))
                        : 0;
                    var outputTotalValue = outputSalePrice * outputStack;
                    var standTile = FindBestMachineStandTile(snapshot, x, y);
                    var blockReasons = new List<string>();
                    if (ReadBool(machine, "ready_for_harvest") != true)
                    {
                        blockReasons.Add("machine_output_not_ready");
                    }

                    if (heldItem.ValueKind != JsonValueKind.Object ||
                        (string.IsNullOrWhiteSpace(outputQualifiedId) && string.IsNullOrWhiteSpace(outputItemId)))
                    {
                        blockReasons.Add("machine_output_item_unavailable");
                    }

                    if (standTile.Tile is null)
                    {
                        blockReasons.AddRange(standTile.BlockReasons);
                    }

                    if (!InventoryMayAcceptItem(snapshot, outputQualifiedId, outputItemId, outputQuality))
                    {
                        blockReasons.Add("machine_output_inventory_cannot_accept_item");
                    }

                    var distance = standTile.Tile is null ? 0 : Math.Abs(playerX - standTile.Tile.X) + Math.Abs(playerY - standTile.Tile.Y);
                    var outputCandidate = new EventCandidate
                    {
                        CandidateId = "machine-output:Farm:" + x + "," + y + ":" + (string.IsNullOrWhiteSpace(outputQualifiedId) ? outputItemId : outputQualifiedId),
                        Kind = "collect_machine_output_tile",
                        Available = blockReasons.Count == 0,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = (standTile.Tile is null ? string.Empty : "move_to_adjacent=" + standTile.Tile.X + "," + standTile.Tile.Y + ";") +
                            "farm.machines[" + x + "," + y + "].held_item=null" +
                            (!string.IsNullOrWhiteSpace(outputQualifiedId) ? ";qualified_item_id=" + outputQualifiedId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(outputItemId) ? ";item_id=" + outputItemId : string.Empty) +
                            ";output_stack=" + outputStack +
                            ";output_sale_price=" + outputSalePrice +
                            ";output_total_value=" + outputTotalValue +
                            ";machine_value_basis=held_item_sale_price_times_stack" +
                            ";machine_output_executor_status=runtime_collect",
                        ItemId = outputItemId,
                        QualifiedItemId = outputQualifiedId,
                        Quantity = outputStack,
                        EstimatedTicks = Math.Max(90, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_machine_output_runtime_collect",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
                    };
                    var candidates = new List<EventCandidate> { outputCandidate };
                    candidates.AddRange(MachineLoadInputCandidates(snapshot, machine, x, y, playerX, playerY));
                    return candidates;
                })
                .OrderBy(candidate => candidate.TileY ?? int.MaxValue)
                .ThenBy(candidate => candidate.TileX ?? int.MaxValue)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private EventCandidate[] MachineLoadInputCandidates(SnapshotEnvelope snapshot, JsonElement machine, int x, int y, int playerX, int playerY)
        {
            if (!machine.TryGetProperty("loadable_inputs", out var inputs) || inputs.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var standTile = FindBestMachineStandTile(snapshot, x, y);
            var machineBusy = ReadInt(machine, "minutes_until_ready") > 0 || ReadBool(machine, "ready_for_harvest") == true;
            var machineData = machine.TryGetProperty("machine_data", out var data) && data.ValueKind == JsonValueKind.Object
                ? data
                : default;
            var outputRuleCount = machineData.ValueKind == JsonValueKind.Object ? Math.Max(0, ReadInt(machineData, "output_rule_count")) : 0;
            var hasMachineDataOutput = machineData.ValueKind == JsonValueKind.Object && ReadBool(machineData, "has_output") == true;
            var inventoryStacks = InventoryStacksByQualifiedId(snapshot);
            return inputs.EnumerateArray()
                .Where(input => input.ValueKind == JsonValueKind.Object)
                .Select(input =>
                {
                    var slotIndex = ReadInt(input, "slot_index");
                    var qualifiedItemId = ReadString(input, "qualified_item_id");
                    var itemId = ReadString(input, "item_id");
                    var inputStack = Math.Max(1, ReadInt(input, "stack"));
                    var inputSalePrice = Math.Max(0, ReadInt(input, "sale_price"));
                    var prediction = PredictMachineOutputFromProbe(input, machineData, qualifiedItemId, itemId, inputSalePrice, inventoryStacks) ??
                        PredictMachineOutputFromSummary(machineData, qualifiedItemId, itemId, inputSalePrice, inventoryStacks);
                    var blockReasons = new List<string>();
                    if (machineBusy)
                    {
                        blockReasons.Add("machine_input_target_busy");
                    }

                    if (standTile.Tile is null)
                    {
                        blockReasons.AddRange(standTile.BlockReasons);
                    }

                    if (slotIndex < 0)
                    {
                        blockReasons.Add("machine_input_slot_unavailable");
                    }

                    if (string.IsNullOrWhiteSpace(qualifiedItemId) && string.IsNullOrWhiteSpace(itemId))
                    {
                        blockReasons.Add("machine_input_item_id_unavailable");
                    }

                    var distance = standTile.Tile is null ? 0 : Math.Abs(playerX - standTile.Tile.X) + Math.Abs(playerY - standTile.Tile.Y);
                    return new EventCandidate
                    {
                        CandidateId = "machine-input:Farm:" + x + "," + y + ":slot" + slotIndex + ":" + (string.IsNullOrWhiteSpace(qualifiedItemId) ? itemId : qualifiedItemId),
                        Kind = "load_machine_input_tile",
                        Available = blockReasons.Count == 0,
                        LocationId = "Farm",
                        TileX = x,
                        TileY = y,
                        ExpectedEffect = (standTile.Tile is null ? string.Empty : "move_to_adjacent=" + standTile.Tile.X + "," + standTile.Tile.Y + ";") +
                            "farm.machines[" + x + "," + y + "].minutes_until_ready>0_or_ready=true" +
                            ";input_slot_index=" + slotIndex +
                            (!string.IsNullOrWhiteSpace(qualifiedItemId) ? ";qualified_item_id=" + qualifiedItemId : string.Empty) +
                            (!string.IsNullOrWhiteSpace(itemId) ? ";item_id=" + itemId : string.Empty) +
                            ";input_stack_available=" + inputStack +
                            ";input_sale_price=" + inputSalePrice +
                            ";machine_input_opportunity_cost=" + inputSalePrice +
                            ";machine_input_value_basis=" + prediction.ValueBasis +
                            ";machine_output_rule_count=" + outputRuleCount +
                            ";machine_has_output_rule=" + hasMachineDataOutput.ToString().ToLowerInvariant() +
                            ";machine_output_prediction_status=" + prediction.Status +
                            prediction.ExpectedEffectSuffix +
                            ";machine_input_probe_source=Object.performObjectDropInAction(probe:true)" +
                            ";machine_input_executor_status=runtime_load",
                        ItemId = itemId,
                        QualifiedItemId = qualifiedItemId,
                        SlotIndex = slotIndex,
                        Quantity = inputStack,
                        EstimatedTicks = Math.Max(90, distance * 60 + 30),
                        EnergyCost = 0,
                        AvailabilityClass = "transparent_machine_input_runtime_load",
                        BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
                    };
                })
                .ToArray();
        }

        private static MachineOutputPrediction? PredictMachineOutputFromProbe(
            JsonElement input,
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (!input.TryGetProperty("predicted_output", out var predictedOutput) ||
                predictedOutput.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = ReadString(predictedOutput, "status");
            if (!string.Equals(status, "available", StringComparison.OrdinalIgnoreCase))
            {
                var reason = ReadString(predictedOutput, "reason");
                return string.IsNullOrWhiteSpace(reason)
                    ? null
                    : MachineOutputPrediction.Unavailable("machine_native_probe_" + SanitizeStatus(reason));
            }

            if (!predictedOutput.TryGetProperty("item", out var outputItem) || outputItem.ValueKind != JsonValueKind.Object)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_output_item_unavailable");
            }

            var matchedRuleId = ReadString(predictedOutput, "matched_rule_id");
            var additionalConsumed = ReadAdditionalConsumedSummaryForRequiredItem(machineData, qualifiedItemId, itemId, matchedRuleId, inventoryStacks);
            if (!additionalConsumed.HasValue)
            {
                return MachineOutputPrediction.Unavailable("machine_native_probe_additional_consumption_unpriced");
            }

            var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
            var outputItemId = ReadString(outputItem, "item_id");
            var outputStack = Math.Max(1, ReadInt(predictedOutput, "stack"));
            if (outputStack <= 0)
            {
                outputStack = Math.Max(1, ReadInt(outputItem, "stack"));
            }

            var outputSalePrice = Math.Max(0, ReadInt(predictedOutput, "sale_price"));
            if (outputSalePrice <= 0)
            {
                outputSalePrice = Math.Max(0, ReadInt(outputItem, "sale_price"));
            }

            var totalValue = outputSalePrice * Math.Max(1, outputStack);
            var additionalValue = additionalConsumed.Value.TotalValue;
            var netValue = totalValue - inputSalePrice - additionalValue;
            var suffix = string.Empty;
            if (!string.IsNullOrWhiteSpace(outputQualifiedId))
            {
                suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
            }
            if (!string.IsNullOrWhiteSpace(outputItemId))
            {
                suffix += ";predicted_output_item_id=" + outputItemId;
            }

            suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                ";predicted_output_sale_price=" + outputSalePrice +
                ";predicted_output_price_source=machine_native_probe_sale_price" +
                ";predicted_output_total_value=" + totalValue +
                ";machine_additional_consumed_total_value=" + additionalValue +
                ";predicted_output_net_value=" + netValue;
            var requiredItemId = ReadString(predictedOutput, "required_item_id");
            if (!string.IsNullOrWhiteSpace(requiredItemId))
            {
                suffix += ";predicted_output_rule_required_item_id=" + requiredItemId;
            }
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                suffix += ";predicted_output_rule_id=" + matchedRuleId;
            }
            var preserveType = ReadString(predictedOutput, "preserve_type");
            if (!string.IsNullOrWhiteSpace(preserveType))
            {
                suffix += ";predicted_output_preserve_type=" + preserveType;
            }
            var preservedItemId = ReadString(predictedOutput, "preserved_item_id");
            if (!string.IsNullOrWhiteSpace(preservedItemId))
            {
                suffix += ";predicted_output_preserved_item_id=" + preservedItemId;
            }
            if (!string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
            {
                suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                    ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
            }
            var minutesUntilReady = ReadInt(predictedOutput, "effective_minutes_until_ready");
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "override_minutes_until_ready");
            }
            if (minutesUntilReady <= 0)
            {
                minutesUntilReady = ReadInt(predictedOutput, "rule_minutes_until_ready");
            }
            if (minutesUntilReady > 0)
            {
                suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
            }

            return new MachineOutputPrediction(
                "machine_native_probe_available",
                additionalValue > 0
                    ? "machine_native_probe_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                    : "machine_native_probe_total_value_minus_transparent_input_sale_price",
                suffix);
        }

        private static MachineOutputPrediction PredictMachineOutputFromSummary(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            int inputSalePrice,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object ||
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return MachineOutputPrediction.Unavailable("machine_data_summary_unavailable");
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (!MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(ReadString(rule, "condition")) ||
                    !string.IsNullOrWhiteSpace(ReadString(rule, "per_item_condition")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_condition_not_evaluated");
                }

                var additionalConsumed = ReadAdditionalConsumedSummary(rule, inventoryStacks);
                if (ReadInt(rule, "additional_consumed_item_count") > 0 && !additionalConsumed.HasValue)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_additional_consumption_unpriced");
                }

                if (!rule.TryGetProperty("output_item", out var outputItem) || outputItem.ValueKind != JsonValueKind.Object)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_without_output_item");
                }

                var copyPrice = ReadBool(outputItem, "copy_price") == true;
                if (ReadBool(outputItem, "copy_quality") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_quality_not_priced");
                }

                if (ReadBool(outputItem, "copy_color") == true)
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_copy_color_not_priced");
                }

                if (!string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_type")) ||
                    !string.IsNullOrWhiteSpace(ReadString(outputItem, "preserve_id")))
                {
                    return MachineOutputPrediction.Unavailable("machine_data_exact_required_item_match_preserve_not_priced");
                }

                var outputQualifiedId = ReadString(outputItem, "qualified_item_id");
                var outputItemId = ReadString(outputItem, "item_id");
                var outputStack = ReadInt(outputItem, "stack");
                if (outputStack <= 0)
                {
                    outputStack = Math.Max(1, ReadInt(outputItem, "min_stack"));
                }

                var outputSalePrice = copyPrice ? inputSalePrice : Math.Max(0, ReadInt(outputItem, "sale_price"));
                var totalValue = outputSalePrice * Math.Max(1, outputStack);
                var additionalValue = additionalConsumed.HasValue ? additionalConsumed.Value.TotalValue : 0;
                var netValue = totalValue - inputSalePrice - additionalValue;
                var suffix = string.Empty;
                if (!string.IsNullOrWhiteSpace(outputQualifiedId))
                {
                    suffix += ";predicted_output_qualified_item_id=" + outputQualifiedId;
                }
                if (!string.IsNullOrWhiteSpace(outputItemId))
                {
                    suffix += ";predicted_output_item_id=" + outputItemId;
                }

                suffix += ";predicted_output_stack=" + Math.Max(1, outputStack) +
                    ";predicted_output_sale_price=" + outputSalePrice +
                    ";predicted_output_price_source=" + (copyPrice ? "copy_price_from_transparent_input_sale_price" : "output_item_sale_price") +
                    ";predicted_output_total_value=" + totalValue +
                    ";machine_additional_consumed_total_value=" + additionalValue +
                    ";predicted_output_net_value=" + netValue +
                    ";predicted_output_rule_required_item_id=" + requiredItemId;
                if (additionalConsumed.HasValue && !string.IsNullOrWhiteSpace(additionalConsumed.Value.ConsumedItems))
                {
                    suffix += ";machine_additional_consumed_items=" + additionalConsumed.Value.ConsumedItems +
                        ";machine_additional_consumed_available=" + additionalConsumed.Value.AvailableItems;
                }

                var minutesUntilReady = ReadInt(rule, "minutes_until_ready");
                if (minutesUntilReady > 0)
                {
                    suffix += ";predicted_minutes_until_ready=" + minutesUntilReady;
                }

                return new MachineOutputPrediction(
                    "machine_data_exact_required_item_match",
                    additionalValue > 0
                        ? "predicted_output_total_value_minus_transparent_input_and_additional_consumed_sale_price"
                        : "predicted_output_total_value_minus_transparent_input_sale_price",
                    suffix);
            }

            return MachineOutputPrediction.Unavailable("machine_data_no_exact_required_item_match");
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummary(JsonElement rule, IReadOnlyDictionary<string, int> inventoryStacks)
        {
            var count = ReadInt(rule, "additional_consumed_item_count");
            if (count <= 0)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            if (!rule.TryGetProperty("additional_consumed_items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var pricedCount = 0;
            var total = 0;
            var consumed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var amount = Math.Max(1, ReadInt(item, "amount"));
                var salePrice = ReadInt(item, "sale_price");
                if (salePrice <= 0)
                {
                    return null;
                }

                var qualifiedId = NormalizeObjectQualifiedId(ReadString(item, "qualified_item_id"), ReadString(item, "item_id"));
                if (string.IsNullOrWhiteSpace(qualifiedId))
                {
                    return null;
                }

                total += amount * salePrice;
                consumed[qualifiedId] = consumed.TryGetValue(qualifiedId, out var current) ? current + amount : amount;
                pricedCount++;
            }

            if (pricedCount != count)
            {
                return null;
            }

            var consumedItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value));
            var availableItems = string.Join(",", consumed
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                {
                    inventoryStacks.TryGetValue(pair.Key, out var available);
                    return pair.Key + ":" + available;
                }));
            return new AdditionalConsumedSummary(total, consumedItems, availableItems);
        }

        private static AdditionalConsumedSummary? ReadAdditionalConsumedSummaryForRequiredItem(
            JsonElement machineData,
            string qualifiedItemId,
            string itemId,
            string matchedRuleId,
            IReadOnlyDictionary<string, int> inventoryStacks)
        {
            if (machineData.ValueKind != JsonValueKind.Object ||
                !machineData.TryGetProperty("output_rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array)
            {
                return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
            }

            var normalizedQualified = NormalizeObjectQualifiedId(qualifiedItemId, itemId);
            if (!string.IsNullOrWhiteSpace(matchedRuleId))
            {
                foreach (var rule in rules.EnumerateArray())
                {
                    if (rule.ValueKind == JsonValueKind.Object &&
                        string.Equals(ReadString(rule, "id"), matchedRuleId, StringComparison.Ordinal))
                    {
                        return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                    }
                }
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var requiredItemId = ReadString(rule, "required_item_id");
                if (MachineRuleRequiredItemMatches(requiredItemId, normalizedQualified, itemId))
                {
                    return ReadAdditionalConsumedSummary(rule, inventoryStacks);
                }
            }

            return new AdditionalConsumedSummary(0, string.Empty, string.Empty);
        }

        private static bool MachineRuleRequiredItemMatches(string requiredItemId, string normalizedQualifiedItemId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(requiredItemId))
            {
                return false;
            }

            return string.Equals(requiredItemId, normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requiredItemId, itemId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeObjectQualifiedId(requiredItemId, requiredItemId), normalizedQualifiedItemId, StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeStatus(string value)
        {
            var chars = value
                .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
                .ToArray();
            var status = new string(chars).Trim('_');
            while (status.Contains("__", StringComparison.Ordinal))
            {
                status = status.Replace("__", "_", StringComparison.Ordinal);
            }

            return string.IsNullOrWhiteSpace(status) ? "unavailable" : status;
        }

        private static string NormalizeObjectQualifiedId(string qualifiedItemId, string itemId)
        {
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return qualifiedItemId;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return string.Empty;
            }

            return itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId;
        }

        private readonly struct MachineOutputPrediction
        {
            public MachineOutputPrediction(string status, string valueBasis, string expectedEffectSuffix)
            {
                Status = status;
                ValueBasis = valueBasis;
                ExpectedEffectSuffix = expectedEffectSuffix;
            }

            public string Status { get; }

            public string ValueBasis { get; }

            public string ExpectedEffectSuffix { get; }

            public static MachineOutputPrediction Unavailable(string status)
            {
                return new MachineOutputPrediction(status, "transparent_input_sale_price_output_unknown", string.Empty);
            }
        }

        private readonly struct AdditionalConsumedSummary
        {
            public AdditionalConsumedSummary(int totalValue, string consumedItems, string availableItems)
            {
                TotalValue = totalValue;
                ConsumedItems = consumedItems;
                AvailableItems = availableItems;
            }

            public int TotalValue { get; }

            public string ConsumedItems { get; }

            public string AvailableItems { get; }
        }

        private static (int X, int Y)? FirstDebrisChunkTile(JsonElement debris)
        {
            if (!debris.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Object)
                {
                    return (ReadInt(chunk, "tile_x"), ReadInt(chunk, "tile_y"));
                }
            }

            return null;
        }

        private static bool InventoryMayAcceptItem(SnapshotEnvelope snapshot, string qualifiedItemId, string itemId, int quality)
        {
            var normalizedQualifiedId = !string.IsNullOrWhiteSpace(qualifiedItemId)
                ? qualifiedItemId
                : string.IsNullOrWhiteSpace(itemId)
                    ? string.Empty
                    : itemId.StartsWith("(O)", StringComparison.OrdinalIgnoreCase) ? itemId : "(O)" + itemId;
            if (string.IsNullOrWhiteSpace(normalizedQualifiedId))
            {
                return false;
            }

            var capacity = ReadStateFieldValue(snapshot, "player", "inventory_capacity");
            if (capacity.HasValue && capacity.Value.ValueKind == JsonValueKind.Object)
            {
                if (ReadBool(capacity.Value, "has_empty_slot") == true ||
                    ReadInt(capacity.Value, "empty_slots") > 0)
                {
                    return true;
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (ReadBool(item, "is_empty") == true || string.IsNullOrWhiteSpace(ReadString(item, "qualified_item_id")))
                {
                    return true;
                }

                if (string.Equals(ReadString(item, "qualified_item_id"), normalizedQualifiedId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(item, "quality") == quality &&
                    ReadInt(item, "stack") < ReadInt(item, "maximum_stack_size"))
                {
                    return true;
                }
            }

            return false;
        }

        private EventCandidate[] ClearObstacleCandidates(SnapshotEnvelope snapshot)
        {
            var candidates = new List<EventCandidate>();
            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
            if (objects.HasValue && objects.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in objects.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var x = ReadInt(item, "tile_x");
                    var y = ReadInt(item, "tile_y");
                    var qualifiedId = ReadString(item, "qualified_item_id");
                    var clearKind = ClearableObjectKind(qualifiedId, ReadString(item, "name"));
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        continue;
                    }

                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, qualifiedId));
                }
            }

            var terrainFeatures = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
            if (terrainFeatures.HasValue && terrainFeatures.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var feature in terrainFeatures.Value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
                {
                    var type = ReadString(feature, "type");
                    var clearKind = ClearableTerrainFeatureKind(type);
                    if (string.IsNullOrWhiteSpace(clearKind))
                    {
                        continue;
                    }

                    var x = ReadInt(feature, "tile_x");
                    var y = ReadInt(feature, "tile_y");
                    candidates.Add(ClearObstacleCandidate(snapshot, locationId, playerX, playerY, x, y, clearKind, type));
                }
            }

            return candidates
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY)
                .ThenBy(candidate => candidate.TileX)
                .ToArray();
        }

        private EventCandidate ClearObstacleCandidate(SnapshotEnvelope snapshot, string locationId, int playerX, int playerY, int x, int y, string clearKind, string sourceId)
        {
            var energyCost = ClearObstacleEnergyCost(clearKind);
            var blockReasons = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "executor.clear_obstacle",
                Parameters = new[]
                {
                    Parameter("target_tile_x", x.ToString()),
                    Parameter("target_tile_y", y.ToString()),
                    Parameter("max_tool_swings", "8")
                }
            }).ToList();
            var standTile = FindBestStandTile(snapshot, x, y);
            if (standTile is null)
            {
                blockReasons.Add("clear_obstacle_no_adjacent_route_stand_tile");
            }
            var distance = standTile is not null
                ? Math.Abs(playerX - standTile.X) + Math.Abs(playerY - standTile.Y)
                : 0;
            var estimatedTicks = Math.Max(60, distance * 60 + ClearObstacleToolTicks(clearKind));
            var playerEnergy = ReadStateFieldValue(snapshot, "player", "energy");
            if (playerEnergy.HasValue &&
                playerEnergy.Value.ValueKind == JsonValueKind.Number &&
                playerEnergy.Value.TryGetInt32(out var availableEnergy) &&
                energyCost > availableEnergy)
            {
                blockReasons.Add("insufficient_energy_for_clear_obstacle");
            }

            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            if (currentTime > 0 && WouldFinishAfterClock(currentTime, estimatedTicks, 2600))
            {
                blockReasons.Add("clear_obstacle_would_exceed_day_time_budget");
            }

            return new EventCandidate
            {
                CandidateId = "clear:" + locationId + ":" + x + "," + y + ":" + clearKind,
                Kind = "clear_obstacle_tile",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = (standTile is not null ? "move_to_adjacent=" + standTile.X + "," + standTile.Y + ";" : string.Empty) +
                    "current_location.obstacle[" + x + "," + y + "]=clear;clear_kind=" + clearKind + ";source=" + sourceId,
                EstimatedTicks = estimatedTicks,
                EnergyCost = energyCost,
                AvailabilityClass = "always_available",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray()
            };
        }

        private static int ClearObstacleToolTicks(string clearKind)
        {
            return clearKind switch
            {
                "grass" => 60,
                "weeds" => 60,
                "stone" => 240,
                "twig" => 240,
                "tree" => 600,
                "fruit_tree" => 600,
                _ => 240
            };
        }

        private static int ClearObstacleEnergyCost(string clearKind)
        {
            return clearKind switch
            {
                "grass" => 0,
                "weeds" => 1,
                "stone" => 2,
                "twig" => 2,
                "tree" => 10,
                "fruit_tree" => 10,
                _ => 2
            };
        }

        private static bool WouldFinishAfterClock(int startTime, int estimatedTicks, int latestFinishTime)
        {
            var estimatedMinutes = (int)Math.Ceiling(Math.Max(0, estimatedTicks) / 60.0);
            return AddClockMinutes(startTime, estimatedMinutes) > latestFinishTime;
        }

        private static int AddClockMinutes(int hhmm, int minutes)
        {
            var total = (hhmm / 100 * 60) + (hhmm % 100) + minutes;
            return total / 60 * 100 + total % 60;
        }

        private static string ClearableObjectKind(string qualifiedId, string name)
        {
            if (qualifiedId is "(O)343" or "(O)450")
            {
                return "stone";
            }

            if (qualifiedId is "(O)294" or "(O)295")
            {
                return "twig";
            }

            if (qualifiedId.StartsWith("(O)Weeds", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("weed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "weeds";
            }

            return string.Empty;
        }

        private EventCandidate[] QuestCandidates(SnapshotEnvelope snapshot)
        {
            var activeQuests = ReadStateFieldValue(snapshot, "quests", "active_quests");
            var specialOrders = ReadStateFieldValue(snapshot, "quests", "special_orders");

            var questRefs = activeQuests.HasValue && activeQuests.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<QuestProgressRef[]>(activeQuests.Value.GetRawText()) ?? Array.Empty<QuestProgressRef>()
                : Array.Empty<QuestProgressRef>();

            var orderRefs = specialOrders.HasValue && specialOrders.Value.ValueKind == JsonValueKind.Array
                ? JsonSerializer.Deserialize<SpecialOrderProgressRef[]>(specialOrders.Value.GetRawText()) ?? Array.Empty<SpecialOrderProgressRef>()
                : Array.Empty<SpecialOrderProgressRef>();

            var ordinaryCandidates = QuestCandidateBuilder.BuildOrdinaryCandidates(questRefs);
            var specialOrderCandidates = QuestCandidateBuilder.BuildSpecialOrderCandidates(orderRefs);

            var candidates = new List<EventCandidate>();
            var locationId = ReadStateFieldString(snapshot, "player", "location_id");

            foreach (var candidate in ordinaryCandidates)
            {
                var blockReasons = new List<string>(candidate.BlockedDiagnostics);
                blockReasons.Add("quest_native_executor_not_implemented");
                candidates.Add(new EventCandidate
                {
                    CandidateId = candidate.CandidateId,
                    Kind = "quest_candidate",
                    Available = false,
                    LocationId = locationId,
                    ExpectedEffect = "quest_candidate_family=" + candidate.Family +
                        ";runtime_type=" + candidate.RuntimeType +
                        ";next_action=" + candidate.NextActionCategory +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetLocation) ? ";target_location=" + candidate.RequiredTargetLocation : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetNpc) ? ";target_npc=" + candidate.RequiredTargetNpc : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredItemId) ? ";item_id=" + candidate.RequiredItemId : string.Empty) +
                        ";target_count=" + candidate.RequiredTargetCount +
                        ";current_count=" + candidate.CurrentProgressCount +
                        ";time=unknown;energy=unknown",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    BlockReasons = blockReasons.ToArray(),
                    Parameters = new[]
                    {
                        Parameter("candidate_family", candidate.Family),
                        Parameter("candidate_runtime_type", candidate.RuntimeType),
                        Parameter("candidate_next_action", candidate.NextActionCategory),
                        Parameter("candidate_provenance", candidate.Provenance),
                        Parameter("candidate_id", candidate.CandidateId),
                        Parameter("quest_id", candidate.QuestId),
                        Parameter("quest_key", candidate.QuestKey),
                        Parameter("required_target_npc", candidate.RequiredTargetNpc),
                        Parameter("required_target_location", candidate.RequiredTargetLocation),
                        Parameter("required_item_id", candidate.RequiredItemId),
                        Parameter("required_target_count", candidate.RequiredTargetCount.ToString()),
                        Parameter("current_progress_count", candidate.CurrentProgressCount.ToString()),
                        Parameter("is_complete", candidate.IsComplete.ToString().ToLowerInvariant()),
                        Parameter("days_remaining", candidate.DaysRemaining.ToString()),
                        Parameter("due_date", candidate.DueDate.ToString()),
                        Parameter("planning_eligible", "true")
                    }
                });
            }

            foreach (var candidate in specialOrderCandidates)
            {
                var blockReasons = new List<string>(candidate.BlockedDiagnostics);
                blockReasons.Add("quest_native_executor_not_implemented");
                candidates.Add(new EventCandidate
                {
                    CandidateId = candidate.CandidateId,
                    Kind = "special_order_candidate",
                    Available = false,
                    LocationId = locationId,
                    ExpectedEffect = "quest_candidate_family=" + candidate.Family +
                        ";runtime_type=" + candidate.RuntimeType +
                        ";next_action=" + candidate.NextActionCategory +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetLocation) ? ";target_location=" + candidate.RequiredTargetLocation : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredTargetNpc) ? ";target_npc=" + candidate.RequiredTargetNpc : string.Empty) +
                        (!string.IsNullOrWhiteSpace(candidate.RequiredItemId) ? ";item_id=" + candidate.RequiredItemId : string.Empty) +
                        ";target_count=" + candidate.RequiredTargetCount +
                        ";current_count=" + candidate.CurrentProgressCount +
                        ";time=unknown;energy=unknown",
                    EstimatedTicks = -1,
                    EnergyCost = -1,
                    BlockReasons = blockReasons.ToArray(),
                    Parameters = new[]
                    {
                        Parameter("candidate_family", candidate.Family),
                        Parameter("candidate_runtime_type", candidate.RuntimeType),
                        Parameter("candidate_next_action", candidate.NextActionCategory),
                        Parameter("candidate_provenance", candidate.Provenance),
                        Parameter("candidate_id", candidate.CandidateId),
                        Parameter("quest_id", candidate.QuestId),
                        Parameter("quest_key", candidate.QuestKey),
                        Parameter("required_target_npc", candidate.RequiredTargetNpc),
                        Parameter("required_target_location", candidate.RequiredTargetLocation),
                        Parameter("required_item_id", candidate.RequiredItemId),
                        Parameter("required_target_count", candidate.RequiredTargetCount.ToString()),
                        Parameter("current_progress_count", candidate.CurrentProgressCount.ToString()),
                        Parameter("is_complete", candidate.IsComplete.ToString().ToLowerInvariant()),
                        Parameter("days_remaining", candidate.DaysRemaining.ToString()),
                        Parameter("due_date", candidate.DueDate.ToString()),
                        Parameter("planning_eligible", "true")
                    }
                });
            }

            return candidates.ToArray();
        }

        private static string ClearableTerrainFeatureKind(string type)
        {
            if (type.EndsWith(".Grass", StringComparison.Ordinal) || type == "Grass")
            {
                return "grass";
            }

            if (type.EndsWith(".Tree", StringComparison.Ordinal) || type == "Tree")
            {
                return "tree";
            }

            if (type.EndsWith(".FruitTree", StringComparison.Ordinal) || type == "FruitTree")
            {
                return "fruit_tree";
            }

            return string.Empty;
        }

        private static EconomicCandidate[] EconomicCandidates(SnapshotEnvelope snapshot, string optionId)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuyCandidates(snapshot);
            }

            if (optionId == "economy.sell_items")
            {
                return SellCandidates(snapshot);
            }

            if (optionId == "economy.ship_items")
            {
                return Array.Empty<EconomicCandidate>();
            }

            return Array.Empty<EconomicCandidate>();
        }

        private static string[] ValueGateBlockingReasons(SnapshotEnvelope snapshot, string optionId, EconomicCandidate[] economicCandidates)
        {
            if (optionId == "economy.buy_supplies")
            {
                return BuySuppliesValueBlockReasons(snapshot, economicCandidates);
            }

            if (optionId == "economy.sell_items")
            {
                return SellItemsValueBlockReasons(snapshot, economicCandidates);
            }

            if (optionId == "economy.ship_items")
            {
                return ShipItemsValueBlockReasons(snapshot);
            }

            return Array.Empty<string>();
        }

        private static string[] BuySuppliesValueBlockReasons(SnapshotEnvelope snapshot, EconomicCandidate[] candidates)
        {
            if (candidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue || shopStock.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { "menus_shop_stock_unavailable" };
            }

            if (ReadBool(shopStock.Value, "read_only") == true)
            {
                return new[] { "shop_menu_read_only" };
            }

            if (!shopStock.Value.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return new[] { "shop_stock_empty", "no_value_available_purchase_candidates" };
            }

            return (candidates.Length == 0
                    ? new[] { "shop_stock_empty" }
                    : candidates.SelectMany(candidate => candidate.BlockReasons))
                .Concat(new[] { "no_value_available_purchase_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] SellItemsValueBlockReasons(SnapshotEnvelope snapshot, EconomicCandidate[] candidates)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "player_inventory_unavailable" };
            }

            if (candidates.Any(candidate => candidate.Available))
            {
                return Array.Empty<string>();
            }

            return (candidates.Length == 0
                    ? new[] { "inventory_empty" }
                    : candidates.SelectMany(candidate => candidate.BlockReasons))
                .Concat(new[] { "no_value_available_sell_candidates" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ShipItemsValueBlockReasons(SnapshotEnvelope snapshot)
        {
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!HasCompletedShippingBin(shippingBins))
            {
                return new[] { "no_completed_shipping_bin" };
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "player_inventory_unavailable" };
            }

            return Array.Empty<string>();
        }

        private static EconomicCandidate[] BuyCandidates(SnapshotEnvelope snapshot)
        {
            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue ||
                shopStock.Value.ValueKind != JsonValueKind.Object ||
                !shopStock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return BuyCandidatesFromShopPreview(snapshot);
            }

            var shopId = ReadString(shopStock.Value, "shop_id");
            return entries.EnumerateArray()
                .Select((entry, index) =>
                {
                    var blockReasons = BuyEntryBlockReasons(entry);
                    var price = ReadInt(entry, "price");
                    return new EconomicCandidate
                    {
                        CandidateId = "buy:" + index,
                        Kind = "buy_shop_item",
                        Available = blockReasons.Length == 0,
                        ItemId = ReadString(entry, "item_id"),
                        QualifiedItemId = ReadString(entry, "qualified_item_id"),
                        DisplayName = ReadString(entry, "display_name"),
                        ShopId = shopId,
                        Quantity = 1,
                        UnitPrice = price,
                        TotalValue = price,
                        CurrencyBalance = ReadInt(entry, "currency_balance"),
                        Stock = ReadInt(entry, "stock"),
                        InfiniteStock = ReadBool(entry, "infinite_stock") == true,
                        BlockReasons = blockReasons
                    };
                })
                .ToArray();
        }

        private static EconomicCandidate[] BuyCandidatesFromShopPreview(SnapshotEnvelope snapshot)
        {
            var shops = ReadStateFieldValue(snapshot, "locations", "shops");
            if (!shops.HasValue ||
                shops.Value.ValueKind != JsonValueKind.Object ||
                !shops.Value.TryGetProperty("shops", out var shopArray) ||
                shopArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            return shopArray.EnumerateArray()
                .Where(shop => shop.ValueKind == JsonValueKind.Object)
                .SelectMany(shop =>
                {
                    var shopId = ReadString(shop, "shop_id");
                    if (!shop.TryGetProperty("stock_preview", out var preview) ||
                        preview.ValueKind != JsonValueKind.Object ||
                        !preview.TryGetProperty("entries", out var previewEntries) ||
                        previewEntries.ValueKind != JsonValueKind.Array)
                    {
                        return Enumerable.Empty<EconomicCandidate>();
                    }

                    return previewEntries.EnumerateArray()
                        .Where(entry => entry.ValueKind == JsonValueKind.Object)
                        .Select((entry, index) =>
                        {
                            var blockReasons = ReadStringArray(entry, "executor_block_reasons");
                            var price = ReadInt(entry, "price");
                            return new EconomicCandidate
                            {
                                CandidateId = "buy-preview:" + shopId + ":" + index,
                                Kind = "buy_shop_item",
                                Available = blockReasons.Length == 0 && ReadBool(entry, "executor_purchase_preview_enabled") == true,
                                ItemId = ReadString(entry, "item_id"),
                                QualifiedItemId = ReadString(entry, "qualified_item_id"),
                                DisplayName = ReadString(entry, "display_name"),
                                ShopId = shopId,
                                Quantity = 1,
                                UnitPrice = price,
                                TotalValue = price,
                                CurrencyBalance = ReadInt(entry, "currency_balance"),
                                Stock = ReadInt(entry, "stock"),
                                InfiniteStock = ReadBool(entry, "infinite_stock") == true,
                                BlockReasons = blockReasons
                            };
                        });
                })
                .ToArray();
        }

        private static string[] BuyEntryBlockReasons(JsonElement entry)
        {
            var reasons = new List<string>();
            if (ReadBool(entry, "can_buy_item") != true) reasons.Add("shop_item_cannot_be_bought");
            if (ReadBool(entry, "infinite_stock") != true && ReadInt(entry, "stock") <= 0) reasons.Add("shop_item_out_of_stock");
            if (ReadBool(entry, "can_afford_one_with_currency") != true) reasons.Add("insufficient_currency_for_purchase");
            if (ReadBool(entry, "can_afford_one_with_trade_item") != true) reasons.Add("insufficient_trade_item_for_purchase");
            if (ReadBool(entry, "could_inventory_accept") != true) reasons.Add("inventory_cannot_accept_purchase");
            return reasons.ToArray();
        }

        private static EconomicCandidate[] SellCandidates(SnapshotEnvelope snapshot)
        {
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EconomicCandidate>();
            }

            var sellContext = ReadStateFieldValue(snapshot, "menus", "sell_context");
            var shopSellAvailable = sellContext.HasValue &&
                sellContext.Value.ValueKind == JsonValueKind.Object &&
                ReadBool(sellContext.Value, "read_only") != true &&
                ReadBool(sellContext.Value, "held_item_present") != true &&
                ReadInt(sellContext.Value, "safety_timer") <= 0;
            var categories = ReadIntArray(sellContext, "categories_to_sell");

            return inventory.Value.EnumerateArray()
                .Where(item => ReadBool(item, "is_empty") != true)
                .Select(item =>
                {
                    var blockReasons = SellItemBlockReasons(item, shopSellAvailable, categories);
                    var stack = Math.Max(1, ReadInt(item, "stack"));
                    var sellToStorePrice = ReadInt(item, "sell_to_store_price");
                    var canShopSell = shopSellAvailable && sellToStorePrice > 0 && CategoryAccepted(item, categories);
                    return new EconomicCandidate
                    {
                        CandidateId = "sell:" + ReadInt(item, "slot_index"),
                        Kind = "sell_shop_item",
                        Available = blockReasons.Length == 0,
                        ItemId = ReadString(item, "item_id"),
                        QualifiedItemId = ReadString(item, "qualified_item_id"),
                        DisplayName = ReadString(item, "display_name"),
                        SlotIndex = ReadInt(item, "slot_index"),
                        Quantity = stack,
                        UnitPrice = sellToStorePrice,
                        TotalValue = sellToStorePrice * stack,
                        CanShopSell = canShopSell,
                        BlockReasons = blockReasons
                    };
                })
                .ToArray();
        }

        private EventCandidate[] ShipCandidates(SnapshotEnvelope snapshot)
        {
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!HasCompletedShippingBin(shippingBins))
            {
                return Array.Empty<EventCandidate>();
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var fullShipmentIndex = ReadFullShipmentIndex(snapshot);
            var binBounds = SelectShippingBinTile(shippingBins);
            if (binBounds is null)
            {
                return Array.Empty<EventCandidate>();
            }

            var binContents = ReadShippingBinContents(snapshot);

            return inventory.Value.EnumerateArray()
                .Where(item => ReadBool(item, "is_empty") != true)
                .Where(item => ReadBool(item, "can_be_shipped") == true && ReadInt(item, "sale_price") > 0 && ReadInt(item, "stack") > 0)
                .Where(item => ReadBool(item, "protected_from_auto_sell") != true && !HasArrayItems(item, "auto_sell_protection_reasons"))
                .Select(item => ShipCandidateForItem(snapshot, item, fullShipmentIndex, binBounds, binContents)!)
                .ToArray();
        }

        private EventCandidate ShipCandidateForItem(
            SnapshotEnvelope snapshot,
            JsonElement item,
            IReadOnlyDictionary<string, FullShipmentItemIndexEntry>? fullShipmentIndex,
            ShippingBinTile binBounds,
            IReadOnlyDictionary<string, int> binContents)
        {
            var blockReasons = new List<string>();
            var locationId = "Farm";

            var itemId = ReadString(item, "item_id");
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            var slotIndex = ReadInt(item, "slot_index");
            var stack = Math.Max(1, ReadInt(item, "stack"));
            var salePrice = ReadInt(item, "sale_price");

            var fullShipmentContributes = false;
            var fullShipmentKnown = false;
            var fullShipmentEligible = false;
            var fullShipmentCurrentShippedCount = 0;
            var fullShipmentAlreadyShipped = false;

            if (fullShipmentIndex != null)
            {
                if (fullShipmentIndex.TryGetValue(itemId, out var fsEntry))
                {
                    fullShipmentKnown = true;
                    fullShipmentEligible = true;
                    fullShipmentCurrentShippedCount = fsEntry.CurrentShippedCount;
                    fullShipmentAlreadyShipped = fsEntry.Shipped;
                    fullShipmentContributes = !fsEntry.Shipped && blockReasons.Count == 0;
                }
                else
                {
                    fullShipmentKnown = true;
                    fullShipmentEligible = false;
                }
            }

            var quantity = fullShipmentContributes ? 1 : stack;
            var availableStack = stack;

            var standTile = ReadBinStandTile(snapshot, binBounds);
            CandidateTile? routeTarget = standTile;
            if (routeTarget is null)
            {
                blockReasons.Add("shipping_bin_no_transparent_interaction_stand_tile");
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var distance = routeTarget is not null
                ? Math.Abs(playerX - routeTarget.X) + Math.Abs(playerY - routeTarget.Y)
                : 0;
            var estimatedTicks = Math.Max(60, distance * 60 + 30);

            var currentBinCount = binContents.TryGetValue(qualifiedItemId, out var binCount) ? binCount : 0;

            var effect = "executor_kind=ship_inventory_item_to_bin" +
                ";qualified_item_id=" + qualifiedItemId +
                ";item_id=" + itemId +
                ";slot_index=" + slotIndex +
                ";quantity=" + quantity +
                ";available_stack=" + availableStack +
                ";sale_price=" + salePrice +
                ";total_shipping_value=" + (salePrice * quantity) +
                ";shipping_bin_tile=" + binBounds.TileX + "," + binBounds.TileY +
                ";shipping_bin_width=" + binBounds.Width + ",height=" + binBounds.Height +
                (routeTarget is not null
                    ? ";route_stand_tile=" + routeTarget.X + "," + routeTarget.Y
                    : ";route_stand_tile=blocked") +
                ";bin_location=" + locationId +
                ";bin_current_count_of_item=" + currentBinCount +
                ";full_shipment_known=" + fullShipmentKnown.ToString().ToLowerInvariant() +
                ";full_shipment_eligible=" + fullShipmentEligible.ToString().ToLowerInvariant() +
                ";full_shipment_current_shipped_count=" + fullShipmentCurrentShippedCount +
                ";full_shipment_already_shipped=" + fullShipmentAlreadyShipped.ToString().ToLowerInvariant() +
                ";full_shipment_contributes=" + fullShipmentContributes.ToString().ToLowerInvariant() +
                ";shipping_executor_status=runtime_verified";

            return new EventCandidate
            {
                CandidateId = "ship:" + locationId + ":" + binBounds.TileX + "," + binBounds.TileY + ":" + slotIndex + ":" + itemId,
                Kind = "ship_inventory_item_to_bin",
                Available = blockReasons.Count == 0,
                LocationId = locationId,
                TileX = routeTarget?.X,
                TileY = routeTarget?.Y,
                ExpectedEffect = effect,
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                SlotIndex = slotIndex,
                Quantity = quantity,
                ShopId = "ShippingBin",
                EstimatedTicks = estimatedTicks,
                EnergyCost = 0,
                AvailabilityClass = "transparent_shipping_bin",
                FullShipmentKnown = fullShipmentKnown,
                FullShipmentEligible = fullShipmentEligible,
                FullShipmentCurrentShippedCount = fullShipmentCurrentShippedCount,
                FullShipmentAlreadyShipped = fullShipmentAlreadyShipped,
                FullShipmentContributes = fullShipmentContributes,
                AvailableStack = availableStack,
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("slot_index", slotIndex.ToString()),
                    Parameter("item_id", itemId),
                    Parameter("qualified_item_id", qualifiedItemId),
                    Parameter("quantity", quantity.ToString()),
                    Parameter("available_stack", availableStack.ToString()),
                    Parameter("sale_price", salePrice.ToString()),
                    Parameter("bin_tile_x", binBounds.TileX.ToString()),
                    Parameter("bin_tile_y", binBounds.TileY.ToString()),
                    Parameter("route_stand_tile_x", (routeTarget?.X).ToString() ?? string.Empty),
                    Parameter("route_stand_tile_y", (routeTarget?.Y).ToString() ?? string.Empty),
                    Parameter("bin_location", locationId),
                    Parameter("full_shipment_known", fullShipmentKnown.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_eligible", fullShipmentEligible.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_current_shipped_count", fullShipmentCurrentShippedCount.ToString()),
                    Parameter("full_shipment_already_shipped", fullShipmentAlreadyShipped.ToString().ToLowerInvariant()),
                    Parameter("full_shipment_contributes", fullShipmentContributes.ToString().ToLowerInvariant()),
                    Parameter("bin_current_count_of_item", currentBinCount.ToString()),
                    Parameter("shipping_executor_available", "runtime_verified")
                }
            };
        }

        private static CandidateTile? ReadBinStandTile(SnapshotEnvelope snapshot, ShippingBinTile bin)
        {
            if (bin.StandX.HasValue && bin.StandY.HasValue)
            {
                return new CandidateTile(bin.StandX.Value, bin.StandY.Value);
            }

            return null;
        }

        private static IReadOnlyDictionary<string, int> ReadShippingBinContents(SnapshotEnvelope snapshot)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var shippingBins = ReadStateFieldValue(snapshot, "farm", "shipping_bins");
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var bin in shippingBins.Value.EnumerateArray())
            {
                if (!bin.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    var qualifiedId = ReadString(content, "qualified_item_id");
                    var count = ReadInt(content, "count");
                    if (!string.IsNullOrWhiteSpace(qualifiedId) && count > 0)
                    {
                        result[qualifiedId] = result.TryGetValue(qualifiedId, out var current)
                            ? current + count
                            : count;
                    }
                }
            }

            return result;
        }

        private static string[] SellItemBlockReasons(JsonElement item, bool shopSellAvailable, int[] categories)
        {
            var reasons = new List<string>();
            if (ReadBool(item, "protected_from_auto_sell") == true || HasArrayItems(item, "auto_sell_protection_reasons"))
            {
                reasons.Add("inventory_item_protected_from_auto_sell");
            }

            var stack = ReadInt(item, "stack");
            var sellPrice = ReadInt(item, "sell_to_store_price");
            if (stack <= 0 || sellPrice <= 0)
            {
                reasons.Add("non_positive_sell_price");
            }

            var canShopSell = shopSellAvailable && sellPrice > 0 && CategoryAccepted(item, categories);
            if (!canShopSell)
            {
                if (!shopSellAvailable) reasons.Add("menus_sell_context_unavailable");
                if (shopSellAvailable && !CategoryAccepted(item, categories)) reasons.Add("item_not_accepted_by_active_shop");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static JsonElement? ReadStateFieldValue(SnapshotEnvelope snapshot, string sectionName, string fieldName)
        {
            if (!snapshot.State.TryGetValue(sectionName, out var section) ||
                section.ValueKind != JsonValueKind.Object ||
                !section.TryGetProperty(fieldName, out var field) ||
                field.ValueKind != JsonValueKind.Object ||
                !field.TryGetProperty("value", out var value))
            {
                return null;
            }

            return value;
        }

        private static int ReadStateFieldInt(SnapshotEnvelope snapshot, string sectionName, string fieldName)
        {
            var value = ReadStateFieldValue(snapshot, sectionName, fieldName);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetInt32(out var result)
                ? result
                : 0;
        }

        private static string ReadStateFieldString(SnapshotEnvelope snapshot, string sectionName, string fieldName)
        {
            var value = ReadStateFieldValue(snapshot, sectionName, fieldName);
            return value.HasValue && value.Value.ValueKind == JsonValueKind.String
                ? value.Value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool HasUsableShippingBin(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return shippingBins.Value.EnumerateArray().Any(bin =>
                ReadInt(bin, "days_of_construction_left") <= 0);
        }

        private static bool HasCompletedShippingBin(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return shippingBins.Value.EnumerateArray().Any(bin =>
                ReadInt(bin, "days_of_construction_left") <= 0);
        }

        private static ShippingBinTile? SelectShippingBinTile(JsonElement? shippingBins)
        {
            if (!shippingBins.HasValue || shippingBins.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var bin in shippingBins.Value.EnumerateArray())
            {
                if (ReadInt(bin, "days_of_construction_left") <= 0)
                {
                    var tileX = ReadInt(bin, "tile_x");
                    var tileY = ReadInt(bin, "tile_y");
                    var width = Math.Max(1, ReadInt(bin, "tile_width"));
                    if (width <= 0) width = ReadInt(bin, "tiles_wide");
                    if (width <= 0) width = 2;
                    var height = Math.Max(1, ReadInt(bin, "tile_height"));
                    if (height <= 0) height = ReadInt(bin, "tiles_high");
                    if (height <= 0) height = 1;
                    var standX = NullableReadInt(bin, "interaction_stand_tile_x");
                    var standY = NullableReadInt(bin, "interaction_stand_tile_y");
                    return new ShippingBinTile(tileX, tileY, width, height, standX, standY);
                }
            }

            return null;
        }

        private sealed class ShippingBinTile
        {
            public ShippingBinTile(int tileX, int tileY, int width, int height, int? standX, int? standY)
            {
                TileX = tileX;
                TileY = tileY;
                Width = width;
                Height = height;
                StandX = standX;
                StandY = standY;
            }

            public int TileX { get; }
            public int TileY { get; }
            public int Width { get; }
            public int Height { get; }
            public int? StandX { get; }
            public int? StandY { get; }
        }

        private static int[] ReadIntArray(JsonElement? parent, string propertyName)
        {
            if (!parent.HasValue ||
                parent.Value.ValueKind != JsonValueKind.Object ||
                !parent.Value.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int>();
            }

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Number)
                .Select(item => item.GetInt32())
                .ToArray();
        }

        private static string[] ReadStringArray(JsonElement parent, string propertyName)
        {
            if (parent.ValueKind != JsonValueKind.Object ||
                !parent.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray();
        }

        private static bool CategoryAccepted(JsonElement item, int[] categories)
        {
            return categories.Length == 0 || categories.Contains(ReadInt(item, "category"));
        }

        private static bool HasArrayItems(JsonElement value, string propertyName)
        {
            return value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty(propertyName, out var array) &&
                array.ValueKind == JsonValueKind.Array &&
                array.GetArrayLength() > 0;
        }

        private static bool? ReadBool(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static bool HasNumber(JsonElement value, string propertyName)
        {
            return value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Number;
        }

        private static int ReadInt(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return 0;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var result) ? result : 0;
        }

        private static int? NullableReadInt(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var result) ? result : null;
        }

        private static double? NullableReadDouble(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var result) ? result : null;
        }

        private static string ReadString(JsonElement value, string propertyName)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(propertyName, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() ?? string.Empty : string.Empty;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }

        private string[] CompilerProbeBlockingReasons(SnapshotEnvelope snapshot, OptionAvailabilityCandidate candidate)
        {
            if (candidate.Parameters.Length == 0 && candidate.OptionId != "executor.interact")
            {
                return Array.Empty<string>();
            }

            var envelope = new SmallModelActionEnvelope
            {
                ModelOutputId = "availability.synthetic",
                SourceModel = "candidate-availability.evaluator",
                StateHash = snapshot.StateHash,
                GoalId = "candidate.availability",
                ExecutionMode = "training_singleplayer",
                Actor = new ActionActorRef
                {
                    ActorId = "training_farmer.availability",
                    ActorType = "training_farmer",
                    ControlSurface = "training_sandbox"
                },
                Actions = new[]
                {
                    new SmallModelAction
                    {
                        ActionId = "availability.synthetic.action",
                        OptionId = candidate.OptionId,
                        Rationale = "candidate availability parameter-bound validation",
                        Parameters = candidate.Parameters
                    }
                }
            };

            var queue = compiler.Compile(envelope, snapshot);
            var item = queue.Items.FirstOrDefault();
            if (item is null)
            {
                return Array.Empty<string>();
            }

            return item.BlockingReasons
                .Where(reason => reason != "queue_global_compiler_block")
                .ToArray();
        }

        private static bool IsExecutorEnabled(string optionId)
        {
            return optionId == "recovery.stabilize_day" ||
                optionId == "farm.maintain_crops" ||
                optionId == "farm.process_machines" ||
                optionId == "fishing.catch_fish" ||
                optionId == "economy.buy_supplies" ||
                optionId == "exploration.visit_location" ||
                optionId == "executor.move_to_tile" ||
                optionId == "executor.traverse_connector" ||
                optionId == "executor.face_direction" ||
                optionId == "executor.wait_ticks" ||
                optionId == "executor.select_safe_item_slot" ||
                optionId == "executor.close_menu" ||
                optionId == "executor.buy_shop_item" ||
                optionId == "executor.clear_obstacle" ||
                optionId == "executor.till_soil" ||
                optionId == "executor.plant_seed" ||
                optionId == "executor.harvest_crop" ||
                optionId == "executor.harvest_giant_crop" ||
                optionId == "executor.catch_fish" ||
                optionId == "executor.interact" ||
                optionId == "executor.sleep" ||
                optionId == "executor.pickup_debris" ||
                optionId == "executor.collect_machine_output" ||
                optionId == "executor.load_machine_input" ||
                optionId == "executor.choose_dialogue_response" ||
                optionId == "executor.social_interact";
        }

        private static bool IsPreviewOnly(string optionId, string trainingRole, bool executorEnabled)
        {
            if (trainingRole == TrainingRoles.ExecutorCalibration)
            {
                return !executorEnabled;
            }

            return optionId == "economy.sell_items" ||
                optionId == "economy.ship_items" ||
                optionId == "social.talk_npc" ||
                optionId == "social.gift_npc" ||
                optionId == "quest.advance";
        }

        private static string ExecutorDisabledReason(string optionId)
        {
            if (optionId == "economy.sell_items")
            {
                return "sell_shipping_executor_disabled";
            }

            if (optionId == "social.talk_npc" || optionId == "social.gift_npc")
            {
                return "social_high_level_direct_executor_disabled_use_daily_plan_compiler";
            }

            if (optionId == "quest.advance")
            {
                return "quest_native_executor_not_implemented";
            }

            if (optionId == "mining.reach_depth")
            {
                return "mining_perfect_executor_not_implemented";
            }

            if (optionId == "executor.harvest_crop")
            {
                return "harvest_executor_disabled";
            }

            return "executor_disabled";
        }

        private sealed class FullShipmentItemIndexEntry
        {
            public int CurrentShippedCount { get; set; }
            public bool Shipped { get; set; }
        }

        private static IReadOnlyDictionary<string, FullShipmentItemIndexEntry>? ReadFullShipmentIndex(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("world_progress", out var worldSection) ||
                worldSection.ValueKind != JsonValueKind.Object ||
                !worldSection.TryGetProperty("full_shipment_progress", out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = ReadString(envelope, "status");
            if (status != "available" && status != "derived")
            {
                return null;
            }

            if (!envelope.TryGetProperty("value", out var value) ||
                value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!value.TryGetProperty("eligible_item_count", out var eligibleCount) ||
                eligibleCount.ValueKind != JsonValueKind.Number ||
                !eligibleCount.TryGetInt32(out var expectedCount) ||
                expectedCount < 0)
            {
                return null;
            }

            if (!value.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var index = new Dictionary<string, FullShipmentItemIndexEntry>(StringComparer.Ordinal);
            foreach (var entry in items.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var itemId = ReadString(entry, "item_id");
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return null;
                }

                if (!entry.TryGetProperty("current_shipped_count", out var currentCountEl) ||
                    currentCountEl.ValueKind != JsonValueKind.Number ||
                    !currentCountEl.TryGetInt32(out var currentCount) ||
                    currentCount < 0)
                {
                    return null;
                }

                if (!entry.TryGetProperty("shipped", out var shippedEl) ||
                    (shippedEl.ValueKind != JsonValueKind.True && shippedEl.ValueKind != JsonValueKind.False))
                {
                    return null;
                }
                var shipped = shippedEl.ValueKind == JsonValueKind.True;

                if (shipped != (currentCount > 0))
                {
                    return null;
                }

                if (!index.TryAdd(itemId, new FullShipmentItemIndexEntry
                {
                    CurrentShippedCount = currentCount,
                    Shipped = shipped
                }))
                {
                    return null;
                }
            }

            if (index.Count != expectedCount)
            {
                return null;
            }

            return index;
        }
    }
}
