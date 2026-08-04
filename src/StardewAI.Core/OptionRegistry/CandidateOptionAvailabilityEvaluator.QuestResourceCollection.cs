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
            var currentDebrisReceipts = CurrentLocationIsProjectedMine(snapshot)
                ? Enumerable.Empty<EventCandidate>()
                : PickupDebrisCandidates(snapshot)
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
            var bushSourceSteps = BushHarvestCandidates(snapshot)
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
                    }));
            var gingerSourceSteps = GingerHarvestCandidates(snapshot)
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
                    }));
            var scytheCropSourceSteps = HarvestCropCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => string.Equals(
                    ReadParameter(candidate.Parameters, "harvest_method"),
                    "Scythe",
                    StringComparison.OrdinalIgnoreCase))
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
                    }));
            var giantCropSourceSteps = HarvestGiantCropCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => ProjectedOutputContainsItem(
                    candidate,
                    "giant_crop_guaranteed_outputs_json",
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "false"),
                        Parameter("quest_acquisition_source_step", "true")
                    }));
            var currentLocationClumpSourceSteps = GreenRainResourceClumpCandidates(snapshot)
                .Where(candidate => candidate.Available)
                .Where(candidate => ProjectedOutputContainsItem(
                    candidate,
                    "expected_output_items_json",
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "false"),
                        Parameter("quest_acquisition_source_step", "true")
                    }));
            var fishingSourceSteps = FishingEventCandidateBuilder.Build(snapshot)
                .Where(candidate => candidate.Kind == "catch_fish" && candidate.Available)
                .Where(candidate => FishingCandidateContainsItem(
                    candidate,
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "false"),
                        Parameter("quest_acquisition_source_step", "true")
                    }));
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
            var machineLoadSources = BoundedTaskMachineLoadCandidates(snapshot)
                .Where(candidate => ItemIdentityMatches(
                    ReadParameter(
                        candidate.Parameters,
                        "predicted_output_item_id"),
                    ReadParameter(
                        candidate.Parameters,
                        "predicted_output_qualified_item_id"),
                    quest.RequiredItemId))
                .Select(candidate => AttachQuest(
                    candidate,
                    quest,
                    new[]
                    {
                        Parameter("quest_required_item_id", quest.RequiredItemId),
                        Parameter("quest_acquisition_target_step", "false"),
                        Parameter("quest_acquisition_source_step", "true")
                    }));
            var miningSteps = MiningResourceCollectionCandidateBuilder
                .Build(snapshot, QualifyQuestObjectId(quest.RequiredItemId))
                .Select(candidate => AttachQuest(candidate, quest));
            var candidates = directReceipts
                .Concat(currentDebrisReceipts)
                .Concat(sourceSteps)
                .Concat(bushSourceSteps)
                .Concat(gingerSourceSteps)
                .Concat(scytheCropSourceSteps)
                .Concat(giantCropSourceSteps)
                .Concat(currentLocationClumpSourceSteps)
                .Concat(fishingSourceSteps)
                .Concat(machineReceipts)
                .Concat(machineLoadSources)
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

        private static bool CurrentLocationIsProjectedMine(SnapshotEnvelope snapshot)
        {
            var currentMine = ReadStateFieldValue(
                snapshot,
                "mining",
                "current_mine");
            if (!currentMine.HasValue ||
                currentMine.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var mineLocation = ReadString(currentMine.Value, "location_id");
            var playerLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            return !string.IsNullOrWhiteSpace(mineLocation) &&
                string.Equals(
                    mineLocation,
                    playerLocation,
                    StringComparison.OrdinalIgnoreCase);
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

            var matchingMonsterDropIds = MatchingMonsterDropQualifiedItemIds(
                snapshot,
                fields.AcceptableContextTagSets);
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
                .Concat(BoundedTaskMachineLoadCandidates(snapshot)
                    .Where(candidate => CandidateContextTagsMatch(
                        candidate,
                        "predicted_output_context_tags_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
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
                .Concat(BushHarvestCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => CandidateContextTagsMatch(
                        candidate,
                        "bush_output_context_tags_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(GingerHarvestCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => CandidateContextTagsMatch(
                        candidate,
                        "ginger_output_context_tags_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(HarvestCropCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => string.Equals(
                        ReadParameter(candidate.Parameters, "harvest_method"),
                        "Scythe",
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
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(HarvestGiantCropCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => ProjectedOutputContextTagsMatch(
                        candidate,
                        "giant_crop_guaranteed_outputs_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(GreenRainResourceClumpCandidates(snapshot)
                    .Where(candidate => candidate.Available)
                    .Where(candidate => ProjectedOutputContextTagsMatch(
                        candidate,
                        "expected_output_context_tag_sets_json",
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                                "quest_acceptable_context_tag_sets_json",
                                JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(FishingEventCandidateBuilder.Build(snapshot)
                    .Where(candidate => candidate.Kind == "catch_fish" && candidate.Available)
                    .Where(candidate => FishingCandidateMatchesContextTags(
                        candidate,
                        fields.AcceptableContextTagSets))
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
                            Parameter(
                            "quest_acceptable_context_tag_sets_json",
                            JsonSerializer.Serialize(fields.AcceptableContextTagSets))
                        })))
                .Concat(MiningResourceCollectionCandidateBuilder
                    .BuildMonsterDrops(
                        snapshot,
                        matchingMonsterDropIds,
                        "special_order:" + quest.QuestKey + ":" + quest.SelectedObjectiveIndex)
                    .Where(candidate => candidate.Available)
                    .Select(candidate => AttachQuest(
                        candidate,
                        quest,
                        new[]
                        {
                            Parameter("quest_acquisition_target_step", "false"),
                            Parameter("quest_acquisition_source_step", "true"),
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
                        "special_order_matching_collect_action_not_ready_in_current_projection")
                };
        }

        private IEnumerable<EventCandidate> BoundedTaskMachineLoadCandidates(
            SnapshotEnvelope snapshot)
        {
            return MachineServiceCandidates(snapshot, commitmentLedger: null)
                .Where(candidate => string.Equals(
                    candidate.Kind,
                    "load_machine_input_tile",
                    StringComparison.Ordinal))
                .Where(candidate => candidate.Available)
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_output_prediction_status"),
                    "machine_native_probe_available",
                    StringComparison.Ordinal))
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "machine_prediction_training_kind"),
                    "exact",
                    StringComparison.Ordinal))
                .Where(candidate => string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "predicted_output_additional_consumed_item_count"),
                    "0",
                    StringComparison.Ordinal))
                .Where(candidate => !string.IsNullOrWhiteSpace(
                    ReadParameter(
                        candidate.Parameters,
                        "predicted_output_qualified_item_id")));
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
