using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record CropTeacherDayBoundaryStart(
        int FromTotalDay,
        string FromDayKey,
        string BeforeStateHash,
        SaveDirectoryFingerprint BeforeSave);

    private sealed record PendingCropTeacherDayBoundary(
        int FromTotalDay,
        string FromDayKey,
        string QueueId,
        string QueueItemId,
        string BeforeStateHash,
        SaveDirectoryFingerprint BeforeSave);

    private sealed record CropTeacherDayBoundaryCheck(
        bool Verified,
        string Detail,
        CropTeacherDayBoundaryReceipt? Receipt);

    private static async Task<CropTeacherDayBoundaryStart?>
        CaptureCropTeacherDayBoundaryStartAsync(
            LiveTrainingOptions options,
            CropTeacherRunStep? step,
            JsonObject beforeSnapshot,
            string beforeStateHash)
    {
        if (step?.Stage != CropTeacherRunStageKinds.NativeSleep)
        {
            return null;
        }
        if (!options.RequireNativeSaveBoundary)
        {
            throw new InvalidOperationException(
                "crop_teacher_run_requires_native_save_boundary");
        }
        return await CaptureCropTeacherDayBoundaryStartCoreAsync(
            options,
            step.TotalDay,
            beforeSnapshot,
            beforeStateHash);
    }

    private static async Task<CropTeacherDayBoundaryStart?>
        CaptureCropTeacherTerminalBoundaryStartAsync(
            LiveTrainingOptions options,
            CropTeacherRunStep step,
            MoneyRevenueCropNativeProductionRunPlan plan,
            JsonObject beforeSnapshot,
            string beforeStateHash)
    {
        if (step.Stage != CropTeacherRunStageKinds.Complete ||
            step.TotalDay != plan.LastTotalDay)
        {
            return null;
        }
        if (!options.RequireNativeSaveBoundary)
        {
            return null;
        }
        return await CaptureCropTeacherDayBoundaryStartCoreAsync(
            options,
            step.TotalDay,
            beforeSnapshot,
            beforeStateHash);
    }

    private static async Task<CropTeacherDayBoundaryStart>
        CaptureCropTeacherDayBoundaryStartCoreAsync(
            LiveTrainingOptions options,
            int expectedTotalDay,
            JsonObject beforeSnapshot,
            string beforeStateHash)
    {
        var totalDay = ReadCropTeacherTotalDay(beforeSnapshot);
        var dayKey = QueueReplanFilter.SnapshotDayKey(beforeSnapshot);
        if (totalDay != expectedTotalDay ||
            string.IsNullOrWhiteSpace(dayKey) ||
            string.IsNullOrWhiteSpace(beforeStateHash))
        {
            throw new InvalidDataException(
                "crop_teacher_day_boundary_start_invalid");
        }
        var save = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
            options.SaveIsolationPath,
            options.SaveSlot);
        return new CropTeacherDayBoundaryStart(
            totalDay,
            dayKey,
            beforeStateHash,
            save);
    }

    private static PendingCropTeacherDayBoundary
        CreatePendingCropTeacherDayBoundary(
            CropTeacherDayBoundaryStart start,
            JsonObject verifiedExecution)
    {
        var queueId = ReadString(verifiedExecution, "queue_id");
        var queueItemId = ReadString(verifiedExecution, "queue_item_id");
        if (string.IsNullOrWhiteSpace(queueId) ||
            string.IsNullOrWhiteSpace(queueItemId) ||
            !string.Equals(
                ReadString(verifiedExecution, "option_id"),
                "executor.sleep",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "crop_teacher_day_boundary_sleep_receipt_invalid");
        }
        return new PendingCropTeacherDayBoundary(
            start.FromTotalDay,
            start.FromDayKey,
            queueId,
            queueItemId,
            start.BeforeStateHash,
            start.BeforeSave);
    }

    private static async Task<CropTeacherDayBoundaryCheck>
        CheckCropTeacherDayBoundaryAsync(
            LiveTrainingOptions options,
            PendingCropTeacherDayBoundary pending,
            JsonObject currentSnapshot)
    {
        var currentTotalDay = ReadCropTeacherTotalDay(currentSnapshot);
        var currentDayKey = QueueReplanFilter.SnapshotDayKey(currentSnapshot);
        if (currentTotalDay < pending.FromTotalDay ||
            currentTotalDay > pending.FromTotalDay + 1 ||
            string.IsNullOrWhiteSpace(currentDayKey))
        {
            throw new InvalidDataException(
                "crop_teacher_day_boundary_date_drifted");
        }
        if (currentTotalDay == pending.FromTotalDay)
        {
            return new CropTeacherDayBoundaryCheck(
                false,
                "native_day_not_advanced",
                null);
        }

        var currentSave = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
            options.SaveIsolationPath,
            options.SaveSlot);
        var observation = NativeSaveBoundaryVerifier.Evaluate(
            pending.FromDayKey,
            currentDayKey,
            pending.BeforeSave,
            currentSave);
        if (!observation.Verified)
        {
            return new CropTeacherDayBoundaryCheck(
                false,
                "save_changed=" + observation.SaveChanged +
                " day_advanced=" + observation.DayAdvanced,
                null);
        }

        var afterStateHash = ReadString(currentSnapshot, "state_hash");
        if (string.IsNullOrWhiteSpace(afterStateHash))
        {
            throw new InvalidDataException(
                "crop_teacher_day_boundary_after_state_hash_missing");
        }
        return new CropTeacherDayBoundaryCheck(
            true,
            "date_and_save_advanced",
            new CropTeacherDayBoundaryReceipt(
                CropTeacherTransitionLedger.DayBoundaryKey(
                    pending.FromTotalDay),
                pending.FromTotalDay,
                currentTotalDay,
                pending.FromDayKey,
                currentDayKey,
                pending.QueueId,
                pending.QueueItemId,
                pending.BeforeStateHash,
                afterStateHash,
                pending.BeforeSave.Sha256,
                currentSave.Sha256,
                DateTimeOffset.UtcNow.ToString("O")));
    }

    private static int ReadCropTeacherTotalDay(JsonObject snapshot)
    {
        var field = snapshot["state"]?["time"]?["total_days"] as JsonObject;
        if (field is null ||
            field["status"]?.GetValue<string>() is not ("available" or "derived") ||
            field["value"] is not JsonValue value ||
            !value.TryGetValue<int>(out var totalDay) ||
            totalDay < 0)
        {
            throw new InvalidDataException(
                "crop_teacher_total_day_unavailable");
        }
        return totalDay;
    }
}
