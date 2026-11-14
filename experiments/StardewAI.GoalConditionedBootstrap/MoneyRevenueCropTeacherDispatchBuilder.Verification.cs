using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropTeacherDispatchBuilder
{
    // Read the producer's original lineage without generating another dispatch or native evidence.
    public static MoneyRevenueCropTeacherDispatchManifest ReadVerified(string manifestPath)
    {
        var pins = new Dictionary<string, string>(DispatchPathComparer);
        string Pin(string path, string expected, string label)
        {
            var full = RequiredFile(path, label);
            Require(Hash(full) == expected, "Crop Teacher dispatch binding changed: " + label);
            if (pins.TryGetValue(full, out var previous))
                Require(previous == expected, "Crop Teacher dispatch has conflicting bindings: " + label);
            pins[full] = expected;
            return full;
        }
        var fullManifest = RequiredFile(manifestPath, "Crop Teacher dispatch manifest");
        Pin(fullManifest, Hash(fullManifest), "manifest");
        var manifest = Read<MoneyRevenueCropTeacherDispatchManifest>(fullManifest, "Crop Teacher dispatch manifest");
        Require(manifest.SchemaVersion == "money_revenue_crop_teacher_dispatch_manifest.v1" &&
            manifest.Status == "native_crop_production_run_plan_ready" && !manifest.FormalProductTrainingAuthorized &&
            BootstrapArtifactGuard.EqualJson(Read<JsonElement>(fullManifest, "dispatch shape"), manifest),
            "Crop Teacher dispatch manifest shape or authorization differs.");

        var capturePath = Pin(manifest.CaptureManifestPath, manifest.CaptureManifestSha256, "capture");
        var capture = Read<PlanningHandoffCaptureManifest>(capturePath, "dispatch capture");
        var layouts = capture.LayoutSnapshots ?? throw new InvalidDataException("Dispatch capture layouts are missing.");
        Pin(capture.SourceSnapshotPath, capture.SourceSnapshotSha256, "captured source");
        foreach (var layout in layouts) Pin(layout.Path, layout.Sha256, "captured layout");
        ValidateCapture(capture);
        var teacherPath = Pin(manifest.TeacherRequestPath, manifest.TeacherRequestSha256, "Teacher request");
        var decisionPath = Pin(manifest.TeacherDecisionPath, manifest.TeacherDecisionSha256, "Teacher decision");
        var receiptPath = Pin(manifest.DecisionReceiptPath, manifest.DecisionReceiptSha256, "decision receipt");
        var ledgerPath = Pin(manifest.StrategyLedgerPath, manifest.StrategyLedgerSha256, "committed ledger");
        var obligationRequestPath = Pin(manifest.ObligationRequestPath, manifest.ObligationRequestSha256, "obligation request");
        var obligationPath = Pin(manifest.ObligationPlanPath, manifest.ObligationPlanSha256, "obligation plan");
        var runRequestPath = Pin(manifest.NativeRunPlanRequestPath, manifest.NativeRunPlanRequestSha256, "run request");
        var runPath = Pin(manifest.NativeRunPlanPath, manifest.NativeRunPlanSha256, "run plan");
        var teacher = Read<MoneyRevenueCropProductionTeacherRequest>(teacherPath, "Teacher request");
        Require(SameDispatchPath(teacher.SnapshotPath, capture.SourceSnapshotPath) &&
            teacher.FarmLayoutSnapshotPaths.Select(Path.GetFullPath).SequenceEqual(layouts
                .OrderBy(row => row.MinY).ThenBy(row => row.MinX).Select(row => Path.GetFullPath(row.Path)), DispatchPathComparer) &&
            teacher.DecisionId == manifest.DecisionId && capture.SourceStateHash == manifest.SourceStateHash &&
            !teacher.FormalProductTrainingAuthorized, "Crop Teacher request does not bind its original capture/decision.");
        if (manifest.BaseStrategyLedgerPath is { } basePath)
        {
            Pin(basePath, manifest.BaseStrategyLedgerSha256 ?? string.Empty, "base ledger");
            Require(!string.IsNullOrWhiteSpace(teacher.BaseStrategyLedgerPath) &&
                SameDispatchPath(basePath, teacher.BaseStrategyLedgerPath), "Crop Teacher dispatch base ledger differs.");
        }
        else Require(manifest.BaseStrategyLedgerSha256 is null && string.IsNullOrWhiteSpace(teacher.BaseStrategyLedgerPath),
            "Crop Teacher dispatch has an undeclared base ledger.");

        var decision = MoneyRevenueCropProductionTeacherDecisionBuilder.Build(teacherPath);
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(decisionPath, "retained decision"), decision) &&
            decision.DecisionId == manifest.DecisionId &&
            !decision.FormalProductTrainingAuthorized, "Crop Teacher dispatch decision did not rebuild exactly.");
        var receipt = MoneyRevenueCropProductionDecisionReceiptBuilder.Build(decisionPath);
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(receiptPath, "retained receipt"), receipt) &&
            receipt.AllCommitmentsAccepted && receipt.FinalLedger is not null && receipt.SourceStateHash == manifest.SourceStateHash &&
            !receipt.FormalProductTrainingAuthorized &&
            BootstrapArtifactGuard.EqualJson(Read<StrategyCommitmentLedger>(ledgerPath, "retained ledger"), receipt.FinalLedger),
            "Crop Teacher dispatch receipt/committed ledger did not rebuild exactly.");
        var expectedObligation = new MoneyRevenueCropProductionObligationPlanRequest
        {
            SnapshotPath = Path.GetFullPath(capture.SourceSnapshotPath), StrategyLedgerPath = ledgerPath,
            DeadlineTotalDayExclusive = receipt.DeadlineTotalDayExclusive, TargetTotalMoneyEarned = receipt.TargetTotalMoneyEarned
        };
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(obligationRequestPath, "obligation request"), expectedObligation),
            "Crop Teacher dispatch obligation request differs from its original receipt.");
        var obligation = MoneyRevenueCropProductionObligationPlanBuilder.Build(obligationRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(obligationPath, "retained obligation"), obligation) &&
            obligation.ActiveCommitmentCount == receipt.LayoutIntents.Length && obligation.SourceStateHash == manifest.SourceStateHash &&
            !obligation.FormalProductTrainingAuthorized, "Crop Teacher dispatch obligation did not rebuild exactly.");
        var selected = decision.TeacherCandidateEvaluations.Where(row => row.Selected).ToArray();
        Require(selected.Length == 1, "Crop Teacher dispatch requires its original single selected crop.");
        var seedIntents = obligation.Commitments.Where(row => row.SeedQuantityShortfall > 0).Select(row =>
        {
            var choice = selected.SingleOrDefault(value => value.SeedQualifiedItemId == row.SeedQualifiedItemId);
            Require(choice is not null && !string.IsNullOrWhiteSpace(choice.ShopId) && choice.SeedUnitPrice > 0,
                "Crop Teacher dispatch seed shortfall lost its selected shop binding.");
            return new MoneyRevenueCropNativeSeedAcquisitionIntent(row.CommitmentId, row.SeedQualifiedItemId, choice!.ShopId, choice.SeedUnitPrice);
        }).ToArray();
        var expectedRun = new MoneyRevenueCropNativeProductionRunPlanRequest
        {
            CropProductionPlanPath = obligationPath, LayoutIntents = receipt.LayoutIntents,
            SeedAcquisitionIntents = seedIntents, MaxQueueItemCount = receipt.MaxQueueItemCount
        };
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(runRequestPath, "run request"), expectedRun),
            "Crop Teacher dispatch run request differs from its original receipt.");
        var run = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(runRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(Read<JsonElement>(runPath, "retained run plan"), run) &&
            run.NativeProductionRunReady && run.SourceStateHash == manifest.SourceStateHash &&
            run.CoveredCommitmentIds.SequenceEqual(receipt.LayoutIntents.Select(row => row.CommitmentId), StringComparer.Ordinal) &&
            run.CoveredCommitmentIds.SequenceEqual(manifest.CoveredCommitmentIds, StringComparer.Ordinal) &&
            run.FirstTotalDay == manifest.FirstTotalDay && run.LastTotalDay == manifest.LastTotalDay &&
            run.ActionCount == manifest.ActionCount && !run.FormalProductTrainingAuthorized,
            "Crop Teacher dispatch native plan/denominator did not rebuild exactly.");
        foreach (var (path, expected) in pins)
            Require(Hash(path) == expected, "Crop Teacher dispatch input changed during read: " + path);
        return manifest;
    }

    private static StringComparer DispatchPathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static bool SameDispatchPath(string left, string right) => DispatchPathComparer.Equals(Path.GetFullPath(left), Path.GetFullPath(right));
}
