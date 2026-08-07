using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void OnDayStartedForPetBowlReceipts(object? sender, DayStartedEventArgs e)
    {
        TrySettlePetBowlReceiptsSafely();
    }

    private void OnSaveLoadedForPetBowlReceipts(object? sender, SaveLoadedEventArgs e)
    {
        TrySettlePetBowlReceiptsSafely();
    }

    private void TrySettlePetBowlReceiptsSafely()
    {
        try
        {
            TrySettleActiveRunPendingPetBowlReceipts();
        }
        catch (Exception ex)
        {
            Monitor.Log($"Pet bowl receipt reconciliation error: {ex.Message}", LogLevel.Warn);
        }
    }

    private string WritePetBowlPendingReceipt(ActiveNativeTool tool, PetBowl bowl, Pet pet)
    {
        try
        {
            var request = tool.Pending.Request;
            var receiptsDir = ResolveReceiptDirectory();
            Directory.CreateDirectory(receiptsDir);
            var safeRunId = SanitizeFileName(request.RunId);
            var safeQueueItemId = SanitizeFileName(request.QueueItemId);
            var safeNonce = SanitizeFileName(request.RequestNonce);
            if (string.IsNullOrWhiteSpace(safeNonce) || safeNonce == "unknown")
            {
                return string.Empty;
            }

            var receiptId = "pet_bowl_" + safeRunId + "_" + safeQueueItemId + "_" + safeNonce;
            var receiptPath = Path.Combine(receiptsDir, receiptId + ".json");
            var receipt = new PetBowlReceipt
            {
                ReceiptId = receiptId,
                RunId = request.RunId,
                QueueId = request.QueueId,
                QueueItemId = request.QueueItemId,
                RequestNonce = request.RequestNonce,
                PetId = pet.petId.Value.ToString("D"),
                LocationId = bowl.GetParentLocation()?.NameOrUniqueName ?? Game1.currentLocation.NameOrUniqueName,
                BuildingTileX = bowl.tileX.Value,
                BuildingTileY = bowl.tileY.Value,
                SourceTotalDays = Game1.Date.TotalDays,
                FriendshipBefore = request.ExpectedFriendshipBefore!.Value,
                ExpectedFriendshipAfter = request.ExpectedNextDayFriendshipAfter!.Value,
                PetLoveMailBefore = request.ExpectedPetLoveMailBefore!.Value,
                ExpectedPetLoveMailAfter = request.ExpectedNextDayPetLoveMail!.Value,
                MarnieAdoptionMailBeforeOrPending = request.ExpectedMarniePetAdoptionMailBeforeOrPending!.Value,
                ExpectedMarnieAdoptionMailAfterOrPending = request.ExpectedNextDayMarniePetAdoptionMail!.Value,
                CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7).ToString("O")
            };
            AtomicWritePetBowlReceipt(receiptPath, receipt);
            return receiptPath;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write pet bowl pending receipt: {ex.Message}", LogLevel.Error);
            return string.Empty;
        }
    }

    private static void AtomicWritePetBowlReceipt(string receiptPath, PetBowlReceipt receipt)
    {
        var tempPath = receiptPath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(receipt, JsonOptions), System.Text.Encoding.UTF8);
        using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length == 0)
            {
                throw new InvalidOperationException("pet bowl receipt temp file empty after flush");
            }
        }
        File.Move(tempPath, receiptPath, overwrite: true);
    }

    private void TrySettleActiveRunPendingPetBowlReceipts()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }
        var receiptsDir = ResolveReceiptDirectory();
        if (!Directory.Exists(receiptsDir))
        {
            return;
        }

        var activeRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        foreach (var receiptPath in Directory.GetFiles(receiptsDir, "pet_bowl_*.json"))
        {
            var receipt = JsonSerializer.Deserialize<PetBowlReceipt>(File.ReadAllText(receiptPath, System.Text.Encoding.UTF8), JsonOptions);
            if (receipt is null || receipt.Status != "pending" ||
                string.IsNullOrWhiteSpace(activeRunId) || !string.Equals(receipt.RunId, activeRunId, StringComparison.Ordinal))
            {
                continue;
            }
            if (Game1.Date.TotalDays <= receipt.SourceTotalDays)
            {
                continue;
            }

            var pet = Guid.TryParse(receipt.PetId, out var petId) ? Utility.findPet(petId) : null;
            var bowl = FindReceiptPetBowl(receipt);
            var friendship = pet?.friendshipTowardFarmer.Value;
            var watered = bowl?.watered.Value;
            var loveMail = Game1.player.mailReceived.Contains("petLoveMessage");
            var adoptionMail = Game1.player.hasOrWillReceiveMail("MarniePetAdoption");
            var exactlyNextDay = Game1.Date.TotalDays == receipt.SourceTotalDays + 1;
            var exactSettlement = exactlyNextDay && friendship == receipt.ExpectedFriendshipAfter && watered == false &&
                loveMail == receipt.ExpectedPetLoveMailAfter && adoptionMail == receipt.ExpectedMarnieAdoptionMailAfterOrPending;

            receipt.Status = exactSettlement ? "completed" : exactlyNextDay ? "failed" : "ambiguous";
            receipt.SettlementReason = exactSettlement
                ? "native_Pet.dayUpdate_exact_friendship_bowl_and_mail_settlement"
                : !exactlyNextDay
                    ? "settlement_observed_after_more_than_one_day"
                    : "native_Pet.dayUpdate_projection_mismatch";
            receipt.SettledAt = DateTimeOffset.UtcNow.ToString("O");
            receipt.SettledTotalDays = Game1.Date.TotalDays;
            receipt.SettledFriendship = friendship;
            receipt.SettledBowlWatered = watered;
            receipt.SettledPetLoveMail = loveMail;
            receipt.SettledMarnieAdoptionMailOrPending = adoptionMail;
            AtomicWritePetBowlReceipt(receiptPath, receipt);
            if (AppendDelayedPetBowlFeedback(receipt))
            {
                receipt.FeedbackAppended = true;
                AtomicWritePetBowlReceipt(receiptPath, receipt);
            }
        }
    }

    private PetBowl? FindReceiptPetBowl(PetBowlReceipt receipt)
    {
        var location = Game1.getLocationFromName(receipt.LocationId);
        return location?.buildings.OfType<PetBowl>().FirstOrDefault(bowl =>
            bowl.GetType() == typeof(PetBowl) && bowl.tileX.Value == receipt.BuildingTileX &&
            bowl.tileY.Value == receipt.BuildingTileY && bowl.petId.Value.ToString("D") == receipt.PetId);
    }

    private bool AppendDelayedPetBowlFeedback(PetBowlReceipt receipt)
    {
        try
        {
            var feedbackPath = Path.Combine(ResolveReceiptDirectory(), "delayed_pet_bowl_feedback.jsonl");
            if (File.Exists(feedbackPath) && File.ReadLines(feedbackPath).Any(line =>
                line.Contains("\"receipt_id\":\"" + receipt.ReceiptId + "\"", StringComparison.Ordinal)))
            {
                return true;
            }
            var row = new
            {
                receipt_id = receipt.ReceiptId,
                run_id = receipt.RunId,
                queue_id = receipt.QueueId,
                queue_item_id = receipt.QueueItemId,
                request_nonce = receipt.RequestNonce,
                pet_id = receipt.PetId,
                source_total_days = receipt.SourceTotalDays,
                friendship_before = receipt.FriendshipBefore,
                expected_friendship_after = receipt.ExpectedFriendshipAfter,
                settled_friendship = receipt.SettledFriendship,
                expected_pet_love_mail_after = receipt.ExpectedPetLoveMailAfter,
                settled_pet_love_mail = receipt.SettledPetLoveMail,
                expected_marnie_adoption_mail_after_or_pending = receipt.ExpectedMarnieAdoptionMailAfterOrPending,
                settled_marnie_adoption_mail_or_pending = receipt.SettledMarnieAdoptionMailOrPending,
                settled_bowl_watered = receipt.SettledBowlWatered,
                settlement_status = receipt.Status,
                settlement_reason = receipt.SettlementReason,
                settled_at = receipt.SettledAt
            };
            File.AppendAllText(feedbackPath, JsonSerializer.Serialize(row, JsonOptions) + "\n", System.Text.Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            Monitor.Log($"Failed to write delayed pet bowl feedback: {ex.Message}", LogLevel.Warn);
            return false;
        }
    }
}
