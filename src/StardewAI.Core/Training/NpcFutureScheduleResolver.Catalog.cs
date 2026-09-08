using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed partial class NpcFutureScheduleResolver
    {
        private sealed class CatalogEntry
        {
            public string Key { get; set; } = string.Empty;

            public string Raw { get; set; } = string.Empty;

            public string RawSha256 { get; set; } = string.Empty;
        }

        private sealed class CatalogNpc
        {
            public string Name { get; set; } = string.Empty;

            public string AssetName { get; set; } = string.Empty;

            public string DefaultMap { get; set; } = string.Empty;

            public int DefaultTileX { get; set; }

            public int DefaultTileY { get; set; }

            public bool HasMasterSchedule { get; set; }

            public Dictionary<string, CatalogEntry> Entries { get; set; } =
                new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        }

        private static bool TryReadCatalog(
            JsonElement source,
            string npcName,
            out CatalogNpc catalogNpc,
            out string reason)
        {
            catalogNpc = new CatalogNpc();
            reason = string.Empty;
            if (source.ValueKind != JsonValueKind.Object ||
                !TryReadString(source, "population_owner", out var populationOwner) ||
                populationOwner != "Utility.ForEachVillager(includeEventActors:false)" ||
                !TryReadString(source, "selection_owner", out var selectionOwner) ||
                selectionOwner != "NPC.TryLoadSchedule" ||
                !TryReadString(source, "parser_owner", out var parserOwner) ||
                parserOwner != "NPC.parseMasterSchedule" ||
                !TryReadString(source, "projection_status", out var projectionStatus) ||
                projectionStatus != "complete_live_master_schedule_catalog_conditional_future_resolution_pending" ||
                !TryReadString(source, "catalog_sha256", out var expectedCatalogSha) ||
                !source.TryGetProperty("villagers", out var villagers) ||
                villagers.ValueKind != JsonValueKind.Array)
            {
                reason = "future_schedule_catalog_contract_invalid";
                return false;
            }

            var fingerprint = new StringBuilder();
            var matches = new List<CatalogNpc>();
            foreach (var row in villagers.EnumerateArray())
            {
                if (!TryReadCatalogNpc(row, fingerprint, out var parsedNpc, out reason))
                    return false;
                if (string.Equals(parsedNpc.Name, npcName, StringComparison.Ordinal))
                    matches.Add(parsedNpc);
            }

            if (!FixedTimeEquals(expectedCatalogSha, Sha256(fingerprint.ToString())))
            {
                reason = "future_schedule_catalog_sha256_mismatch";
                return false;
            }
            if (matches.Count == 0)
            {
                reason = "future_schedule_npc_absent_from_native_population";
                return false;
            }
            if (matches.Skip(1).Any(candidate => !EquivalentScheduleIdentity(matches[0], candidate)))
            {
                reason = "future_schedule_catalog_ambiguous_target_instances";
                return false;
            }

            catalogNpc = matches[0];
            return true;
        }

        private static bool EquivalentScheduleIdentity(CatalogNpc left, CatalogNpc right)
        {
            if (!string.Equals(left.AssetName, right.AssetName, StringComparison.Ordinal) ||
                left.HasMasterSchedule != right.HasMasterSchedule)
                return false;
            if (!left.HasMasterSchedule)
                return true;
            if (!string.Equals(left.DefaultMap, right.DefaultMap, StringComparison.Ordinal) ||
                left.DefaultTileX != right.DefaultTileX ||
                left.DefaultTileY != right.DefaultTileY ||
                left.Entries.Count != right.Entries.Count)
                return false;
            foreach (var pair in left.Entries)
            {
                if (!right.Entries.TryGetValue(pair.Key, out var other) ||
                    !FixedTimeEquals(pair.Value.RawSha256, other.RawSha256))
                    return false;
            }
            return true;
        }

        private static bool TryReadCatalogNpc(
            JsonElement row,
            StringBuilder fingerprint,
            out CatalogNpc npc,
            out string reason)
        {
            npc = new CatalogNpc();
            reason = string.Empty;
            if (row.ValueKind != JsonValueKind.Object ||
                !TryReadString(row, "npc_name", out var name) ||
                !TryReadString(row, "schedule_asset_name", out var assetName) ||
                !TryReadString(row, "default_map", out var defaultMap) ||
                !TryReadBool(row, "is_villager", out var isVillager) || !isVillager ||
                !TryReadBool(row, "event_actor", out var eventActor) || eventActor ||
                !TryReadInt(row, "default_tile_x", out var defaultTileX) ||
                !TryReadInt(row, "default_tile_y", out var defaultTileY) ||
                !TryReadBool(row, "master_schedule_present", out var present) ||
                !TryReadInt(row, "master_schedule_entry_count", out var declaredCount) ||
                declaredCount < 0 ||
                !row.TryGetProperty("master_schedule_entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                reason = "future_schedule_catalog_villager_row_invalid";
                return false;
            }

            fingerprint
                .Append(name).Append('\u001f')
                .Append(assetName).Append('\u001f')
                .Append(present ? "<present>" : "<missing>").Append('\u001e');
            var parsedEntries = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
            string? previousKey = null;
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !TryReadString(entry, "schedule_key", out var key) ||
                    !entry.TryGetProperty("raw_schedule", out var rawValue) ||
                    rawValue.ValueKind != JsonValueKind.String ||
                    !TryReadString(entry, "raw_schedule_sha256", out var expectedRawSha))
                {
                    reason = "future_schedule_catalog_entry_invalid";
                    return false;
                }
                var raw = rawValue.GetString() ?? string.Empty;
                if (!FixedTimeEquals(expectedRawSha, Sha256(raw)))
                {
                    reason = "future_schedule_entry_sha256_mismatch";
                    return false;
                }
                if (previousKey is not null && CompareCatalogKeys(previousKey, key) > 0)
                {
                    reason = "future_schedule_catalog_entries_not_canonical";
                    return false;
                }
                previousKey = key;
                if (!parsedEntries.TryAdd(key, new CatalogEntry
                    {
                        Key = key,
                        Raw = raw,
                        RawSha256 = expectedRawSha
                    }))
                {
                    reason = "future_schedule_catalog_duplicate_key";
                    return false;
                }
                fingerprint
                    .Append(name).Append('\u001f')
                    .Append(key).Append('\u001f')
                    .Append(expectedRawSha).Append('\u001e');
            }

            if (parsedEntries.Count != declaredCount || (!present && declaredCount != 0))
            {
                reason = "future_schedule_catalog_entry_count_mismatch";
                return false;
            }

            npc = new CatalogNpc
            {
                Name = name,
                AssetName = assetName,
                DefaultMap = defaultMap,
                DefaultTileX = defaultTileX,
                DefaultTileY = defaultTileY,
                HasMasterSchedule = present,
                Entries = parsedEntries
            };
            return true;
        }

        private static int CompareCatalogKeys(string left, string right)
        {
            var comparison = StringComparer.OrdinalIgnoreCase.Compare(left, right);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(left, right);
        }

        private static bool TryReadString(JsonElement source, string name, out string value)
        {
            value = string.Empty;
            if (!source.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
                return false;
            value = property.GetString() ?? string.Empty;
            return value.Length > 0;
        }

        private static bool TryReadInt(JsonElement source, string name, out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool TryReadBool(JsonElement source, string name, out bool value)
        {
            value = false;
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }
            value = property.GetBoolean();
            return true;
        }

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
                .Select(valueByte => valueByte.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length)
                return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }
}
