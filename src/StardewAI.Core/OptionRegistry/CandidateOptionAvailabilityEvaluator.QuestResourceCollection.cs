using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private IEnumerable<EventCandidate> BindOrdinaryResourceCollectionCandidates(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest)
        {
            if (string.IsNullOrWhiteSpace(quest.RequiredItemId))
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "quest_resource_item_identity_missing") };
            }

            var directReceipts = SpawnedObjectForagingCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => ItemIdentityMatches(
                    candidate.ItemId,
                    candidate.QualifiedItemId,
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "true"),
                        Parameter("quest_acquisition_source_step", "false")
                    }));
            var farmDebrisReceipts = PickupDebrisCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => ItemIdentityMatches(
                    candidate.ItemId,
                    candidate.QualifiedItemId,
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "true"),
                        Parameter("quest_acquisition_source_step", "false")
                    }));
            var sourceSteps = ClearObstacleCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => ClearCandidateProducesItem(candidate, quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "false"),
                        Parameter("quest_acquisition_source_step", "true")
                    }));
            var bushSourceSteps = string.Equals(
                    ReadStateFieldString(snapshot, "player", "location_id"),
                    "Farm",
                    StringComparison.OrdinalIgnoreCase)
                ? BushHarvestCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => ItemIdentityMatches(
                        candidate.ItemId,
                        candidate.QualifiedItemId,
                        quest.RequiredItemId))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_required_item_id", quest.RequiredItemId),
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true")
                        }))
                : Enumerable.Empty<EventCandidate>();
            var machineReceipts = MachineServiceCandidates(snapshot, commitmentLedger: null)
                .Where(candidate => candidate.Kind == "collect_machine_output_tile" && candidate.Available)
                .Where(candidate => ItemIdentityMatches(
                    candidate.ItemId,
                    candidate.QualifiedItemId,
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "true"),
                        Parameter("quest_acquisition_source_step", "false")
                    }));
            var miningSteps = MiningResourceCollectionCandidateBuilder
                .Build(snapshot, QualifyQuestObjectId(quest.RequiredItemId))
                .Select(candidate => AttachQuest(candidate, quest));
            var candidates = directReceipts
                .Concat(farmDebrisReceipts)
                .Concat(sourceSteps)
                .Concat(bushSourceSteps)
                .Concat(machineReceipts)
                .Concat(miningSteps)
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[]
                {
                    BlockedQuestCandidate(
                        snapshot,
                        quest,
                        "quest_matching_resource_source_not_available_in_current_projection")
                };
        }

        private static bool ClearCandidateProducesItem(EventCandidate candidate, string requiredItemId)
        {
            var qualifiedRequired = QualifyQuestObjectId(requiredItemId);
            return
                (string.Equals(
                    ReadParameter(candidate.Parameters, "clear_output_qualified_item_id"),
                    qualifiedRequired,
                    StringComparison.OrdinalIgnoreCase) &&
                 ReadIntParameter(candidate.Parameters, "clear_output_quantity_min") > 0) ||
                (string.Equals(
                    ReadParameter(candidate.Parameters, "clear_bonus_output_qualified_item_id"),
                    qualifiedRequired,
                    StringComparison.OrdinalIgnoreCase) &&
                 ReadIntParameter(candidate.Parameters, "clear_bonus_output_quantity_min") > 0);
        }

        private static string QualifyQuestObjectId(string itemId)
        {
            return itemId.StartsWith("(", StringComparison.Ordinal)
                ? itemId
                : "(O)" + itemId;
        }

        private static int ReadIntParameter(
            SmallModelActionParameter[] parameters,
            string name)
        {
            return int.TryParse(
                ReadParameter(parameters, name),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                    ? value
                    : 0;
        }

        private IEnumerable<EventCandidate> BindSpecialOrderCollectCandidates(
            SnapshotEnvelope snapshot,
            QuestCandidateRef quest,
            PerTypeObjectiveFields fields)
        {
            if (fields.AcceptableContextTagSets.Length == 0)
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "special_order_collect_context_tag_sets_missing") };
            }
            if (QuestContextTagMatcher.ContainsUnprojectedColorTag(fields.AcceptableContextTagSets))
            {
                return new[] { BlockedQuestCandidate(snapshot, quest, "special_order_collect_has_unprojected_color_tags") };
            }

            var candidates = HarvestCropCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => string.Equals(
                    ReadParameter(candidate.Parameters, "harvest_method"),
                    "Grab",
                    StringComparison.OrdinalIgnoreCase))
                .Where(candidate => CandidateContextTagsMatch(
                    candidate,
                    "harvest_context_tags_json",
                    fields.AcceptableContextTagSets))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_acquisition_target_step", "true"),
                        Parameter("quest_acquisition_source_step", "false"),
                        Parameter(
                            "quest_acceptable_context_tag_sets_json",
                            JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                    }))
                .Concat(MachineServiceCandidates(snapshot, commitmentLedger: null)
                    .Where(candidate =>
                        candidate.Kind == "collect_machine_output_tile" &&
                        candidate.Available)
                    .Where(candidate => CandidateContextTagsMatch(
                        candidate,
                        "output_context_tags_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "true"),
                            Parameter("quest_acquisition_source_step", "false"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(PickupDebrisCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => CandidateContextTagsMatch(
                        candidate,
                        "debris_context_tags_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "true"),
                            Parameter("quest_acquisition_source_step", "false"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .ToArray();
            return candidates.Length > 0
                ? candidates
                : new[]
                {
                    BlockedQuestCandidate(
                        snapshot,
                        quest,
                        "special_order_matching_grab_harvest_not_ready_in_current_projection")
                };
        }

        private static bool CandidateContextTagsMatch(
            EventCandidate candidate,
            string parameterName,
            string[] acceptableContextTagSets)
        {
            try
            {
                var tags = JsonSerializer.Deserialize<string[]>(
                    ReadParameter(candidate.Parameters, parameterName)) ?? Array.Empty<string>();
                return QuestContextTagMatcher.Matches(tags, acceptableContextTagSets);
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
