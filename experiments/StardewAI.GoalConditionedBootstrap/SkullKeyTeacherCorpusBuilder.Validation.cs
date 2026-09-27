using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class SkullKeyTeacherCorpusBuilder
{
    private static void ValidateEpisodeHeader(
        PlanExecutionEpisodeEnvelope episode)
    {
        if (episode.SchemaVersion != "plan_execution_episode.v1" ||
            string.IsNullOrWhiteSpace(episode.EpisodeId) ||
            string.IsNullOrWhiteSpace(episode.RunId) ||
            episode.OptionId != "executor.interact" ||
            episode.Status != "applied" ||
            !episode.Success ||
            episode.TrainingRole != TrainingRoles.ExecutorCalibration ||
            episode.BlockReasons.Length != 0 ||
            episode.PrimitiveKind != "claim_skull_key" ||
            episode.PrimitiveVerificationStatus != "verified")
        {
            throw new InvalidDataException(
                "Skull Key execution episode header is invalid.");
        }
    }

    private static void ValidateEpisodeBinding(
        PlanExecutionEpisodeEnvelope episode,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        TrainingExecutionResult result)
    {
        if (episode.SourceStateHash != before.StateHash ||
            episode.AfterStateHash != after.StateHash ||
            !episode.StateHashChanged ||
            episode.SourceStateHash == episode.AfterStateHash ||
            !episode.AfterSnapshotFresh ||
            episode.BeforeGameTick != before.GameTick ||
            episode.AfterGameTick != after.GameTick ||
            episode.AfterGameTick <= episode.BeforeGameTick ||
            episode.RunId != result.RunId ||
            episode.QueueId != result.QueueId ||
            episode.OptionId != result.OptionId ||
            episode.Status != result.Status ||
            episode.PrimitiveKind != result.PrimitiveKind ||
            episode.PrimitiveVerificationStatus !=
                result.PrimitiveVerificationStatus ||
            !episode.PrimitiveVerificationReasons.SequenceEqual(
                result.PrimitiveVerificationReasons,
                StringComparer.Ordinal) ||
            !string.Equals(
                episode.RequestedEffect,
                result.RequestedEffect,
                StringComparison.Ordinal) ||
            !string.Equals(
                episode.ObservedEffect,
                result.ObservedEffect,
                StringComparison.Ordinal) ||
            !EqualJson(episode.ChangedFacts, result.ChangedFacts))
        {
            throw new InvalidDataException(
                "Skull Key execution episode does not bind its native artifacts.");
        }
    }

    private static void ValidateOrdinaryFloor120(
        SnapshotEnvelope snapshot,
        string label)
    {
        var mine = ReadEnvelopeObject(snapshot, "mining", "current_mine");
        if (RequiredString(mine, "mine_kind") != "ordinary_mines" ||
            RequiredInt(mine, "mine_level") != BottomOfOrdinaryMines ||
            !RequiredBool(mine, "is_loaded_current_location") ||
            RequiredBool(mine, "is_skull_cavern") ||
            RequiredBool(mine, "is_quarry_mine"))
        {
            throw new InvalidDataException(
                "Skull Key " + label +
                " snapshot is not loaded ordinary-mine floor 120.");
        }
    }

    private static void ValidateRewardChest(JsonElement chest)
    {
        if (RequiredString(chest, "runtime_type") !=
                "StardewValley.Objects.Chest" ||
            RequiredInt(chest, "item_count") < 1 ||
            !RequiredBool(chest, "contains_skull_key") ||
            RequiredSkullKeySpecialItemWhich(chest) != 4 ||
            RequiredString(chest, "interaction_kind") != "overlay_object" ||
            RequiredString(chest, "expected_action_type") != "SkullKeyChest" ||
            RequiredString(chest, "source") !=
                "MineShaft.overlayObjects Chest.Items SpecialItem.which")
        {
            throw new InvalidDataException(
                "Skull Key reward chest identity is invalid.");
        }
    }

    private static void ValidateQueueItem(
        PlanExecutionEpisodeEnvelope episode,
        TrainingExecutionResult result,
        int chestX,
        int chestY)
    {
        var item = episode.EffectiveQueueItem;
        if (!item.HasValue || item.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Skull Key execution episode has no effective queue item.");
        }
        var command = RequiredObject(item.Value, "normalized_command");
        if (
            RequiredString(item.Value, "queue_item_id") !=
                result.QueueItemId ||
            RequiredString(item.Value, "option_id") !=
                "mining.obtain_skull_key" ||
            RequiredString(command, "option_id") !=
                "mining.obtain_skull_key" ||
            RequiredString(command, "state_hash") != episode.SourceStateHash ||
            RequiredConsistentParameter(command, "execution_option_id") !=
                "executor.interact" ||
            RequiredConsistentParameter(command, "target_location_family") !=
                "ordinary_mines" ||
            RequiredConsistentParameter(command, "target_depth") !=
                BottomOfOrdinaryMines.ToString(CultureInfo.InvariantCulture) ||
            RequiredConsistentParameter(
                command,
                "required_terminal_interaction") !=
                "skull_key_reward_chest" ||
            RequiredConsistentParameter(command, "mining_step_kind") !=
                "claim_skull_key" ||
            RequiredConsistentParameter(command, "target_tile_x") !=
                chestX.ToString(CultureInfo.InvariantCulture) ||
            RequiredConsistentParameter(command, "target_tile_y") !=
                chestY.ToString(CultureInfo.InvariantCulture) ||
            RequiredConsistentParameter(command, "interaction_kind") !=
                "overlay_object" ||
            RequiredConsistentParameter(command, "expected_action_type") !=
                "SkullKeyChest" ||
            RequiredConsistentParameter(command, "required_postcondition") !=
                "player.has_skull_key=true")
        {
            throw new InvalidDataException(
                "Skull Key execution episode does not bind the compiled terminal interaction.");
        }
    }

    private static int RequiredSkullKeySpecialItemWhich(JsonElement chest)
    {
        var hasCanonical = chest.TryGetProperty(
            "special_item_which",
            out var canonical);
        var hasLegacy = chest.TryGetProperty(
            "skull_key_special_item_which",
            out var legacy);
        if (hasCanonical == hasLegacy)
        {
            throw new InvalidDataException(
                "Skull Key reward chest special-item identity is missing or ambiguous.");
        }
        var value = hasCanonical ? canonical : legacy;
        return value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException(
                "Skull Key reward chest special-item identity is invalid.");
    }

    private static void ValidateExecutionResult(
        PlanExecutionEpisodeEnvelope episode,
        TrainingExecutionResult result)
    {
        var changed = result.ChangedFacts.Where(change =>
                change.Path == "player.has_skull_key" &&
                change.Before == "false" &&
                change.After == "true")
            .ToArray();
        var requiredReasons = new[]
        {
            "native_reward_chest_open_handled",
            "native_reward_item_claimed",
            "player_has_skull_key_transition_observed"
        };
        if (result.SchemaVersion != "training_execution_result.v1" ||
            result.BeforeStateHash != episode.SourceStateHash ||
            result.Status != "applied" ||
            !result.FeedbackAvailable ||
            result.ActualTicks is not > 0 ||
            result.BlockReasons.Length != 0 ||
            result.PrimitiveKind != "claim_skull_key" ||
            result.PrimitiveVerificationStatus != "verified" ||
            !requiredReasons.All(reason =>
                result.PrimitiveVerificationReasons.Contains(
                    reason,
                    StringComparer.Ordinal)) ||
            changed.Length != 1 ||
            !result.RequestedEffect.Contains(
                "required_postcondition=player.has_skull_key=true",
                StringComparison.Ordinal) ||
            !result.ObservedEffect.Contains(
                "player.has_skull_key=true",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Skull Key execution result is not one verified native terminal claim.");
        }
    }

    private static void ValidateSnapshot(
        SnapshotEnvelope snapshot,
        string label)
    {
        if (snapshot.SchemaVersion != "snapshot.v1" ||
            snapshot.GameVersion != "1.6.15" ||
            string.IsNullOrWhiteSpace(snapshot.BridgeVersion) ||
            string.IsNullOrWhiteSpace(snapshot.StateHash) ||
            snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State))
        {
            throw new InvalidDataException(
                "Skull Key " + label + " snapshot is invalid.");
        }
    }
}
