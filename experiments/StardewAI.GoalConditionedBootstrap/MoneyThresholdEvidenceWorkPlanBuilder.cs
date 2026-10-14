using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdEvidenceWorkPlanBuilder
{
    public static MoneyThresholdEvidenceWorkPlan Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money-threshold evidence work-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdEvidenceWorkPlanRequest>(
            fullRequestPath,
            "Money-threshold evidence work-plan request");
        ValidateRequest(request);

        MoneyThresholdTeacherCorpus? corpus = null;
        var corpusPath = string.Empty;
        var corpusSha256 = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.MoneyThresholdCorpusPath))
        {
            corpusPath = RequiredFile(
                request.MoneyThresholdCorpusPath,
                "Money-threshold Teacher corpus");
            corpus = MoneyThresholdTeacherCorpusBuilder.Verify(corpusPath);
            corpusSha256 = CurrentTeacherFrontierSupport.HashFile(corpusPath);
        }

        return Build(
            fullRequestPath,
            request,
            corpusPath,
            corpusSha256,
            corpus);
    }

    internal static MoneyThresholdEvidenceWorkPlan BuildFromVerifiedCorpus(
        string requestPath,
        string verifiedCorpusPath,
        MoneyThresholdTeacherCorpus verifiedCorpus)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money-threshold evidence work-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyThresholdEvidenceWorkPlanRequest>(
            fullRequestPath,
            "Money-threshold evidence work-plan request");
        ValidateRequest(request);
        var corpusPath = RequiredFile(
            verifiedCorpusPath,
            "Verified Money-threshold Teacher corpus");
        if (!string.Equals(
                Path.GetFullPath(request.MoneyThresholdCorpusPath),
                corpusPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Verified Money corpus does not match the work-plan request.");
        }
        MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(verifiedCorpus);
        return Build(
            fullRequestPath,
            request,
            corpusPath,
            CurrentTeacherFrontierSupport.HashFile(corpusPath),
            verifiedCorpus);
    }

    private static MoneyThresholdEvidenceWorkPlan Build(
        string fullRequestPath,
        MoneyThresholdEvidenceWorkPlanRequest request,
        string corpusPath,
        string corpusSha256,
        MoneyThresholdTeacherCorpus? corpus)
    {
        var cells = MoneyThresholdEvidenceCellProjector.Build(corpus);
        var fullyComplete = cells.Count(value =>
            value.NativeOutcomeVerified &&
            value.TeacherComparisonVerified);
        return new MoneyThresholdEvidenceWorkPlan
        {
            Status = fullyComplete == cells.Length
                ? "money_threshold_evidence_complete"
                : "money_threshold_evidence_work_remaining",
            PlanId = request.PlanId,
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            MoneyThresholdCorpusPath = corpusPath,
            MoneyThresholdCorpusSha256 = corpusSha256,
            CriterionCount =
                GrandpaEvaluationGoalEvaluator.MoneyThresholds.Count,
            RequiredPartitionCount =
                MoneyThresholdEvidenceCellProjector.RequiredPartitionCount,
            RequiredCellCount = cells.Length,
            NativeOutcomeCompleteCellCount = cells.Count(value =>
                value.NativeOutcomeVerified),
            TeacherComparisonCompleteCellCount = cells.Count(value =>
                value.TeacherComparisonVerified),
            FullyCompleteCellCount = fullyComplete,
            CoverageComplete = fullyComplete == cells.Length,
            Cells = cells,
            FormalProductTrainingAuthorized = false
        };
    }

    private static void ValidateRequest(
        MoneyThresholdEvidenceWorkPlanRequest request)
    {
        if (request.SchemaVersion !=
                "money_threshold_evidence_work_plan_request.v1" ||
            string.IsNullOrWhiteSpace(request.PlanId) ||
            request.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Money-threshold evidence work-plan request is invalid.");
        }
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }
}
