using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl?> BuildCropTeacherOwnedTerminalContinuationAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request, CropTeacherOwnerLifetime lifetime)
    {
        var owner = lifetime.Current ?? throw new InvalidOperationException("Terminal continuation requires the original managed owner.");
        var selection = lifetime.TerminalSelection ?? throw new InvalidOperationException("Retain the original terminal selection before closeout.");
        if (owner.AcceptedTerminalDepositCount < selection.MinimumUnits)
            return await BuildCropTeacherOwnedTerminalQueueAsync(options, iteration, owner, selection).ConfigureAwait(false);
        if (owner.AcceptedTerminalDepositCount != selection.MinimumUnits || owner.Pending is not null || owner.HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the original accepted terminal cursor before native settlement.");
        var target = selection.CreateTarget(owner.AcceptedTerminalDepositCount);
        selection.ValidateInputsUnchanged();
        var sourcePath = owner.CurrentSnapshotPath;
        var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
        var day = ReadSnapshotEnvelopeInt(source, "time", "total_days");
        if (day == target.TotalDay + 1)
        {
            if (lifetime.TerminalSettlement is null)
            {
                var evidence = await owner.BuildTerminalSettlementEvidenceAsync(lifetime.TerminalProduction!, target,
                    Path.Combine(owner.EvidenceDirectory, "settlement-terminal")).ConfigureAwait(false);
                lifetime.AttachTerminalSettlement(evidence);
                AppendProgress(options, "crop_teacher_managed_terminal_settlement", iteration, ReadString(source, "state_hash"),
                    string.Empty, "manifest=" + evidence.ManifestPath + " settled_revenue=" + evidence.SettledRevenue +
                    " semantic_saved_content_verified=false");
            }
            return null;
        }
        var step = SelectCropTeacherTerminalCloseoutStep(source, target);
        var runtime = Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4"));
        var operation = step.Stage == CropTeacherRunStageKinds.NativeSleep
            ? await PrepareCropTeacherOwnedOvernightAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false)
            : await PrepareCropTeacherOwnedRouteAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false);
        var start = operation.OriginalStart!;
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        return new(StandardResult(queue, operation.Record["original_model_plan_path"]!.GetValue<string>(), string.Empty,
            sourcePath, source, ReadString(source, "state_hash"), options, "crop_teacher_terminal_settlement", "full"), operation);
    }

    private static CropTeacherRunStep SelectCropTeacherTerminalCloseoutStep(JsonObject source, CropTeacherTerminalShippingTarget target)
    {
        if (target.AcceptedDepositCount != target.RequiredQuantity || target.RequiredQuantity <= 0 ||
            ReadSnapshotEnvelopeInt(source, "time", "total_days") != target.TotalDay || HasPriorityStoryEventInterrupt(source) ||
            CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(source))
            throw new InvalidDataException("Terminal closeout requires completed original deposits, the same day and no unbound higher-priority control.");
        var homeField = source["state"]?["current_location"]?["home_context"];
        var home = homeField?["value"]?["home_location_id"]?.GetValue<string>();
        var current = source["state"]?["player"]?["location_id"]?["value"]?.GetValue<string>();
        if (homeField?["status"]?.GetValue<string>() is not ("available" or "derived") ||
            string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(current))
            throw new InvalidDataException("Terminal closeout has no readable original home/location.");
        return new(current == home ? CropTeacherRunStageKinds.NativeSleep : CropTeacherRunStageKinds.RouteHome,
            target.TotalDay, string.Empty, home, home, string.Empty, string.Empty, 0, 0, null, "crop_teacher_terminal_settlement");
    }
}
