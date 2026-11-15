using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class CropSaleStockReservationContract
{
    public const string Schema = "crop_sale_stock_reservation.v1";
    public const string TransferableSchema = "crop_sale_stock_reservation.v2";
    public const string PartitionedSchema = "crop_sale_stock_reservation.v3";

    public static string Purpose(string commitmentId, string outputRetentionKey, string inventoryIdentitySha256) =>
        JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = Schema, CommitmentId = commitmentId, OutputRetentionKey = outputRetentionKey, InventoryIdentitySha256 = inventoryIdentitySha256
        });

    public static string TransferablePurpose(string commitmentId, string outputRetentionKey, string inventoryIdentitySha256,
        int originSlotIndex) => JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = TransferableSchema, CommitmentId = commitmentId, OutputRetentionKey = outputRetentionKey,
            InventoryIdentitySha256 = inventoryIdentitySha256, OriginSlotIndex = originSlotIndex
        });

    public static bool IsTransferable(CropSaleStockReservationPurpose purpose) =>
        purpose.SchemaVersion is TransferableSchema or PartitionedSchema;

    public static string PartitionReservationId(string parentId, string stepSha256, int index) =>
        parentId + ":transfer-split:" + stepSha256 + ":" + index.ToString(CultureInfo.InvariantCulture);

    public static string PartitionedPurpose(MaterialReservation parent, string stepSha256, int index)
    {
        if (!TryRead(parent, out var original) || !IsTransferable(original!) ||
            !MaterialOutputCustodyContract.IsLowerSha256(stepSha256) || index <= 0)
            throw new ArgumentException("Sale-stock partition requires an original transferable parent and explicit partition identity.");
        return JsonSerializer.Serialize(new CropSaleStockReservationPurpose
        {
            SchemaVersion = PartitionedSchema, CommitmentId = original!.CommitmentId,
            OutputRetentionKey = original.OutputRetentionKey, InventoryIdentitySha256 = original.InventoryIdentitySha256,
            OriginSlotIndex = original.OriginSlotIndex, PartitionParentId = parent.ReservationId,
            PartitionStepSha256 = stepSha256, PartitionIndex = index
        });
    }

    public static bool TryRead(MaterialReservation claim, out CropSaleStockReservationPurpose? purpose)
    {
        purpose = null;
        if (string.IsNullOrWhiteSpace(claim.Purpose) || !claim.Purpose.TrimStart().StartsWith("{", StringComparison.Ordinal)) return false;
        try { purpose = JsonSerializer.Deserialize<CropSaleStockReservationPurpose>(claim.Purpose); }
        catch (JsonException) { return false; }
        return purpose is not null &&
            !string.IsNullOrWhiteSpace(purpose.CommitmentId) && !string.IsNullOrWhiteSpace(purpose.OutputRetentionKey) &&
            MaterialOutputCustodyContract.IsLowerSha256(purpose.InventoryIdentitySha256) &&
            claim.OutputCustody is null && !string.IsNullOrWhiteSpace(claim.SourceDecisionId) && claim.Quantity > 0 &&
            claim.SlotIndex >= 0 && claim.GoalId == "earn_money" && PositionMatchesVersion(claim, purpose);
    }

    private static bool PositionMatchesVersion(MaterialReservation claim, CropSaleStockReservationPurpose purpose)
    {
        var playerNode = "player:" + claim.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return purpose.SchemaVersion switch
        {
            Schema => claim.ReservationId == ReservationId(claim.SourceDecisionId, claim.SlotIndex) && claim.NodeId == playerNode,
            TransferableSchema => purpose.OriginSlotIndex is >= 0 &&
                claim.ReservationId == ReservationId(claim.SourceDecisionId, purpose.OriginSlotIndex.Value) &&
                (claim.NodeId == playerNode || claim.NodeId?.StartsWith("chest:", StringComparison.Ordinal) == true),
            PartitionedSchema => purpose.OriginSlotIndex is >= 0 && purpose.PartitionIndex is > 0 &&
                MaterialOutputCustodyContract.IsLowerSha256(purpose.PartitionStepSha256 ?? string.Empty) &&
                HasPartitionRoot(purpose.PartitionParentId, ReservationId(claim.SourceDecisionId, purpose.OriginSlotIndex.Value)) &&
                claim.ReservationId == PartitionReservationId(purpose.PartitionParentId!, purpose.PartitionStepSha256!, purpose.PartitionIndex.Value) &&
                (claim.NodeId == playerNode || claim.NodeId?.StartsWith("chest:", StringComparison.Ordinal) == true),
            _ => false
        };
    }

    private static bool HasPartitionRoot(string? id, string root)
    {
        if (id == root) return true;
        if (id is null || !id.StartsWith(root + ":transfer-split:", StringComparison.Ordinal)) return false;
        return id.Substring(root.Length).Split(new[] { ":transfer-split:" }, StringSplitOptions.None).Skip(1).All(part =>
        {
            var fields = part.Split(':');
            return fields.Length == 2 && MaterialOutputCustodyContract.IsLowerSha256(fields[0]) &&
                int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index > 0 &&
                fields[1] == index.ToString(CultureInfo.InvariantCulture);
        });
    }

    public static string ReservationId(string producerStepId, int slotIndex) => producerStepId + ":crop-sale-stock:" +
        slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public sealed class CropSaleStockReservationPurpose
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;
    [JsonPropertyName("output_retention_key")]
    public string OutputRetentionKey { get; set; } = string.Empty;
    [JsonPropertyName("inventory_identity_sha256")]
    public string InventoryIdentitySha256 { get; set; } = string.Empty;
    [JsonPropertyName("origin_slot_index")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OriginSlotIndex { get; set; }

    [JsonPropertyName("partition_parent_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PartitionParentId { get; set; }
    [JsonPropertyName("partition_step_sha256"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PartitionStepSha256 { get; set; }
    [JsonPropertyName("partition_index"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PartitionIndex { get; set; }
}
