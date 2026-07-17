using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void OnDayStartedForShippingReceipts(object? sender, StardewModdingAPI.Events.DayStartedEventArgs e)
    {
        try
        {
            TrySettleActiveRunPendingShippingReceipts();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Shipping receipt reconciliation error: {ex.Message}", LogLevel.Warn);
        }
    }

    private void ReconcileShippingReceipts()
    {
        try
        {
            var receiptsDir = ResolveReceiptDirectory();
            if (!Directory.Exists(receiptsDir)) return;

            var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
            var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
            foreach (var receiptPath in receiptFiles)
            {
                try
                {
                    var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                    var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                    if (receipt is null) continue;

                    var isTerminal = receipt.Status == "completed" || receipt.Status == "failed" || receipt.Status == "ambiguous" || receipt.Status == "timed_out";

                    if (receipt.Status == "pending")
                    {
                        if (string.IsNullOrWhiteSpace(activeRunId) ||
                            !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                            continue;

                        if (receipt.ExpiresAt != null && DateTimeOffset.TryParse(receipt.ExpiresAt, out var expires) && DateTimeOffset.UtcNow > expires)
                        {
                            receipt.Status = "timed_out";
                            receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                            receipt.SettlementReason = "receipt_expired";
                            if (!receipt.FeedbackAppended)
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                                if (AppendDelayedFeedback(receipt))
                                {
                                    receipt.FeedbackAppended = true;
                                }
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                            else
                            {
                                AtomicWriteReceipt(receiptPath, receipt);
                            }
                        }
                    }
                    else if (isTerminal && !receipt.FeedbackAppended)
                    {
                        if (AppendDelayedFeedback(receipt))
                        {
                            receipt.FeedbackAppended = true;
                        }
                        AtomicWriteReceipt(receiptPath, receipt);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static void AtomicWriteReceipt(string receiptPath, ShippingReceipt receipt)
    {
        var tempPath = receiptPath + ".tmp";
        var json = JsonSerializer.Serialize(receipt, JsonOptions);
        File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);
        using (var fs = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (fs.Length == 0) throw new InvalidOperationException("temp file empty after flush");
        }
        File.Move(tempPath, receiptPath, overwrite: true);
    }

    private static string ResolveReceiptDirectory()
    {
        var trainingDir = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_OUTPUT_DIR");
        if (!string.IsNullOrWhiteSpace(trainingDir))
        {
            return Path.Combine(trainingDir, "pending_receipts");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") == "1")
        {
            throw new InvalidOperationException("STARDEWAI_TRAINING_OUTPUT_DIR is required when STARDEWAI_TRAINING_MODE=1");
        }

        var dir = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "training_output");
        return Path.Combine(dir, "pending_receipts");
    }

    private bool AppendDelayedFeedback(ShippingReceipt receipt)
    {
        try
        {
            if (receipt.FeedbackAppended) return true;

            var dir = ResolveReceiptDirectory();
            var feedbackPath = Path.Combine(dir, "delayed_shipping_feedback.jsonl");

            if (File.Exists(feedbackPath))
            {
                var existingLines = File.ReadAllLines(feedbackPath, System.Text.Encoding.UTF8);
                foreach (var existingLine in existingLines)
                {
                    if (string.IsNullOrWhiteSpace(existingLine)) continue;
                    try
                    {
                        var existingRow = JsonSerializer.Deserialize<JsonElement>(existingLine, JsonOptions);
                        if (existingRow.TryGetProperty("receipt_id", out var existingId) &&
                            string.Equals(existingId.GetString(), receipt.ReceiptId, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }

            var row = new
            {
                receipt_id = receipt.ReceiptId,
                run_id = receipt.RunId,
                queue_id = receipt.QueueId,
                queue_item_id = receipt.QueueItemId,
                request_nonce = receipt.RequestNonce,
                unqualified_item_id = receipt.UnqualifiedItemId,
                qualified_item_id = receipt.QualifiedItemId,
                quantity = receipt.Quantity,
                source_date = receipt.SourceDate,
                pre_basic_shipped_count = receipt.PreBasicShippedCount,
                settled_basic_shipped_count = receipt.SettledBasicShippedCount,
                settlement_status = receipt.Status,
                settlement_reason = receipt.SettlementReason,
                settled_at = receipt.SettledAt,
                settled_game_date = receipt.SettledGameDate
            };
            var line = JsonSerializer.Serialize(row, JsonOptions) + "\n";
            File.AppendAllText(feedbackPath, line, System.Text.Encoding.UTF8);

            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write delayed shipping feedback: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }

    private void TrySettleActiveRunPendingShippingReceipts()
    {
        var receiptsDir = ResolveReceiptDirectory();
        if (!Directory.Exists(receiptsDir)) return;

        var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        var receiptFiles = Directory.GetFiles(receiptsDir, "ship_*.json");
        var errors = new List<Exception>();
        foreach (var receiptPath in receiptFiles)
        {
            try
            {
                var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
                var receipt = JsonSerializer.Deserialize<ShippingReceipt>(json, JsonOptions);
                if (receipt is null || receipt.Status != "pending") continue;

                if (string.IsNullOrWhiteSpace(activeRunId) ||
                    !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
                    continue;

                if (!int.TryParse(receipt.SourceDate, out var sourceDate)) continue;
                var currentGameDate = Game1.Date.TotalDays;
                if (currentGameDate <= sourceDate) continue;

                var currentCount = GetBasicShippedCount(Game1.player, receipt.UnqualifiedItemId);
                var expected = receipt.PreBasicShippedCount + receipt.Quantity;
                var newStatus = "ambiguous";
                var reason = string.Empty;

                if (currentCount == expected)
                {
                    newStatus = "completed";
                    reason = "basicShipped_incremented_by_expected_quantity";
                }
                else if (currentCount == receipt.PreBasicShippedCount)
                {
                    newStatus = "failed";
                    reason = "basicShipped_did_not_increment";
                }
                else
                {
                    newStatus = "ambiguous";
                    reason = "basicShipped_unexpected_delta:" + (currentCount - receipt.PreBasicShippedCount);
                }

                receipt.Status = newStatus;
                receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
                receipt.SettlementReason = reason;
                receipt.SettledBasicShippedCount = currentCount;
                receipt.SettledGameDate = currentGameDate.ToString();
                receipt.SettledSeason = Game1.currentSeason;
                receipt.SettledDayOfMonth = Game1.dayOfMonth.ToString();
                receipt.SettledYear = Game1.year.ToString();

                AtomicWriteReceipt(receiptPath, receipt);

                if (AppendDelayedFeedback(receipt))
                {
                    receipt.FeedbackAppended = true;
                    AtomicWriteReceipt(receiptPath, receipt);
                }
                Monitor.Log($"Shipping receipt {receipt.ReceiptId} settled: {newStatus} ({reason}). basicShipped: {receipt.PreBasicShippedCount}->{currentCount}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to process shipping receipt {receiptPath}: {ex.Message}", LogLevel.Warn);
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException("One or more shipping receipt settlements failed.", errors);
    }

}
