using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupIdleMachineTarget(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_idle_machine_target",
                "location.machines[target].present=true",
                "target_tile=missing",
                "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var locationId = string.IsNullOrWhiteSpace(request.LocationId)
            ? "Farm"
            : request.LocationId;
        var location = Game1.getLocationFromName(locationId);
        if (location is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_idle_machine_target",
                "location.machines[target].present=true",
                "location_id=" + locationId,
                "fixture_location_not_found");
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var machineItemId = string.IsNullOrWhiteSpace(
            request.ExpectedShopId)
                ? "12"
                : request.ExpectedShopId;
        var beforeMachine = MachineObservedEffect(location, target);
        location.objects.Remove(tile);
        var machine = new StardewValley.Object(
            tile,
            machineItemId);
        machine.MinutesUntilReady = -1;
        machine.readyForHarvest.Value = false;
        machine.heldObject.Value = null;
        location.objects[tile] = machine;
        var observed = MachineAt(location, target);
        var verified = observed is not null &&
            string.Equals(
                observed.QualifiedItemId,
                machine.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase);
        RefreshTransparentMachineProbeCache();
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
            PrimitiveKind = "debug_setup_idle_machine_target",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_runtime_fixture_idle_machine_present",
                    "location_id=" + locationId,
                    "qualified_item_id=" + machine.QualifiedItemId
                }
                : new[]
                {
                    "fixture_idle_machine_identity_mismatch",
                    "location_id=" + locationId
                },
            RequestedEffect =
                "location.machines[" + locationId + ":" +
                target.X + "," + target.Y + "].present=true",
            ObservedEffect =
                MachineObservedEffect(location, target) +
                ";location_id=" + locationId,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "fixture_idle_machine_setup_failed" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "locations[" + locationId +
                            "].machines[" + target.X + "," +
                            target.Y + "]",
                        Before = beforeMachine,
                        After = MachineObservedEffect(
                            location,
                            target)
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
