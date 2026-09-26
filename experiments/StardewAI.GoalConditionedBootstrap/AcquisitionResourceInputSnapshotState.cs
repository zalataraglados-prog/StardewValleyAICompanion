using System.Globalization;
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

    public MaterialInventoryGraph? MaterialGraph =>
        materialSupply.Value.Graph;

    public AcquisitionResourceMaterialSlot[] MaterialSlots =>
        materialSupply.Value.Slots;

    public bool RodEvidenceAvailable => rodInventory.Value.Available;

    public AcquisitionResourceRodState[] Rods => rodInventory.Value.Rods;

    public string[] RodBlockingReasons => rodInventory.Value.BlockingReasons;

    public JsonElement CrabPotNetwork => crabPotNetwork.Value;

    public int AvailableQuantity(string qualifiedItemId) =>
        materialSupply.Value.Quantities.GetValueOrDefault(qualifiedItemId);

    public AcquisitionPlayerInventoryQuantity PlayerInventoryQuantity(
        string selector,
        string qualifiedItemId)
    {
        if (!MaterialEvidenceAvailable || MaterialGraph is null)
        {
            return AcquisitionPlayerInventoryQuantity.Blocked(
                MaterialBlockingReasons.Length > 0
                    ? MaterialBlockingReasons[0]
                    : "material_inventory_graph_missing_or_unavailable");
        }
        var selectedPlayerId = selector.Equals(
                "Current",
                StringComparison.OrdinalIgnoreCase)
            ? MaterialGraph.PlayerId
            : long.TryParse(selector, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var playerId)
                ? playerId
                : long.MinValue;
        if (selectedPlayerId != MaterialGraph.PlayerId)
        {
            return AcquisitionPlayerInventoryQuantity.Blocked(
                "player_inventory_selector_not_bound:" + selector);
        }
        var nodes = MaterialGraph.InventoryNodes.Where(node =>
                node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == selectedPlayerId)
            .ToArray();
        if (nodes.Length != 1 ||
            nodes[0].SupplyState != "available" ||
            !nodes[0].ActorUseAuthorized)
        {
            return AcquisitionPlayerInventoryQuantity.Blocked(
                "current_player_inventory_node_unavailable");
        }
        var quantity = nodes[0].Slots.Where(slot =>
                slot.QualifiedItemId == qualifiedItemId)
            .Sum(slot => (long)slot.Stack);
        if (quantity > int.MaxValue)
        {
            return AcquisitionPlayerInventoryQuantity.Blocked(
                "current_player_inventory_quantity_overflow");
        }
        return new AcquisitionPlayerInventoryQuantity(
            true,
            (int)quantity,
            string.Empty);
    }

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
                        slot.Stack <= 0 ||
                        slot.ContextTags is null)))
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
                    null,
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    Array.Empty<AcquisitionResourceMaterialSlot>(),
                    projection.BlockingReasons.Length > 0
                        ? projection.BlockingReasons
                        : new[] { "material_supply_projection_blocked" });
            }
            var quantities = projection.Quantities.ToDictionary(
                row => row.QualifiedItemId,
                row => row.AvailableQuantity,
                StringComparer.Ordinal);
            var graphSlots = graph.InventoryNodes
                .SelectMany(node => node.Slots.Select(slot => new
                {
                    node.NodeId,
                    Slot = slot
                }))
                .ToDictionary(
                    row => SlotKey(row.NodeId, row.Slot.SlotIndex),
                    row => row.Slot,
                    StringComparer.Ordinal);
            var slots = projection.Slots.Select(slot =>
            {
                var graphSlot = graphSlots[SlotKey(
                    slot.NodeId,
                    slot.SlotIndex)];
                return new AcquisitionResourceMaterialSlot(
                    slot.NodeId,
                    slot.SlotIndex,
                    slot.QualifiedItemId,
                    slot.AvailableQuantity,
                    graphSlot.ContextTags,
                    graphSlot.ContextTagsProjectionStatus,
                    graphSlot.Edibility,
                    graphSlot.EdibilityProjectionStatus);
            }).ToArray();
            return new MaterialReadResult(
                true,
                graph,
                quantities,
                slots,
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


    private sealed record MaterialReadResult(
        bool Available,
        MaterialInventoryGraph? Graph,
        IReadOnlyDictionary<string, int> Quantities,
        AcquisitionResourceMaterialSlot[] Slots,
        string[] BlockingReasons)
    {
        public static MaterialReadResult Blocked(string reason) => new(
            false,
            null,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<AcquisitionResourceMaterialSlot>(),
            new[] { reason });
    }

    private static string SlotKey(string nodeId, int slotIndex) =>
        nodeId + "#" + slotIndex;

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

internal sealed record AcquisitionResourceMaterialSlot(
    string NodeId,
    int SlotIndex,
    string QualifiedItemId,
    int AvailableQuantity,
    string[] ContextTags,
    string ContextTagsProjectionStatus,
    int? Edibility,
    string EdibilityProjectionStatus);

internal sealed record AcquisitionPlayerInventoryQuantity(
    bool EvidenceAvailable,
    int Quantity,
    string BlockingReason)
{
    public static AcquisitionPlayerInventoryQuantity Blocked(string reason) =>
        new(false, 0, reason);
}
