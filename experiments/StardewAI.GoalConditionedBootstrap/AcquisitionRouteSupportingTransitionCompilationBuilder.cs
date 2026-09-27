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
        var calendar = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteCalendarResolutionReport>(
            Path.GetFullPath(inputs.CalendarResolutionPath),
            "Acquisition route calendar resolution");
        var route = ExactlyOne(
            processing.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Target-date processing route occurrence");
        var requirement = RequirementRoute(route.UpstreamRoute);
        var staticRoute = ExactlyOne(
            calendar.Routes,
            value => value.RouteOccurrenceId == inputs.RouteOccurrenceId,
            "Static calendar route occurrence");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            Path.GetFullPath(inputs.AcquisitionLoweringPath),
            "Acquisition route lowering");
        var lowered = LoweredRoute(lowering, requirement);
        var optionIds = lowered.EndpointOptionIds
            .Concat(lowered.SupportingOptionIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rankedOptionIds = optionIds.Where(optionId =>
                optionId != "inventory.transfer_item" &&
                optionId !=
                    "farm.establish_supported_machine_capacity")
            .ToArray();
        var candidates = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                processing.GoalId,
                requirement,
                rankedOptionIds,
                CurrentTeacherFrontierSupport.ReadCurrentCandidates(ranking),
                out var candidateReasons);
        var stagingReasons = Array.Empty<string>();
        AcquisitionMachineCapacitySupportBinding? capacityBinding = null;
        var capacityReasons = Array.Empty<string>();
        if (request.SupportTransitionKind ==
            "machine_input_material_transfer")
        {
            var stagingCandidates =
                AcquisitionMachineInputMaterialStaging.BuildCandidates(
                snapshot,
                route.UpstreamRoute,
                out stagingReasons);
            candidates = candidates.Concat(stagingCandidates).ToArray();
        }
        if (request.SupportTransitionKind ==
            "machine_capacity_establishment")
        {
            var requestedKind = request.MachineSupportIntentStage ==
                    MachineSupportIntentStages.CraftSelected
                ? "craft_machine_item"
                : request.MachineSupportIntentStage ==
                    MachineSupportIntentStages.PlacementBound
                    ? "place_machine_item"
                    : string.Empty;
            var capacityMatches = AcquisitionMachineCapacitySupport
                .BuildCandidates(
                    snapshot,
                    ledger,
                    processing.GoalId,
                    requirement,
                    route.UpstreamRoute,
                    staticRoute,
                    lowered,
                    requestedKind,
                    out capacityBinding,
                    out capacityReasons);
            candidates = candidates
                .Concat(capacityMatches.Select(match => match.Candidate))
                .ToArray();
        }
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(
                requirement,
                lowered,
                candidates,
                capacityBinding?.MachineQualifiedItemId ?? string.Empty);
        var purchase = AcquisitionRouteSupportingTransitionRequestBuilder
            .PurchasePrerequisite(route);
        if (purchase is not null)
        {
            matches = matches.Where(match =>
                    AcquisitionRouteSupportingTransitionRequestBuilder
                        .PurchaseCandidateMatchesBinding(
                        match.Candidate,
                        purchase))
                .ToArray();
        }
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
            candidateReasons.Concat(stagingReasons).Concat(capacityReasons));
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


}
