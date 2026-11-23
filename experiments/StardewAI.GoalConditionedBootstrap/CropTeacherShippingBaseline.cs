using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

// Pending proceeds from unrelated stock already in this exact native bin.
public sealed record CropTeacherShippingBaseline(long Value, string ContentsSha256)
{
    public static CropTeacherShippingBaseline Read(JsonElement bin, string cropId)
    {
        if (!bin.TryGetProperty("contents_truncated", out var truncated) || truncated.ValueKind != JsonValueKind.False ||
            !bin.TryGetProperty("contents", out var contents) || contents.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Terminal shipping baseline requires complete original bin contents.");
        var rows = contents.EnumerateArray()
            .Where(row => row.GetProperty("qualified_item_id").GetString() != cropId)
            .OrderBy(row => row.GetProperty("qualified_item_id").GetString(), StringComparer.Ordinal)
            .ThenBy(row => row.GetProperty("quality").GetInt32())
            .ThenBy(row => row.GetProperty("unit_sell_to_store_price").GetInt32()).ToArray();
        long total = 0;
        foreach (var row in rows)
        {
            var count = row.GetProperty("count").GetInt32();
            var price = row.GetProperty("unit_sell_to_store_price").GetInt32();
            var value = checked((long)count * price);
            if (count <= 0 || price < 0 || value != row.GetProperty("total_sell_to_store_price").GetInt64())
                throw new InvalidDataException("Terminal shipping baseline contains unverified pending value.");
            total = checked(total + value);
        }
        var canonical = SnapshotHash.Canonicalize(JsonSerializer.SerializeToElement(rows));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(total, digest);
    }
}
