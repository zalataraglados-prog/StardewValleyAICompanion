using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

// A linked immutable value, so record equality survives journal serialization/reopen.
public sealed record CropTeacherTerminalVariant(int SlotIndex, int Quality, int UnitPrice,
    int Quantity, CropTeacherTerminalVariant? Next = null);
public sealed record CropTeacherTerminalVariantPlan(string SchemaVersion, string BinScope,
    CropTeacherTerminalVariant First);

// Selected once from an already verified retained wave; the owner supplies the accepted cursor.
public sealed record CropTeacherTerminalShippingTarget(
    string RetentionProofPath, string RetentionProofSha256, string SaveId, string PlayerId,
    string GameVersion, int TotalDay, string QualifiedItemId, int SlotIndex, int Quality,
    int UnitPrice, int BinTileX, int BinTileY, int StandTileX, int StandTileY,
    int InitialQuantity, int RequiredQuantity, int AcceptedDepositCount)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CropTeacherTerminalVariantPlan? NativeVariants { get; init; }

    public IEnumerable<CropTeacherTerminalVariant> ReadNativeVariants()
    {
        if (NativeVariants is null || NativeVariants.SchemaVersion != "crop_teacher_native_terminal_variants.v1" ||
            string.IsNullOrWhiteSpace(NativeVariants.BinScope))
            throw new InvalidDataException("Missing/unknown native terminal variant plan.");
        var previous = -1; var quantity = 0;
        for (var row = NativeVariants.First; row is not null; row = row.Next)
        {
            if (row.SlotIndex <= previous || row.Quality < 0 || row.UnitPrice <= 0 || row.Quantity <= 0)
                throw new InvalidDataException("Native terminal variants must be unique ordered retained slots.");
            previous = row.SlotIndex; quantity = checked(quantity + row.Quantity);
            if (quantity > RequiredQuantity) throw new InvalidDataException("Native terminal variant quantity exceeds obligation.");
            yield return row;
        }
        var first = NativeVariants.First;
        if (quantity != RequiredQuantity || quantity <= 0 || first is null || first.SlotIndex != SlotIndex ||
            first.Quality != Quality || first.UnitPrice != UnitPrice || first.Quantity != InitialQuantity)
            throw new InvalidDataException("Native terminal variants differ from selected target/whole obligation.");
    }

    public CropTeacherTerminalVariant VariantAt(int count)
    {
        var rows = ReadNativeVariants().ToArray();
        if (count < 0 || count >= RequiredQuantity) throw new InvalidDataException("Native terminal cursor is outside obligation.");
        foreach (var row in rows) { if (count < row.Quantity) return row; count -= row.Quantity; }
        throw new InvalidDataException("Native terminal cursor has no selected slot.");
    }

    public long PendingValueAt(int count)
    {
        if (count < 0 || count > RequiredQuantity) throw new InvalidDataException("Terminal cursor is outside obligation.");
        if (NativeVariants is null) return checked((long)count * UnitPrice);
        long value = 0;
        foreach (var row in ReadNativeVariants())
        {
            var units = Math.Min(count, row.Quantity);
            value = checked(value + (long)units * row.UnitPrice); count -= units;
        }
        return value;
    }

    public void VerifyNativeVariantSource(SnapshotEnvelope source, int count, long minimumGrossRevenue = 0)
    {
        var rows = ReadNativeVariants().ToArray();
        if (PendingValueAt(RequiredQuantity) < minimumGrossRevenue || source.SaveId.Value != SaveId ||
            source.PlayerId.Value != PlayerId || source.GameVersion != GameVersion)
            throw new InvalidDataException("Native terminal retained actor/obligation differs.");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(source, "native terminal variant source");
        JsonElement Field(string domain, string name)
        {
            var field = source.State[domain].GetProperty(name);
            if (!field.TryGetProperty("status", out var status) || status.GetString() is not ("available" or "derived"))
                throw new InvalidDataException("Native terminal source field unavailable.");
            return field.GetProperty("value");
        }
        if (Field("time", "total_days").GetInt32() != TotalDay || Field("player", "location_id").GetString() != "Farm")
            throw new InvalidDataException("Native terminal source day/location differs.");
        var remainingCursor = count;
        foreach (var row in rows)
        {
            var consumed = Math.Min(remainingCursor, row.Quantity); remainingCursor -= consumed;
            var slots = Field("player", "inventory").EnumerateArray().Where(slot => slot.GetProperty("slot_index").GetInt32() == row.SlotIndex).ToArray();
            if (slots.Length != 1) throw new InvalidDataException("Native terminal slot is missing or duplicated.");
            var slot = slots[0]; var remaining = row.Quantity - consumed;
            var empty = slot.TryGetProperty("is_empty", out var emptyValue) && emptyValue.ValueKind == JsonValueKind.True;
            if (remaining == 0 ? !empty : empty || slot.GetProperty("qualified_item_id").GetString() != QualifiedItemId ||
                slot.GetProperty("stack").GetInt32() != remaining || slot.GetProperty("quality").GetInt32() != row.Quality ||
                slot.GetProperty("sell_to_store_price").GetInt32() != row.UnitPrice || !slot.GetProperty("can_be_shipped").GetBoolean())
                throw new InvalidDataException("Native terminal slot/quality/price/quantity differs from accepted prefix.");
        }
        var bins = Field("farm", "shipping_bins").EnumerateArray().Where(bin => bin.GetProperty("tile_x").GetInt32() == BinTileX &&
            bin.GetProperty("tile_y").GetInt32() == BinTileY).ToArray();
        if (bins.Length != 1) throw new InvalidDataException("Native terminal bin is missing or duplicated.");
        var selected = bins[0];
        if (!selected.GetProperty("completed").GetBoolean() || selected.GetProperty("bin_scope").GetString() != NativeVariants!.BinScope ||
            selected.GetProperty("player_id").ToString() != PlayerId || selected.GetProperty("interaction_stand_tile_x").GetInt32() != StandTileX ||
            selected.GetProperty("interaction_stand_tile_y").GetInt32() != StandTileY ||
            !string.IsNullOrWhiteSpace(selected.GetProperty("interaction_stand_tile_blocked_reason").GetString()) ||
            !selected.GetProperty("pending_settlement_value_complete").GetBoolean() ||
            selected.GetProperty("pending_settlement_value").GetInt64() != PendingValueAt(count))
            throw new InvalidDataException("Native terminal bin/custody/pending value differs from accepted prefix.");
    }
}
