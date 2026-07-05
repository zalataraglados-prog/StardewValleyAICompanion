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
                    if (item.OptionId == "farm.maintain_crops")
                    {
                        applied.Add(item.OptionId);
                        SimulateMaintainCrops(before, changes, costs);
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

        private static void SimulateMaintainCrops(WorldModelEnvelope before, List<SimulatedFactChange> changes, List<SimulatedResourceCost> costs)
        {
            var watered = 0;
            if (before.Facts.Farm.TryGetValue("crops", out var crops) && crops.ValueKind == JsonValueKind.Array)
            {
                foreach (var crop in crops.EnumerateArray())
                {
                    if (crop.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var tile = TileLabel(crop);
                    var needsWatering = crop.TryGetProperty("needs_watering", out var needs) &&
                        needs.ValueKind == JsonValueKind.True;
                    if (needsWatering)
                    {
                        watered++;
                        changes.Add(new SimulatedFactChange
                        {
                            Path = "farm.crops[" + tile + "].needs_watering",
                            Before = "true",
                            After = "false"
                        });
                        changes.Add(new SimulatedFactChange
                        {
                            Path = "farm.crops[" + tile + "].watered",
                            Before = crop.TryGetProperty("watered", out var wateredValue) ? wateredValue.GetRawText() : "unknown",
                            After = "true"
                        });
                    }
                }
            }

            if (watered > 0)
            {
                costs.Add(new SimulatedResourceCost
                {
                    Resource = "player.energy",
                    Amount = watered * EnergyPerWateredCrop
                });
            }
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
