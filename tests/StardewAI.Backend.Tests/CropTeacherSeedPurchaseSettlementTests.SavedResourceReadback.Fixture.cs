using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // One declared full-resource template. Never dispose/delete it or mutate its inputs per negative case.
    private static readonly Lazy<OwnedRouteFixture> SavedResourceTemplate = new(() => new(prepareSource: source =>
    {
        ChangeState(source.Before, "time", time =>
        {
            // WorldReadAdapter's original calendar names/envelope shapes, not a new schema.
            time["year"] ??= JsonSerializer.SerializeToNode(new { status = "available", value = 2 });
            time["season"] ??= JsonSerializer.SerializeToNode(new { status = "available", value = "spring" });
            time["day"] ??= JsonSerializer.SerializeToNode(new { status = "available", value = 1 });
        });
    }));

    private sealed class SavedResourceFixture
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "crop-saved-resource-" + Guid.NewGuid().ToString("N"));
        internal SnapshotEnvelope Before { get; }
        internal SnapshotEnvelope After { get; }
        internal StrategyCommitmentLedger Ledger { get; }
        internal string BeforePath => Path.Combine(root, "before.json");
        internal string AfterPath => Path.Combine(root, "after.json");
        internal string LedgerPath => Path.Combine(root, "ledger.json");
        internal const string Step = "declared.saved-resource.readback";

        internal SavedResourceFixture()
        {
            var template = SavedResourceTemplate.Value.Source;
            Before = Copy(template.Before);
            After = Copy(template.Before);
            Ledger = Copy(template.Ledger);
            // These are declared volatile differences, NOT evidence that a real process reloaded a save.
            After.GameTick = 0;
            After.RealTimestamp = "2026-11-12T08:00:00Z";
            ChangeState(After, "player", player =>
            {
                player["location_id"]!["value"] = "FarmHouse";
                player["tile_x"]!["value"] = 7;
                player["tile_y"]!["value"] = 8;
            });
            ChangeState(After, "farm", farm => farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = "FarmHouse");
            Directory.CreateDirectory(root);
            Save();
        }

        internal void Save(bool hashSnapshots = true)
        {
            if (hashSnapshots)
            {
                Before.StateHash = SnapshotHash.ComputeStateHash(Before.State);
                After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            }
            Fixture.Save(BeforePath, Before);
            Fixture.Save(AfterPath, After);
            Fixture.Save(LedgerPath, Ledger);
        }

        internal CropTeacherSavedResourceReadback Open(string step = Step) =>
            CropTeacherSavedResourceReadback.Open(BeforePath, AfterPath, LedgerPath, step);

        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
        internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    }
}
