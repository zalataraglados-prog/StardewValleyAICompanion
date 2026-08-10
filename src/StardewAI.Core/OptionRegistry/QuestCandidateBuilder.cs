using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.Core.OptionRegistry
{
    public static class QuestCandidateBuilder
    {
        private static readonly Dictionary<int, string> QuestTypeToRuntimeClass = new()
        {
            [1] = "Quest",
            [2] = "CraftingQuest",
            [3] = "ItemDeliveryQuest",
            [4] = "SlayMonsterQuest",
            [5] = "SocializeQuest",
            [6] = "GoSomewhereQuest",
            [7] = "FishingQuest",
            [8] = "HaveBuildingQuest",
            [9] = "type9_ambiguous",
            [10] = "ResourceCollectionQuest",
            [11] = "type_weeding_no_subclass"
        };

        private static readonly string[] ObjectiveRuntimeClasses = new[]
        {
            "CollectObjective", "DeliverObjective", "DonateObjective",
            "FishObjective", "GiftObjective", "JKScoreObjective",
            "ReachMineFloorObjective", "ShipObjective", "SlayObjective"
        };

        private static readonly string[] RewardRuntimeClasses = new[]
        {
            "FriendshipReward", "GemsReward", "MailReward",
            "MoneyReward", "ObjectReward", "ResetEventReward"
        };

        public static QuestCandidateRef[] BuildOrdinaryCandidates(QuestProgressRef[] activeQuests)
        {
            if (activeQuests is null || activeQuests.Length == 0)
            {
                return Array.Empty<QuestCandidateRef>();
            }

            return activeQuests
                .Where(q => q is not null)
                .Select(BuildOrdinaryCandidate)
                .ToArray();
        }

        public static QuestCandidateRef[] BuildSpecialOrderCandidates(SpecialOrderProgressRef[] activeOrders)
        {
            if (activeOrders is null || activeOrders.Length == 0)
            {
                return Array.Empty<QuestCandidateRef>();
            }

            return activeOrders
                .Where(o => o is not null)
                .Select(BuildSpecialOrderCandidate)
                .ToArray();
        }

        public static QuestCompilerEnvelope BuildCompilerEnvelope(
            QuestCandidateRef[] ordinaryCandidates,
            QuestCandidateRef[] specialOrderCandidates,
            QuestProgressRef[] rawActiveQuests,
            SpecialOrderProgressRef[] rawSpecialOrders,
            string? requestedCandidateId = null,
            string? requestedQuestId = null,
            string? requestedQuestKey = null,
            string? requestedTargetCount = null,
            string? requestedCurrentCount = null,
            string? requestedObjectiveIndex = null)
        {
            var allCandidates = ordinaryCandidates.Concat(specialOrderCandidates).ToArray();

            var identityCount = 0;
            if (!string.IsNullOrWhiteSpace(requestedCandidateId)) identityCount++;
            if (!string.IsNullOrWhiteSpace(requestedQuestId)) identityCount++;
            if (!string.IsNullOrWhiteSpace(requestedQuestKey)) identityCount++;

            if (identityCount == 0)
            {
                return new QuestCompilerEnvelope
                {
                    TimeEstimate = "unknown",
                    EnergyCost = "unknown",
                    ExecutorBlockReason = "quest_identity_not_specified"
                };
            }

            var matchedCandidates = allCandidates.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(requestedCandidateId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.CandidateId, requestedCandidateId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(requestedQuestId))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestId, requestedQuestId, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(requestedQuestKey))
            {
                matchedCandidates = matchedCandidates.Where(c => string.Equals(c.QuestKey, requestedQuestKey, StringComparison.Ordinal));
            }

            var matchList = matchedCandidates.ToArray();

            if (matchList.Length == 0)
            {
                return new QuestCompilerEnvelope
                {
                    TimeEstimate = "unknown",
                    EnergyCost = "unknown",
                    ExecutorBlockReason = "quest_candidate_not_found:" + string.Join(";", new[]
                    {
                        requestedCandidateId is not null ? "candidate_id=" + requestedCandidateId : null,
                        requestedQuestId is not null ? "quest_id=" + requestedQuestId : null,
                        requestedQuestKey is not null ? "quest_key=" + requestedQuestKey : null
                    }.Where(s => s is not null))
                };
            }

            if (matchList.Length > 1)
            {
                return new QuestCompilerEnvelope
                {
                    TimeEstimate = "unknown",
                    EnergyCost = "unknown",
                    ExecutorBlockReason = "quest_candidate_ambiguous:" + string.Join(";", new[]
                    {
                        requestedCandidateId is not null ? "candidate_id=" + requestedCandidateId : null,
                        requestedQuestId is not null ? "quest_id=" + requestedQuestId : null,
                        requestedQuestKey is not null ? "quest_key=" + requestedQuestKey : null
                    }.Where(s => s is not null)) + ";matches=" + string.Join(",", matchList.Select(c => c.CandidateId))
                };
            }

            var match = matchList[0];

            if (!string.IsNullOrWhiteSpace(requestedTargetCount))
            {
                if (!int.TryParse(requestedTargetCount, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedTargetCount))
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_target_count_malformed:value=" + requestedTargetCount
                    };
                }
                if (match.RequiredTargetCount != parsedTargetCount)
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_target_count_mismatch:model=" + parsedTargetCount + ";live=" + match.RequiredTargetCount
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedCurrentCount))
            {
                if (!int.TryParse(requestedCurrentCount, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedCurrentCount))
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_current_count_malformed:value=" + requestedCurrentCount
                    };
                }
                if (match.CurrentProgressCount != parsedCurrentCount)
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_current_count_mismatch:model=" + parsedCurrentCount + ";live=" + match.CurrentProgressCount
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedObjectiveIndex))
            {
                if (!int.TryParse(requestedObjectiveIndex, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedObjectiveIndex))
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_selected_objective_index_malformed:value=" + requestedObjectiveIndex
                    };
                }
                if (match.SelectedObjectiveIndex != parsedObjectiveIndex)
                {
                    return new QuestCompilerEnvelope
                    {
                        TimeEstimate = "unknown",
                        EnergyCost = "unknown",
                        ExecutorBlockReason = "quest_selected_objective_index_mismatch:model=" + parsedObjectiveIndex + ";live=" + match.SelectedObjectiveIndex
                    };
                }
            }

            return new QuestCompilerEnvelope
            {
                TimeEstimate = "unknown",
                EnergyCost = "unknown",
                ExecutorBlockReason = "quest_requires_typed_daily_candidate_binding",
                SelectedCandidateId = match.CandidateId,
                SelectedQuestKey = match.QuestKey,
                SelectedQuestId = match.QuestId,
                SelectedRuntimeType = match.RuntimeType,
                Family = match.Family,
                NextActionCategory = match.NextActionCategory,
                RequiredTargetNpc = match.RequiredTargetNpc,
                RequiredTargetLocation = match.RequiredTargetLocation,
                RequiredItemId = match.RequiredItemId,
                RequiredTargetCount = match.RequiredTargetCount,
                CurrentProgressCount = match.CurrentProgressCount,
                SelectedObjectiveIndex = match.SelectedObjectiveIndex,
                LiveEvidence = new QuestCompilerEvidence
                {
                    Candidate = match,
                    RawActiveQuests = rawActiveQuests ?? Array.Empty<QuestProgressRef>(),
                    RawSpecialOrders = rawSpecialOrders ?? Array.Empty<SpecialOrderProgressRef>()
                }
            };
        }

        private static QuestCandidateRef BuildOrdinaryCandidate(QuestProgressRef quest)
        {
            var candidateId = "quest:" + (quest.Id ?? "unknown") + ":" + quest.RuntimeType;
            var diagnostics = new List<string>();
            string nextActionCategory;
            string targetNpc = string.Empty;
            string targetLocation = string.Empty;
            string itemId = string.Empty;
            string buildingType = string.Empty;
            int targetCount = 0;
            int currentCount = 0;
            int? targetTileX = null;
            int? targetTileY = null;
            bool complete = quest.Completed;

            if (complete)
            {
                nextActionCategory = "completed";
                diagnostics.Add("quest_already_completed");
            }
            else if (!quest.Accepted)
            {
                nextActionCategory = "accept";
                diagnostics.Add("quest_not_accepted");
            }
            else if (string.IsNullOrWhiteSpace(quest.RuntimeType))
            {
                nextActionCategory = "unknown";
                diagnostics.Add("quest_runtime_type_unavailable");
            }
            else
            {
                var fields = quest.PerTypeFields;
                if (fields is null)
                {
                    nextActionCategory = "unknown";
                    diagnostics.Add("quest_per_type_fields_unavailable");
                }
                else if (!string.IsNullOrWhiteSpace(fields.UnsupportedSubtype))
                {
                    nextActionCategory = "unsupported";
                    diagnostics.Add("quest_unsupported_subtype:" + fields.UnsupportedSubtype);
                }
                else
                {
                    nextActionCategory = CategorizeOrdinaryNextAction(quest.QuestType, fields, diagnostics);
                    targetNpc = string.IsNullOrWhiteSpace(fields.TargetNpc) ? fields.NpcName : fields.TargetNpc;
                    targetLocation = string.IsNullOrWhiteSpace(fields.TargetLocation) ? fields.LocationOfItem : fields.TargetLocation;
                    itemId = fields.ItemId;
                    buildingType = fields.BuildingType;
                    targetCount = fields.TargetCount;
                    currentCount = fields.CurrentCount;
                    if (quest.RuntimeType == "LostItemQuest")
                    {
                        targetTileX = fields.TileX;
                        targetTileY = fields.TileY;
                    }
                    if (quest.QuestType == 5)
                    {
                        targetCount = fields.TotalToGreet;
                        currentCount = Math.Max(0, fields.TotalToGreet - (fields.WhoToGreet?.Length ?? 0));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(quest.RuntimeType))
            {
                diagnostics.Add("quest_missing_runtime_type");
            }

            var provenance = "direct_net_fields:Quest." + quest.RuntimeType;
            if (quest.QuestType == 9)
            {
                provenance = "direct_net_fields:type9_subclass_runtime_disambiguation_required_" + (string.IsNullOrWhiteSpace(quest.RuntimeType) ? "unresolved" : quest.RuntimeType);
            }

            return new QuestCandidateRef
            {
                CandidateId = candidateId,
                Family = "ordinary_quest",
                QuestId = quest.Id ?? string.Empty,
                QuestKey = string.Empty,
                RuntimeType = !string.IsNullOrWhiteSpace(quest.RuntimeType) ? quest.RuntimeType : QuestTypeToRuntimeClass.GetValueOrDefault(quest.QuestType, "unknown_type_" + quest.QuestType),
                Title = quest.Title ?? string.Empty,
                Available = diagnostics.Count == 0,
                BlockedDiagnostics = diagnostics.ToArray(),
                NextActionCategory = nextActionCategory,
                RequiredTargetLocation = targetLocation,
                RequiredTargetNpc = targetNpc,
                RequiredItemId = itemId,
                RequiredBuildingType = buildingType,
                RequiredTargetCount = targetCount,
                RequiredTargetTileX = targetTileX,
                RequiredTargetTileY = targetTileY,
                CurrentProgressCount = currentCount,
                IsComplete = complete,
                DaysRemaining = quest.DaysLeft,
                TimeCostUnknown = true,
                EnergyCostUnknown = true,
                Provenance = provenance,
                PlanningEligible = true
            };
        }

        private static QuestCandidateRef BuildSpecialOrderCandidate(SpecialOrderProgressRef order)
        {
            var questKey = order.QuestKey ?? "unknown";
            var candidateId = "special_order:" + questKey;
            var diagnostics = new List<string>();
            string nextActionCategory;
            string targetNpc = string.Empty;
            string targetLocation = string.Empty;
            string itemId = string.Empty;
            int targetCount = 0;
            int currentCount = 0;
            int selectedObjectiveIndex = -1;
            bool complete = false;

            var state = order.QuestState ?? string.Empty;
            if (state == "Complete" || state == "1")
            {
                complete = true;
                nextActionCategory = "completed";
                diagnostics.Add("special_order_completed");
            }
            else if (state == "Failed" || state == "2")
            {
                nextActionCategory = "failed";
                diagnostics.Add("special_order_failed");
            }
            else if (state != "InProgress" && state != "0")
            {
                nextActionCategory = "unknown_state";
                diagnostics.Add("special_order_unknown_state:" + state);
            }
            else
            {
                nextActionCategory = CategorizeSpecialOrderNextAction(order, diagnostics, out selectedObjectiveIndex);
                var objectives = order.Objectives;
                if (objectives is not null && objectives.Length > 0)
                {
                    var selectedObjective = selectedObjectiveIndex >= 0 && selectedObjectiveIndex < objectives.Length
                        ? objectives[selectedObjectiveIndex]
                        : null;
                    if (selectedObjective is not null)
                    {
                        targetCount = selectedObjective.MaxCount;
                        currentCount = selectedObjective.CurrentCount;
                        var objFields = selectedObjective.PerTypeFields;
                        if (objFields is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(objFields.TargetName))
                            {
                                targetNpc = objFields.TargetName;
                            }
                            if (!string.IsNullOrWhiteSpace(objFields.ResolvedDropBoxGameLocation))
                            {
                                targetLocation = objFields.ResolvedDropBoxGameLocation;
                            }
                            else if (!string.IsNullOrWhiteSpace(objFields.DropBoxGameLocation))
                            {
                                targetLocation = objFields.DropBoxGameLocation;
                            }
                            if (objFields.AcceptableContextTagSets is not null && objFields.AcceptableContextTagSets.Length > 0)
                            {
                                itemId = string.Join(",", objFields.AcceptableContextTagSets);
                            }
                            if (!objFields.Available)
                            {
                                diagnostics.Add("objective_fields_unavailable:" + (string.IsNullOrWhiteSpace(objFields.UnavailableReason) ? "no_reason" : objFields.UnavailableReason));
                            }
                        }
                        else
                        {
                            diagnostics.Add("special_order_objective_per_type_fields_unavailable");
                        }

                        if (selectedObjective.FailOnCompletion)
                        {
                            diagnostics.Add("objective_fail_on_completion_set");
                        }
                    }
                    else
                    {
                        diagnostics.Add("special_order_no_selected_incomplete_objective");
                    }
                }
                else
                {
                    diagnostics.Add("special_order_no_objectives");
                }

                if (order.Rewards is null || order.Rewards.Length == 0)
                {
                    diagnostics.Add("special_order_no_reward_data");
                }
                else
                {
                    var anyRewardAvailable = order.Rewards.Any(r => r.Available);
                    if (!anyRewardAvailable)
                    {
                        diagnostics.Add("special_order_reward_fields_unavailable");
                    }
                }
            }

            if (order.SpecialRule is null)
            {
                diagnostics.Add("special_rule_unavailable");
            }

            if (order.IsIslandOrder < 0)
            {
                diagnostics.Add("is_island_order_unset");
            }

            var provenance = "direct_net_fields:SpecialOrder." + (order.QuestKey ?? "null");

            return new QuestCandidateRef
            {
                CandidateId = candidateId,
                Family = "special_order",
                QuestId = questKey,
                QuestKey = questKey,
                RuntimeType = "SpecialOrder",
                Title = order.QuestName ?? string.Empty,
                Available = diagnostics.Count == 0,
                BlockedDiagnostics = diagnostics.ToArray(),
                NextActionCategory = nextActionCategory,
                RequiredTargetLocation = targetLocation,
                RequiredTargetNpc = targetNpc,
                RequiredItemId = itemId,
                RequiredTargetCount = targetCount,
                CurrentProgressCount = currentCount,
                IsComplete = complete,
                SelectedObjectiveIndex = selectedObjectiveIndex,
                DueDate = order.DueDate,
                TimeCostUnknown = true,
                EnergyCostUnknown = true,
                Provenance = provenance,
                PlanningEligible = true
            };
        }

        private static string CategorizeOrdinaryNextAction(int questType, PerTypeQuestFields fields, List<string> diagnostics)
        {
            if (fields is null)
            {
                diagnostics.Add("per_type_fields_null");
                return "fields_unavailable";
            }

            switch (questType)
            {
                case 1:
                    return "basic_no_action";
                case 2:
                    return string.IsNullOrWhiteSpace(fields.ItemId) ? "craft_missing_item" : "craft_item";
                case 3:
                    if (string.IsNullOrWhiteSpace(fields.TargetNpc) && string.IsNullOrWhiteSpace(fields.ItemId))
                    {
                        diagnostics.Add("delivery_missing_target_and_item");
                        return "delivery_fields_incomplete";
                    }
                    return string.IsNullOrWhiteSpace(fields.TargetNpc) ? "deliver_item" : "deliver_to_npc";
                case 4:
                    if (string.IsNullOrWhiteSpace(fields.MonsterName))
                    {
                        diagnostics.Add("slay_missing_monster_name");
                    }
                    return fields.CurrentCount >= fields.NumberToKill && fields.NumberToKill > 0
                        ? "return_to_npc"
                        : "slay_monsters";
                case 5:
                    return fields.WhoToGreet is not null && fields.WhoToGreet.Length > 0
                        ? "greet_npcs"
                        : "socialize_complete";
                case 6:
                    return string.IsNullOrWhiteSpace(fields.TargetLocation) ? "go_somewhere_missing_location" : "go_to_location";
                case 7:
                    return fields.CurrentCount >= fields.NumberToFish && fields.NumberToFish > 0
                        ? "return_to_npc"
                        : "fish_for_item";
                case 8:
                    return string.IsNullOrWhiteSpace(fields.BuildingType) ? "build_missing_type" : "construct_building";
                case 9:
                    return CategorizeType9NextAction(fields, diagnostics);
                case 10:
                    return fields.CurrentCount >= fields.NumberRequired && fields.NumberRequired > 0
                        ? "return_to_npc"
                        : "collect_resources";
                case 11:
                    return "weeding_no_subclass";
                default:
                    diagnostics.Add("unknown_quest_type_" + questType);
                    return "unknown";
            }
        }

        private static string CategorizeType9NextAction(PerTypeQuestFields fields, List<string> diagnostics)
        {
            if (!string.IsNullOrWhiteSpace(fields.NpcName) || !string.IsNullOrWhiteSpace(fields.LocationOfItem) ||
                fields.TileX != 0 || fields.TileY != 0)
            {
                if (!string.IsNullOrWhiteSpace(fields.ExclusiveQuestId) || fields.FriendshipReward != 0)
                {
                    return fields.ItemFound ? "return_secret_lost_item_to_npc" : "find_secret_lost_item";
                }
                return fields.ItemFound ? "return_lost_item_to_npc" : "find_lost_item";
            }

            if (!string.IsNullOrWhiteSpace(fields.ItemId))
            {
                return "harvest_items";
            }

            diagnostics.Add("type9_ambiguous_fields");
            return "type9_ambiguous";
        }

        private static string CategorizeSpecialOrderNextAction(SpecialOrderProgressRef order, List<string> diagnostics, out int selectedObjectiveIndex)
        {
            selectedObjectiveIndex = -1;
            var objectives = order.Objectives;
            if (objectives is null || objectives.Length == 0)
            {
                return "no_objectives";
            }

            var anyIncomplete = false;
            var hasFailOnCompletionObjective = false;
            var index = 0;
            foreach (var obj in objectives)
            {
                var objType = obj.RuntimeType ?? string.Empty;
                if (string.IsNullOrWhiteSpace(objType))
                {
                    diagnostics.Add("objective_runtime_type_unavailable");
                    index++;
                    continue;
                }

                if (obj.CurrentCount >= obj.MaxCount && obj.MaxCount > 0)
                {
                    if (obj.FailOnCompletion)
                    {
                        diagnostics.Add("fail_on_completion_objective_met");
                    }
                    index++;
                    continue;
                }

                if (obj.FailOnCompletion)
                {
                    hasFailOnCompletionObjective = true;
                    index++;
                    continue;
                }

                anyIncomplete = true;
                selectedObjectiveIndex = index;
                switch (objType)
                {
                    case "CollectObjective":
                        return "collect_items";
                    case "DeliverObjective":
                        return "deliver_to_target";
                    case "DonateObjective":
                        return "donate_items";
                    case "FishObjective":
                        return "catch_fish";
                    case "GiftObjective":
                        return "give_gifts";
                    case "JKScoreObjective":
                        return "achieve_junimo_kart_score";
                    case "ReachMineFloorObjective":
                        return "reach_mine_floor";
                    case "ShipObjective":
                        return "ship_items";
                    case "SlayObjective":
                        return "slay_monsters";
                    default:
                        diagnostics.Add("unknown_objective_runtime_type:" + objType);
                        break;
                }
                index++;
            }

            if (!anyIncomplete && hasFailOnCompletionObjective)
            {
                diagnostics.Add("only_fail_on_completion_objectives_remain");
                return "avoid_fail_on_completion_objective";
            }
            return anyIncomplete ? "advance_objective" : "all_objectives_complete";
        }

        public static string ResolveType9RuntimeClass(QuestProgressRef quest)
        {
            if (quest.QuestType != 9)
            {
                return QuestTypeToRuntimeClass.GetValueOrDefault(quest.QuestType, "unknown");
            }

            var fields = quest.PerTypeFields;
            if (fields is null)
            {
                return "type9_ambiguous_no_fields";
            }

            if (!string.IsNullOrWhiteSpace(fields.NpcName) || !string.IsNullOrWhiteSpace(fields.LocationOfItem) ||
                fields.TileX != 0 || fields.TileY != 0 || fields.ItemFound)
            {
                return "LostItemQuest";
            }

            if (!string.IsNullOrWhiteSpace(fields.ExclusiveQuestId) || fields.FriendshipReward != 0)
            {
                return "SecretLostItemQuest";
            }

            if (!string.IsNullOrWhiteSpace(fields.ItemId) && fields.CurrentCount >= 0)
            {
                return "ItemHarvestQuest";
            }

            return "type9_ambiguous_unresolved";
        }
    }
}
