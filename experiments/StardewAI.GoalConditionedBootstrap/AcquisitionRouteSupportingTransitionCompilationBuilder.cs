using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionCompilationBuilder
{
    public static AcquisitionRouteDispatchCompilation Build(
        AcquisitionRouteSupportingTransitionRequestInputs inputs,
        string supportRequestPath,
        string supportCommitReceiptPath,
        string committedLedgerPath,
        string commitResultPath)
    {
        var requestPath = Path.GetFullPath(supportRequestPath);
        var receiptPath = Path.GetFullPath(supportCommitReceiptPath);
        var committedPath = Path.GetFullPath(committedLedgerPath);
        var snapshotPath = Path.GetFullPath(inputs.SnapshotPath);
        var rankingPath = Path.GetFullPath(inputs.RankingPath);
        var expectedRequest =
            AcquisitionRouteSupportingTransitionRequestBuilder.Build(inputs);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionRequest>(
            requestPath,
            "Acquisition supporting-transition request");
        Require(EqualJson(request, expectedRequest),
            "Supporting-transition request drifted from deterministic source compilation.");
        var expectedReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.Build(
                inputs,
                requestPath,
                committedPath,
                commitResultPath);
        var receipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionCommitReceipt>(
            receiptPath,
            "Acquisition supporting-transition commit receipt");
        Require(EqualJson(receipt, expectedReceipt),
            "Supporting-transition commit receipt drifted from deterministic source verification.");

        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition support compilation snapshot");
        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingPath,
            "Acquisition support compilation ranking");
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            committedPath,
            snapshotDocument.RootElement).Ledger;
        var processing = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateProcessingReport>(
            Path.GetFullPath(inputs.TargetDateProcessingPath),
            "Acquisition target-date processing report");
        var route = ExactlyOne(
            processing.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Target-date processing route occurrence");
        var requirement = RequirementRoute(route.UpstreamRoute);
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            Path.GetFullPath(inputs.AcquisitionLoweringPath),
            "Acquisition route lowering");
        var lowered = LoweredRoute(lowering, requirement);
        var optionIds = lowered.EndpointOptionIds
            .Concat(lowered.SupportingOptionIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var candidates = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                processing.GoalId,
                requirement,
                optionIds,
                CurrentTeacherFrontierSupport.ReadCurrentCandidates(ranking),
                out var candidateReasons);
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(requirement, lowered, candidates);
        return BuildCore(
            request,
            receipt,
            requirement,
            lowered,
            snapshot,
            ledger,
            matches,
            CurrentTeacherFrontierSupport.HashFile(rankingPath),
            CurrentTeacherFrontierSupport.HashFile(requestPath),
            CurrentTeacherFrontierSupport.HashFile(receiptPath),
            candidateReasons);
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateReservation route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static AcquisitionRequirementRouteLowering LoweredRoute(
        AcquisitionRouteOptionLoweringReport lowering,
        AcquisitionRouteTargetDateUnlock requirement) =>
        lowering.RequirementSets.Single(value =>
                value.RequirementSetId == requirement.RequirementSetId)
            .Groups.Single(value =>
                value.RequirementId == requirement.RequirementId)
            .Alternatives[requirement.AlternativeIndex]
            .Routes[requirement.RouteIndex];

    private static T ExactlyOne<T>(
        IEnumerable<T> values,
        Func<T, bool> predicate,
        string label)
    {
        var matches = values.Where(predicate).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(label + " is not unique.");
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
