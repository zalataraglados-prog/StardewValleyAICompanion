using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionResourceInputSnapshotState
{
    private const string MagicBaitQualifiedItemId = "(O)908";
    private readonly Lazy<MaterialReadResult> materialSupply;
    private readonly Lazy<RodReadResult> rodInventory;
    private readonly Lazy<JsonElement> crabPotNetwork;

    private AcquisitionResourceInputSnapshotState(JsonElement state)
    {
        ShopQuotes = new AcquisitionShopQuoteSnapshotState(state);
        materialSupply = new Lazy<MaterialReadResult>(
            () => ReadMaterialSupply(state));
        rodInventory = new Lazy<RodReadResult>(() => ReadRods(state));
        crabPotNetwork = new Lazy<JsonElement>(() => TryFieldValue(
                state,
                "player",
                "crab_pot_network",
                out var value)
            ? value.Clone()
            : default);
    }

    public bool MaterialEvidenceAvailable => materialSupply.Value.Available;

    public AcquisitionShopQuoteSnapshotState ShopQuotes { get; }

    public string[] MaterialBlockingReasons =>
        materialSupply.Value.BlockingReasons;

    public bool RodEvidenceAvailable => rodInventory.Value.Available;

    public AcquisitionResourceRodState[] Rods => rodInventory.Value.Rods;

    public string[] RodBlockingReasons => rodInventory.Value.BlockingReasons;

    public JsonElement CrabPotNetwork => crabPotNetwork.Value;

    public int AvailableQuantity(string qualifiedItemId) =>
        materialSupply.Value.Quantities.GetValueOrDefault(qualifiedItemId);

    public bool HasMagicBaitCapableRod =>
        Rods.Any(rod => rod.CanUseBait);

    public int AttachedMagicBaitQuantity => Rods.Sum(rod =>
        rod.HasMagicBait ? rod.BaitStack : 0);

    public static AcquisitionResourceInputSnapshotState Read(
        JsonElement snapshot)
    {
        var state = RequiredObject(snapshot, "state");
        return new AcquisitionResourceInputSnapshotState(state);
    }

    private static MaterialReadResult ReadMaterialSupply(JsonElement state)
    {
        if (!TryFieldEnvelope(
                state,
                "farm",
                "material_inventory_graph",
                out _,
                out var value))
        {
            return MaterialReadResult.Blocked(
                "material_inventory_graph_missing_or_unavailable");
        }
        try
        {
            var graph = JsonSerializer.Deserialize<MaterialInventoryGraph>(
                value.GetRawText(),
                JsonDefaults.Options);
            if (graph is null ||
                graph.SchemaVersion != "material_inventory_graph.v1" ||
                graph.Status != "available" ||
                graph.DefaultSharedResourcePolicy !=
                    "deny_without_explicit_authorization" ||
                graph.PhysicalInventoryCount != graph.InventoryNodes.Length ||
                graph.AccessPointCount != graph.AccessPoints.Length)
            {
                return MaterialReadResult.Blocked(
                    "material_inventory_graph_contract_invalid");
            }
            if (graph.InventoryNodes.Any(node =>
                    string.IsNullOrWhiteSpace(node.NodeId) ||
                    string.IsNullOrWhiteSpace(node.SupplyState) ||
                    node.Slots.Any(slot =>
                        slot.SlotIndex < 0 ||
                        string.IsNullOrWhiteSpace(slot.QualifiedItemId) ||
                        slot.Stack <= 0)))
            {
                return MaterialReadResult.Blocked(
                    "material_inventory_graph_node_or_slot_invalid");
            }
            var projection = new MaterialSupplyProjection().Project(graph);
            if (projection.Status != "available" ||
                projection.BlockingReasons.Length > 0)
            {
                return new MaterialReadResult(
                    false,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    projection.BlockingReasons.Length > 0
                        ? projection.BlockingReasons
                        : new[] { "material_supply_projection_blocked" });
            }
            var quantities = projection.Quantities.ToDictionary(
                row => row.QualifiedItemId,
                row => row.AvailableQuantity,
                StringComparer.Ordinal);
            return new MaterialReadResult(
                true,
                quantities,
                Array.Empty<string>());
        }
        catch (Exception ex) when (ex is JsonException or OverflowException)
        {
            return MaterialReadResult.Blocked(
                "material_inventory_graph_parse_invalid");
        }
    }

    private static RodReadResult ReadRods(JsonElement state)
    {
        if (!TryFieldEnvelope(
                state,
                "fishing",
                "rod_inventory",
                out _,
                out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return RodReadResult.Blocked(
                "fishing_rod_inventory_missing_or_unavailable");
        }
        var rows = new List<AcquisitionResourceRodState>();
        foreach (var row in value.EnumerateArray())
        {
            var qualifiedItemId = ReadString(row, "qualified_item_id");
            var slot = ReadInt(row, "slot_index");
            var canUseBait = ReadBool(row, "can_use_bait");
            var hasMagicBait = ReadBool(row, "has_magic_bait");
            if (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !slot.HasValue || slot < 0 || !canUseBait.HasValue ||
                !hasMagicBait.HasValue)
            {
                return RodReadResult.Blocked(
                    "fishing_rod_inventory_row_invalid");
            }
            var baitStack = 0;
            if (hasMagicBait.Value)
            {
                var stack = row.TryGetProperty("bait", out var bait) &&
                    bait.ValueKind == JsonValueKind.Object
                        ? ReadInt(bait, "stack")
                        : null;
                if (bait.ValueKind != JsonValueKind.Object ||
                    ReadString(bait, "qualified_item_id") !=
                        MagicBaitQualifiedItemId ||
                    !stack.HasValue || stack <= 0)
                {
                    return RodReadResult.Blocked(
                        "magic_bait_attachment_row_invalid");
                }
                baitStack = stack.Value;
            }
            rows.Add(new AcquisitionResourceRodState(
                slot.Value,
                qualifiedItemId,
                canUseBait.Value,
                hasMagicBait.Value,
                baitStack));
        }
        if (rows.Select(row => row.SlotIndex).Distinct().Count() != rows.Count)
            return RodReadResult.Blocked("fishing_rod_slot_duplicate");
        return new RodReadResult(
            true,
            rows.ToArray(),
            Array.Empty<string>());
    }

    private static bool TryFieldEnvelope(
        JsonElement state,
        string section,
        string field,
        out JsonElement envelope,
        out JsonElement value)
    {
        envelope = default;
        value = default;
        return state.TryGetProperty(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() == "available" &&
            envelope.TryGetProperty("confidence", out var confidence) &&
            confidence.TryGetDouble(out var confidenceValue) &&
            confidenceValue == 1d &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool TryFieldValue(
        JsonElement state,
        string section,
        string field,
        out JsonElement value) =>
        TryFieldEnvelope(state, section, field, out _, out value);

    private static JsonElement RequiredObject(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var result) &&
                result.ValueKind == JsonValueKind.Object,
            "Snapshot " + name + " is missing.");
        return result;
    }

    internal static string ReadString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    internal static int? ReadInt(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.TryGetInt32(out var result)
            ? result
            : null;

    internal static bool? ReadBool(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record MaterialReadResult(
        bool Available,
        IReadOnlyDictionary<string, int> Quantities,
        string[] BlockingReasons)
    {
        public static MaterialReadResult Blocked(string reason) => new(
            false,
            new Dictionary<string, int>(StringComparer.Ordinal),
            new[] { reason });
    }

    private sealed record RodReadResult(
        bool Available,
        AcquisitionResourceRodState[] Rods,
        string[] BlockingReasons)
    {
        public static RodReadResult Blocked(string reason) => new(
            false,
            Array.Empty<AcquisitionResourceRodState>(),
            new[] { reason });
    }
}

internal sealed record AcquisitionResourceRodState(
    int SlotIndex,
    string QualifiedItemId,
    bool CanUseBait,
    bool HasMagicBait,
    int BaitStack);
