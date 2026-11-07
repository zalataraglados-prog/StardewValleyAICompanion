using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public static class CropSeedPlantingBinding
{
    private const string Prefix = "crop_seed_planting_";
    private static readonly string[] Names = { Prefix + "version", Prefix + "commitment_id", Prefix + "reservation_id",
        Prefix + "ledger_id", Prefix + "ledger_revision", Prefix + "reservation_revision", Prefix + "source_decision_id",
        Prefix + "node_id", Prefix + "source_stack", Prefix + "slot_offset", Prefix + "batch_quantity" };
    private static readonly string[] InventoryNames = { "inventory_slot_index", "qualified_item_id", "expected_stack_before",
        "expected_stack_after", "seed_inventory_quality", "seed_id" };

    public static bool IsPresent(SmallModelActionParameter[]? parameters) => parameters?.Any(row => row is not null &&
        (row.Name?.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true ||
         string.Equals(row.Name, "seed_inventory_quality", StringComparison.OrdinalIgnoreCase))) == true;

    public static SmallModelActionParameter[] ToParameters(StrategyCommitmentLedger ledger, string commitmentId,
        string reservationId, int quality, int sourceStack, int offset, int count)
    {
        var crop = ledger.CropPlantingCommitments.Single(row => row.CommitmentId == commitmentId);
        var claim = ledger.MaterialReservations.Single(row => row.ReservationId == reservationId);
        var values = new[] { "1", commitmentId, reservationId, ledger.LedgerId, Number(ledger.Revision), Number(claim.Revision),
            crop.SourceDecisionId, claim.NodeId, Number(sourceStack), Number(offset), Number(count) };
        return Names.Select((name, index) => Parameter(name, values[index])).Concat(new[]
        {
            Parameter("inventory_slot_index", Number(claim.SlotIndex)), Parameter("qualified_item_id", claim.QualifiedItemId),
            Parameter("expected_stack_before", Number(sourceStack - offset)), Parameter("expected_stack_after", Number(sourceStack - offset - 1)),
            Parameter("seed_inventory_quality", Number(quality)), Parameter("seed_id", ObjectId(crop.SeedId))
        }).ToArray();
    }

    public static bool IsBound(ActionQueueItem item, StrategyCommitmentLedger ledger, string stateHash)
    {
        var parameters = item.NormalizedCommand.Parameters;
        if (item.OptionId != "executor.plant_seed" || item.NormalizedCommand.OptionId != item.OptionId ||
            item.NormalizedCommand.GoalId != "earn_money" || item.NormalizedCommand.StateHash != stateHash ||
            ledger.SourceStateHash != stateHash || ledger.PreparedMaterialTransfer is not null ||
            !TryInventory(parameters, out var request)) return false;
        string Read(string name) => parameters.SingleOrDefault(row => row.Name == name)?.Value ?? string.Empty;
        int? Int(string name) => Integer(Read(name));
        var crops = ledger.CropPlantingCommitments.Where(row => row.CommitmentId == Read(Names[1]) &&
            row.Status == StrategyCommitmentStatuses.Active).ToArray();
        var claims = ledger.MaterialReservations.Where(row => row.ReservationId == Read(Names[2]) &&
            row.Status == StrategyCommitmentStatuses.Active).ToArray();
        if (crops.Length != 1 || claims.Length != 1 || Read(Names[3]) != ledger.LedgerId || Int(Names[4]) != ledger.Revision)
            return false;
        var crop = crops[0];
        var claim = claims[0];
        var offset = Int(Names[9]);
        var count = Int(Names[10]);
        var stack = Int(Names[8]);
        return long.TryParse(ledger.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor) &&
            claim.OwnerPlayerId == actor && claim.OutputCustody is null && !string.IsNullOrWhiteSpace(crop.SourceDecisionId) &&
            claim.Revision == Int(Names[5]) &&
            claim.SourceStateHash == stateHash && claim.SourceDecisionId == crop.SourceDecisionId &&
            Read(Names[6]) == crop.SourceDecisionId && claim.GoalId == "earn_money" &&
            claim.Purpose == "crop_seed_supply:" + crop.CommitmentId && claim.QualifiedItemId == crop.SeedQualifiedItemId &&
            claim.QualifiedItemId == request!.QualifiedItemId && claim.NodeId == Read(Names[7]) &&
            claim.SlotIndex == request.InventorySlotIndex && ObjectId(crop.SeedId) == request.SeedId &&
            count is >= 1 && count <= CropSeedPlantingInventoryContract.MaximumBatchSize &&
            count <= claim.Quantity && offset >= 0 && offset < count && stack >= count &&
            ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active && row.NodeId == claim.NodeId &&
                row.SlotIndex == claim.SlotIndex && row.QualifiedItemId == claim.QualifiedItemId).Sum(row => (long)row.Quantity) <= stack &&
            request.ExpectedStackBefore == stack - offset;
    }

    public static bool SourceMatches(SmallModelAction action, SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, string goal)
    {
        if (ledger is null || !TryInventory(action.Parameters, out var request) || request is null) return false;
        var item = new ActionQueueItem { OptionId = action.OptionId, NormalizedCommand = new NormalizedCommand
            { OptionId = action.OptionId, GoalId = goal, StateHash = snapshot.StateHash, Parameters = action.Parameters } };
        if (!IsBound(item, ledger, snapshot.StateHash)) return false;
        var crop = ledger.CropPlantingCommitments.Single(row => row.CommitmentId ==
            action.Parameters.Single(parameter => parameter.Name == Names[1]).Value);
        if (ReadStateFieldIntOptional(snapshot, "time", "total_days") != crop.PlantingTotalDay) return false;
        var nodeId = action.Parameters.Single(row => row.Name == Names[7]).Value;
        var originalStack = Integer(action.Parameters.Single(row => row.Name == Names[8]).Value);
        if (!SelectedGraphSlotMatches(snapshot, ledger, nodeId, request!, originalStack)) return false;
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "inventory"))) return false;
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array) return false;
        return inventory.Value.EnumerateArray().Count(row => row.ValueKind == JsonValueKind.Object &&
            ExactInt(row, "slot_index", request.InventorySlotIndex) && ReadString(row, "qualified_item_id") == request.QualifiedItemId &&
            ExactInt(row, "quality", request.SeedInventoryQuality) && ExactInt(row, "stack", originalStack) &&
            row.TryGetProperty("is_empty", out var empty) && empty.ValueKind == JsonValueKind.False) == 1;
    }

    public static bool TryInventory(SmallModelActionParameter[]? parameters, out TrainingExecutionRequest? request)
    {
        request = null;
        if (parameters is null || parameters.Any(row => row is null || string.IsNullOrWhiteSpace(row.Name) || row.Value is null)) return false;
        if (Names.Concat(InventoryNames).Any(name => parameters.Count(row => row.Name == name) != 1) ||
            parameters.Any(row => row.Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && !Names.Contains(row.Name))) return false;
        string Read(string name) => parameters.Single(row => row.Name == name).Value;
        request = new TrainingExecutionRequest
        {
            CropSeedPlantingVersion = Integer(Read(Names[0])), SeedInventoryQuality = Integer(Read("seed_inventory_quality")),
            InventorySlotIndex = Integer(Read("inventory_slot_index")), QualifiedItemId = Read("qualified_item_id"), SeedId = Read("seed_id"),
            ExpectedStackBefore = Integer(Read("expected_stack_before")), ExpectedStackAfter = Integer(Read("expected_stack_after"))
        };
        return CropSeedPlantingInventoryContract.ValidShape(request);
    }

    private static int? Integer(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static bool SelectedGraphSlotMatches(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string nodeId, TrainingExecutionRequest request, int? stack)
    {
        // The scope already validates complete supply once; each action reads only its selected native slot.
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "material_inventory_graph"))) return false;
        var graph = ReadStateFieldValue(snapshot, "farm", "material_inventory_graph");
        if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object ||
            !long.TryParse(ledger.PlayerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var player) ||
            !ExactLong(graph.Value, "player_id", player) ||
            !graph.Value.TryGetProperty("inventory_nodes", out var inventories) || inventories.ValueKind != JsonValueKind.Array) return false;
        var nodes = inventories.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("node_id", out var id) && id.ValueKind == JsonValueKind.String && id.ValueEquals(nodeId)).Take(2).ToArray();
        if (nodes.Length != 1 || ReadString(nodes[0], "inventory_kind") != "player_inventory" ||
            ReadString(nodes[0], "supply_state") != "available" || !ExactLong(nodes[0], "owner_player_id", player) ||
            !nodes[0].TryGetProperty("actor_use_authorized", out var allowed) || allowed.ValueKind != JsonValueKind.True ||
            !nodes[0].TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array) return false;
        return slots.EnumerateArray().Count(row => row.ValueKind == JsonValueKind.Object &&
            ExactInt(row, "slot_index", request.InventorySlotIndex) && ReadString(row, "qualified_item_id") == request.QualifiedItemId &&
            ExactInt(row, "quality", request.SeedInventoryQuality) && ExactInt(row, "stack", stack)) == 1;
    }
    private static bool ExactLong(JsonElement row, string name, long expected) => row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var actual) && actual == expected;
    private static bool ExactInt(JsonElement row, string name, int? expected) => expected.HasValue &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var actual) && actual == expected;
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string ObjectId(string value) => value.StartsWith("(O)", StringComparison.Ordinal) ? value.Substring(3) : value;
    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}
