using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteApplyTreeTreatment(
        TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Vector2(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var requested = "current_location.terrain_features[" + (int)target.X + "," + (int)target.Y + "].has_moss=false;" +
            "current_location.terrain_features[" + (int)target.X + "," + (int)target.Y + "].stop_growing_moss=true";
        if (!location.terrainFeatures.TryGetValue(target, out var feature) ||
            feature is not Tree tree || tree.GetType() != typeof(Tree) ||
            !string.Equals(request.TargetRuntimeType, typeof(Tree).FullName, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "apply_tree_treatment", requested,
                "target_runtime_type=" + feature?.GetType().FullName,
                "apply_tree_treatment_target_identity_drift");
        }
        if (tree.stopGrowingMoss.Value)
        {
            return BlockedWithPrimitive(request, "apply_tree_treatment", requested,
                "stop_growing_moss=true", "apply_tree_treatment_native_rule_blocked");
        }

        var slotIndex = request.SlotIndex;
        if (!slotIndex.HasValue || slotIndex.Value < 0 || slotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[slotIndex.Value] is not StardewObject treatment ||
            treatment.GetType() != typeof(StardewObject) || treatment.Stack <= 0 ||
            !string.Equals(treatment.QualifiedItemId, "(O)419", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, "(O)419", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "apply_tree_treatment", requested,
                "inventory_identity_mismatch", "apply_tree_treatment_inventory_identity_drift");
        }

        var beforeHasMoss = tree.hasMoss.Value;
        var beforeStopGrowingMoss = tree.stopGrowingMoss.Value;
        var beforeStack = treatment.Stack;
        var previousSlot = Game1.player.CurrentToolIndex;
        var applied = false;
        try
        {
            Game1.player.CurrentToolIndex = slotIndex.Value;
            if (!ReferenceEquals(Game1.player.ActiveObject, treatment))
            {
                return BlockedWithPrimitive(request, "apply_tree_treatment", requested,
                    "active_object_identity_mismatch", "apply_tree_treatment_active_slot_drift");
            }

            applied = treatment.placementAction(
                location,
                (int)target.X * Game1.tileSize,
                (int)target.Y * Game1.tileSize,
                Game1.player);
            if (applied)
            {
                ConsumeOneInventoryItem(slotIndex.Value);
            }
        }
        finally
        {
            Game1.player.CurrentToolIndex = previousSlot;
        }

        var afterStack = Game1.player.Items.ElementAtOrDefault(slotIndex.Value)?.Stack ?? 0;
        var verified = applied && !tree.hasMoss.Value && tree.stopGrowingMoss.Value &&
            afterStack == beforeStack - 1;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "apply_tree_treatment",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_Object_placementAction_applied_vinegar", "exact_tree_moss_flags_and_stack_verified" }
                : new[] { "apply_tree_treatment_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = "has_moss_before=" + beforeHasMoss.ToString().ToLowerInvariant() +
                ";has_moss_after=" + tree.hasMoss.Value.ToString().ToLowerInvariant() +
                ";stop_growing_moss_before=" + beforeStopGrowingMoss.ToString().ToLowerInvariant() +
                ";stop_growing_moss_after=" + tree.stopGrowingMoss.Value.ToString().ToLowerInvariant() +
                ";stack_before=" + beforeStack + ";stack_after=" + afterStack,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "apply_tree_treatment_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "current_location.terrain_features[" + (int)target.X + "," + (int)target.Y + "].has_moss", Before = beforeHasMoss.ToString().ToLowerInvariant(), After = "false" },
                    new SimulatedFactChange { Path = "current_location.terrain_features[" + (int)target.X + "," + (int)target.Y + "].stop_growing_moss", Before = beforeStopGrowingMoss.ToString().ToLowerInvariant(), After = "true" },
                    new SimulatedFactChange { Path = "player.inventory[" + slotIndex.Value + "].stack", Before = beforeStack.ToString(), After = afterStack.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
