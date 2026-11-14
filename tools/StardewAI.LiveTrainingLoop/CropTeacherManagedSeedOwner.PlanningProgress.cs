using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropTeacherManagedPlanningProgress(IReadOnlySet<string> CompletedHarvestTileKeys,
    IReadOnlySet<string> CompletedDayBoundaryKeys);

public sealed partial class CropTeacherManagedSeedOwner
{
    internal CropTeacherManagedPlanningProgress ReadPlanningProgress()
    {
        gate.Wait();
        try
        {
            ValidateState();
            if (Pending is not null || HeadRecoveryRequired)
                throw new InvalidOperationException("Recover the original owner before reading accepted planning progress.");
            var harvest = new HashSet<string>(StringComparer.Ordinal);
            var nights = new HashSet<string>(StringComparer.Ordinal);
            var previous = headerHash;
            var expectedSequence = 1;
            // Consume the original index's small acceptance certificates, not historical snapshot graphs.
            foreach (var (sequence, path, hash) in acceptedHistory)
            {
                if (HashFile(path) != hash) throw new InvalidDataException("Accepted planning certificate changed.");
                var accepted = Read<JsonObject>(path);
                if (HashFile(path) != hash || sequence != expectedSequence++ || Sequence(accepted) != sequence ||
                    Text(accepted, "run_id") != options.RunId || Text(accepted, "previous_accepted_sha256") != previous ||
                    accepted["original_boundary_verified"]?.GetValue<bool>() != true)
                    throw new InvalidDataException("Accepted planning progress lost its original owner chain.");
                previous = hash;
                var kind = accepted["operation_kind"]?.GetValue<string>();
                if (kind == "overnight_control")
                {
                    var from = accepted["from_total_day"]!.GetValue<int>();
                    if (accepted["native_outcome_verified"]?.GetValue<bool>() != true ||
                        accepted["native_overnight_control_verified"]?.GetValue<bool>() != true ||
                        accepted["native_save_boundary_verified"]?.GetValue<bool>() != true || from < 0 ||
                        accepted["to_total_day"]!.GetValue<int>() != checked(from + 1))
                        throw new InvalidDataException("Accepted planning night lacks its original native save boundary.");
                    nights.Add(CropTeacherTransitionLedger.DayBoundaryKey(from));
                }
                else if (kind == "crop_harvest")
                {
                    if (accepted["native_outcome_verified"]?.GetValue<bool>() != true ||
                        accepted["native_harvest_prefix_verified"]?.GetValue<bool>() != true)
                        throw new InvalidDataException("Accepted planning harvest lacks its original native prefix.");
                    var intentPath = RecordPath(sequence, "intent");
                    var intentHash = Text(accepted, "admission_sha256");
                    if (HashFile(intentPath) != intentHash) throw new InvalidDataException("Accepted harvest admission changed.");
                    var step = Read<JsonObject>(intentPath)["harvest_step"]!.Deserialize<CropTeacherRunStep>(JsonOptions)!;
                    var tiles = accepted["applied_tiles"]!.Deserialize<MoneyRevenueCropPlantingTile[]>(JsonOptions)!;
                    if (HashFile(intentPath) != intentHash || tiles.Length != accepted["applied_tile_count"]!.GetValue<int>())
                        throw new InvalidDataException("Accepted planning harvest prefix changed.");
                    foreach (var tile in tiles)
                        harvest.Add(CropTeacherTransitionLedger.HarvestTileKey(step.CommitmentId, step.TotalDay, tile.TileX, tile.TileY));
                }
            }
            if (previous != lastAcceptedHash)
                throw new InvalidDataException("Accepted planning progress does not reach the current owner head.");
            ValidateState();
            return new(harvest, nights);
        }
        finally { gate.Release(); }
    }
}
