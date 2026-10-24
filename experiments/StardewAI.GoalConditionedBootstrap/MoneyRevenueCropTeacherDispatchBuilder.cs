using System.Globalization;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropTeacherDispatchBuilder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static MoneyRevenueCropTeacherDispatchManifest Build(
        string captureManifestPath,
        string outputDirectory,
        string decisionId,
        string decisionRecordedAt,
        int maximumTileCount = 8,
        int maxQueueItemCount = 8)
    {
        Require(!string.IsNullOrWhiteSpace(decisionId),
            "Money crop Teacher dispatch decision ID is required.");
        Require(DateTimeOffset.TryParse(
                    decisionRecordedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var recordedAt) &&
                recordedAt.Offset == TimeSpan.Zero,
            "Money crop Teacher dispatch timestamp must be explicit UTC.");
        Require(maximumTileCount is >= 1 and <= 64 &&
                maxQueueItemCount is >= 1 and <= 8,
            "Money crop Teacher dispatch bounds are invalid.");

        var capturePath = RequiredFile(
            captureManifestPath,
            "Planning handoff capture manifest");
        var capture = Read<PlanningHandoffCaptureManifest>(
            capturePath,
            "Planning handoff capture manifest");
        ValidateCapture(capture);

        var output = Path.GetFullPath(outputDirectory);
        Require(!Directory.Exists(output) && !File.Exists(output),
            "Money crop Teacher dispatch output already exists: " + output);
        Directory.CreateDirectory(output);

        var teacherRequestPath = Path.Combine(output, "teacher-request.json");
        var teacherDecisionPath = Path.Combine(output, "teacher-decision.json");
        var decisionReceiptPath = Path.Combine(output, "decision-receipt.json");
        var strategyLedgerPath = Path.Combine(output, "strategy-ledger.json");
        var obligationRequestPath = Path.Combine(output, "obligation-request.json");
        var obligationPlanPath = Path.Combine(output, "obligation-plan.json");
        var runPlanRequestPath = Path.Combine(output, "native-run-plan-request.json");
        var runPlanPath = Path.Combine(output, "native-run-plan.json");

        var teacherRequest = new MoneyRevenueCropProductionTeacherRequest
        {
            SnapshotPath = Path.GetFullPath(capture.SourceSnapshotPath),
            FarmLayoutSnapshotPaths = capture.LayoutSnapshots
                .OrderBy(value => value.MinY)
                .ThenBy(value => value.MinX)
                .Select(value => Path.GetFullPath(value.Path))
                .ToArray(),
            DecisionId = decisionId,
            DecisionRecordedAt = recordedAt.ToUniversalTime().ToString("O"),
            MaximumTileCount = maximumTileCount,
            MaxQueueItemCount = maxQueueItemCount,
            FormalProductTrainingAuthorized = false
        };
        Write(teacherRequestPath, teacherRequest);

        var decision = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
            teacherRequestPath);
        Write(teacherDecisionPath, decision);
        var receipt = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(
            teacherDecisionPath);
        Require(receipt.AllCommitmentsAccepted && receipt.FinalLedger is not null,
            "Money crop Teacher dispatch did not commit every crop obligation.");
        Write(decisionReceiptPath, receipt);
        Write(strategyLedgerPath, receipt.FinalLedger);

        var obligationRequest =
            new MoneyRevenueCropProductionObligationPlanRequest
            {
                SnapshotPath = teacherRequest.SnapshotPath,
                StrategyLedgerPath = strategyLedgerPath,
                DeadlineTotalDayExclusive = receipt.DeadlineTotalDayExclusive,
                TargetTotalMoneyEarned = receipt.TargetTotalMoneyEarned,
                FormalProductTrainingAuthorized = false
            };
        Write(obligationRequestPath, obligationRequest);
        var obligation = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            obligationRequestPath);
        Require(obligation.ActiveCommitmentCount ==
                    receipt.LayoutIntents.Length &&
                obligation.SourceStateHash == capture.SourceStateHash,
            "Money crop Teacher obligation plan drifted from its capture.");
        Write(obligationPlanPath, obligation);

        var selected = decision.TeacherCandidateEvaluations
            .Where(value => value.Selected)
            .ToArray();
        Require(selected.Length == 1,
            "Money crop Teacher dispatch requires one selected crop.");
        var seedAcquisitions = obligation.Commitments
            .Where(value => value.SeedQuantityShortfall > 0)
            .Select(commitment =>
            {
                var match = selected.SingleOrDefault(value =>
                    value.SeedQualifiedItemId ==
                        commitment.SeedQualifiedItemId);
                if (match is null ||
                    string.IsNullOrWhiteSpace(match.ShopId) ||
                    match.SeedUnitPrice <= 0)
                {
                    throw new InvalidDataException(
                        "Money crop Teacher seed shortfall lacks one selected " +
                        "shop binding: " + commitment.CommitmentId);
                }
                return new MoneyRevenueCropNativeSeedAcquisitionIntent(
                    commitment.CommitmentId,
                    commitment.SeedQualifiedItemId,
                    match.ShopId,
                    match.SeedUnitPrice);
            })
            .ToArray();
        var runPlanRequest =
            new MoneyRevenueCropNativeProductionRunPlanRequest
            {
                CropProductionPlanPath = obligationPlanPath,
                LayoutIntents = receipt.LayoutIntents,
                SeedAcquisitionIntents = seedAcquisitions,
                MaxQueueItemCount = receipt.MaxQueueItemCount,
                FormalProductTrainingAuthorized = false
            };
        Write(runPlanRequestPath, runPlanRequest);
        var runPlan = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
            runPlanRequestPath);
        Require(runPlan.NativeProductionRunReady &&
                runPlan.SourceStateHash == capture.SourceStateHash &&
                runPlan.CoveredCommitmentIds.SequenceEqual(
                    receipt.LayoutIntents.Select(value => value.CommitmentId),
                    StringComparer.Ordinal),
            "Money crop Teacher native run plan drifted from its decision.");
        Write(runPlanPath, runPlan);

        var manifest = new MoneyRevenueCropTeacherDispatchManifest
        {
            CaptureManifestPath = capturePath,
            CaptureManifestSha256 = Hash(capturePath),
            SourceStateHash = capture.SourceStateHash,
            DecisionId = decisionId,
            TeacherRequestPath = teacherRequestPath,
            TeacherRequestSha256 = Hash(teacherRequestPath),
            TeacherDecisionPath = teacherDecisionPath,
            TeacherDecisionSha256 = Hash(teacherDecisionPath),
            DecisionReceiptPath = decisionReceiptPath,
            DecisionReceiptSha256 = Hash(decisionReceiptPath),
            StrategyLedgerPath = strategyLedgerPath,
            StrategyLedgerSha256 = Hash(strategyLedgerPath),
            ObligationRequestPath = obligationRequestPath,
            ObligationRequestSha256 = Hash(obligationRequestPath),
            ObligationPlanPath = obligationPlanPath,
            ObligationPlanSha256 = Hash(obligationPlanPath),
            NativeRunPlanRequestPath = runPlanRequestPath,
            NativeRunPlanRequestSha256 = Hash(runPlanRequestPath),
            NativeRunPlanPath = runPlanPath,
            NativeRunPlanSha256 = Hash(runPlanPath),
            CoveredCommitmentIds = runPlan.CoveredCommitmentIds,
            FirstTotalDay = runPlan.FirstTotalDay,
            LastTotalDay = runPlan.LastTotalDay,
            ActionCount = runPlan.ActionCount,
            FormalProductTrainingAuthorized = false
        };
        Write(Path.Combine(output, "teacher-dispatch-manifest.json"), manifest);
        return manifest;
    }

    private static void ValidateCapture(PlanningHandoffCaptureManifest capture)
    {
        var layouts = capture.LayoutSnapshots ??
            throw new InvalidDataException(
                "Planning handoff capture layouts are missing.");
        Require(capture.SchemaVersion ==
                    "planning_handoff_capture_manifest.v1" &&
                capture.Status == "farm_layout_capture_complete" &&
                capture.SourceStateHash.Length == 64 &&
                capture.MapWidth is >= 1 and <= 512 &&
                capture.MapHeight is >= 1 and <= 512 &&
                capture.LayoutChunkSize is >= 1 and <= 32 &&
                capture.LayoutSnapshotCount ==
                    layouts.Length,
            "Planning handoff capture manifest is incomplete.");
        var sourcePath = RequiredFile(
            capture.SourceSnapshotPath,
            "Planning handoff source snapshot");
        var source = Read<SnapshotEnvelope>(
            sourcePath,
            "Planning handoff source snapshot");
        Require(Hash(sourcePath) == capture.SourceSnapshotSha256 &&
                source.StateHash == capture.SourceStateHash,
            "Planning handoff source snapshot hash drifted.");
        Require(capture.PlanningHandoff.ValueKind == JsonValueKind.Object &&
                ReadString(capture.PlanningHandoff, "handoff_kind") ==
                    "crop_production_teacher" &&
                ReadBool(
                    capture.PlanningHandoff,
                    "automatic_dispatch_required"),
            "Planning handoff is not an automatic crop Teacher dispatch.");

        var expected = new List<(int MinX, int MinY, int MaxX, int MaxY)>();
        for (var minY = 0; minY < capture.MapHeight;
             minY += capture.LayoutChunkSize)
        {
            var maxY = Math.Min(
                capture.MapHeight - 1,
                minY + capture.LayoutChunkSize - 1);
            for (var minX = 0; minX < capture.MapWidth;
                 minX += capture.LayoutChunkSize)
            {
                expected.Add((
                    minX,
                    minY,
                    Math.Min(
                        capture.MapWidth - 1,
                        minX + capture.LayoutChunkSize - 1),
                    maxY));
            }
        }
        var actual = layouts
            .Select(value => (value.MinX, value.MinY, value.MaxX, value.MaxY))
            .OrderBy(value => value.MinY)
            .ThenBy(value => value.MinX)
            .ToArray();
        Require(expected.SequenceEqual(actual),
            "Planning handoff Farm layout partition is not exact.");
        foreach (var layout in layouts)
        {
            var path = RequiredFile(
                layout.Path,
                "Planning handoff Farm layout snapshot");
            var expectedTiles = checked(
                (layout.MaxX - layout.MinX + 1) *
                (layout.MaxY - layout.MinY + 1));
            var snapshot = Read<SnapshotEnvelope>(
                path,
                "Planning handoff Farm layout snapshot");
            Require(Hash(path) == layout.Sha256 &&
                    layout.StateHash.Length == 64 &&
                    snapshot.StateHash == layout.StateHash &&
                    layout.TileCount == expectedTiles,
                "Planning handoff Farm layout binding drifted: " + path);
        }
    }

    private static T Read<T>(string path, string label) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ??
        throw new InvalidDataException(label + " is empty.");

    private static void Write<T>(string path, T value)
    {
        var fullPath = Path.GetFullPath(path);
        Require(!File.Exists(fullPath),
            "Money crop Teacher dispatch artifact already exists: " + fullPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var pending = fullPath + ".pending-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(
            pending,
            JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine,
            new UTF8Encoding(false));
        File.Move(pending, fullPath, overwrite: false);
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));

    private static string ReadString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) &&
        node.ValueKind == JsonValueKind.String
            ? node.GetString() ?? string.Empty
            : string.Empty;

    private static bool ReadBool(JsonElement value, string property) =>
        value.TryGetProperty(property, out var node) &&
        node.ValueKind is JsonValueKind.True;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
