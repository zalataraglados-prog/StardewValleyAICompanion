using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Backend.Tests;

internal static class FreshSaveBoundarySnapshotFixture
{
    // Declared metadata only, never a substitute for original native execution.
    internal static SnapshotEnvelope Create()
    {
        var sources = GoldenWalnutNativeSourceCatalog.Sources.Select(source => new
        {
            source_key = source.SourceKey, native_quantity = source.NativeQuantity, collected_quantity = 0,
            remaining_quantity = source.NativeQuantity, acquisition_option_id = source.AcquisitionOptionId
        }).ToArray();
        var snapshot = new SnapshotEnvelope
        {
            GameVersion = GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion, GameTick = 1,
            SaveId = new FieldEnvelope<string?> { Status = FieldStatus.Available, Value = "archive-fixture-save" },
            PlayerId = new FieldEnvelope<string?> { Status = FieldStatus.Available, Value = "archive-fixture-player" },
            State = new Dictionary<string, JsonElement>
            {
                ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = 0 } }),
                ["locations"] = JsonSerializer.SerializeToElement(new { ginger_island_access = new { status = "available", value = new
                {
                    schema_version = "ginger_island_access.v1", journey = new { island_reached = false },
                    first_parrot = new { complete_or_pending = false }, western_turtle_parrot = new { complete_or_pending = false },
                    island_house_parrot = new { complete_or_pending = false }
                } } }),
                ["world_progress"] = JsonSerializer.SerializeToElement(new { golden_walnut_sources = new { status = "available", value = new
                {
                    schema_version = GoldenWalnutNativeSourceCatalog.SchemaVersion, projection_status = GoldenWalnutNativeSourceCatalog.ProjectionStatus,
                    native_source_key_count = GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount, native_walnut_total = GoldenWalnutNativeSourceCatalog.NativeWalnutTotal,
                    tracked_collected_quantity = 0, world_golden_walnuts_found = 0, sources
                } } })
            }
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
