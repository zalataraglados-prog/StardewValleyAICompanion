using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training
{
    public sealed class TrainingStateTransitionSimulator
    {
        private const int EnergyPerWateredCrop = 2;

        public SimulatedTransitionResult Simulate(WorldModelEnvelope before, ActionQueueEnvelope queue)
        {
            var blockReasons = new List<string>();
            if (queue.Status != "pending")
            {
                blockReasons.Add("queue_not_pending");
            }

            if (queue.ExecutionMode != "training_singleplayer")
            {
                blockReasons.Add("only_training_singleplayer_supported");
            }

            var applied = new List<string>();
            var changes = new List<SimulatedFactChange>();
            var costs = new List<SimulatedResourceCost>();

            if (blockReasons.Count == 0)
            {
                foreach (var item in queue.Items.Where(item => item.Status == "pending"))
                {
                    if (item.OptionId == "executor.water_crop")
                    {
                        applied.Add(item.OptionId);
                        if (!SimulateWaterCrop(before, item, changes, costs))
                        {
                            blockReasons.Add("transparent_water_crop_target_missing_or_not_needed");
                        }
                    }
                    else
                    {
                        blockReasons.Add("unsupported_option_for_transition:" + item.OptionId);
                    }
                }
            }

            var blocked = blockReasons.Count > 0;
            var afterHash = blocked
                ? string.Empty
                : ComputeAfterHash(before.StateHash, applied, changes, costs);

            return new SimulatedTransitionResult
            {
                BeforeStateHash = before.StateHash,
                AfterStateHash = afterHash,
                AppliedOptionIds = applied.Distinct(StringComparer.Ordinal).ToArray(),
                ChangedFacts = changes.ToArray(),
                ResourceCosts = costs.ToArray(),
                Blocked = blocked,
                BlockReasons = blockReasons.ToArray()
            };
        }

        private static bool SimulateWaterCrop(
            WorldModelEnvelope before,
            ActionQueueItem item,
            List<SimulatedFactChange> changes,
            List<SimulatedResourceCost> costs)
        {
            var targetX = ReadIntParameter(item, "target_tile_x");
            var targetY = ReadIntParameter(item, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue ||
                !before.Facts.CurrentLocation.TryGetValue("crops", out var crops) ||
                crops.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var crop in crops.EnumerateArray())
            {
                if (crop.ValueKind != JsonValueKind.Object ||
                    ReadInt(crop, "tile_x") != targetX ||
                    ReadInt(crop, "tile_y") != targetY)
                {
                    continue;
                }

                if (!crop.TryGetProperty("needs_watering", out var needs) || needs.ValueKind != JsonValueKind.True)
                    return false;

                var tile = TileLabel(crop);
                changes.Add(new SimulatedFactChange
                {
                    Path = "current_location.crops[" + tile + "].needs_watering",
                    Before = "true",
                    After = "false"
                });
                changes.Add(new SimulatedFactChange
                {
                    Path = "current_location.crops[" + tile + "].watered",
                    Before = crop.TryGetProperty("watered", out var wateredValue) ? wateredValue.GetRawText() : "unknown",
                    After = "true"
                });
                costs.Add(new SimulatedResourceCost
                {
                    Resource = "player.energy",
                    Amount = EnergyPerWateredCrop
                });
                return true;
            }

            return false;
        }

        private static int? ReadIntParameter(ActionQueueItem item, string name)
        {
            var value = item.NormalizedCommand.Parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value;
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static int? ReadInt(JsonElement value, string propertyName)
        {
            return value.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var parsed)
                ? parsed
                : null;
        }

        private static string TileLabel(JsonElement crop)
        {
            var x = crop.TryGetProperty("tile_x", out var tileX) && tileX.TryGetInt32(out var parsedX)
                ? parsedX.ToString()
                : "?";
            var y = crop.TryGetProperty("tile_y", out var tileY) && tileY.TryGetInt32(out var parsedY)
                ? parsedY.ToString()
                : "?";
            return x + "," + y;
        }

        private static string ComputeAfterHash(
            string beforeHash,
            IReadOnlyCollection<string> applied,
            IReadOnlyCollection<SimulatedFactChange> changes,
            IReadOnlyCollection<SimulatedResourceCost> costs)
        {
            var material = JsonSerializer.Serialize(new
            {
                before_hash = beforeHash,
                applied_option_ids = applied.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                changed_facts = changes.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray(),
                resource_costs = costs.OrderBy(item => item.Resource, StringComparer.Ordinal).ToArray()
            }, JsonOptions);

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
            return "sim." + ToLowerHex(bytes);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var item in bytes)
            {
                builder.Append(item.ToString("x2"));
            }

            return builder.ToString();
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
