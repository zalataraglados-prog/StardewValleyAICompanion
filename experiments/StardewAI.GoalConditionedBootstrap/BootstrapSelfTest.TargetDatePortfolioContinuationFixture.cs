using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioContinuationFixture(
        AcquisitionRouteExecutionBindingInputs template,
        string shopRouteOccurrenceId,
        string fishRouteOccurrenceId)
    {
        var outputRoot = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "portfolio-continuation-fixture");
        Directory.CreateDirectory(outputRoot);
        var snapshotPath = Path.Combine(outputRoot, "before-snapshot.json");
        var snapshot = JsonNode.Parse(
            File.ReadAllText(template.BeforeSnapshotPath))!.AsObject();
        const string stateHash = "three-transition-portfolio-state";
        snapshot["state_hash"] = stateHash;
        var capacity = snapshot["state"]!["locations"]!
            ["social_route_date_evidence"]!["value"]!["locations"]!
            .AsArray()
            .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
            ["cultivation_capacity"]!.AsObject();
        capacity["total_prepared_soil_slot_count"] = 1;
        capacity["open_prepared_soil_slot_count"] = 0;
        capacity["occupied_crop_slot_count"] = 1;
        capacity["unresolved_harvest_item_slot_count"] = 0;
        capacity["occupied_harvest_items"] = new JsonArray(
            new JsonObject
            {
                ["harvest_item_qualified_id"] = "(O)24",
                ["is_garden_pot"] = false,
                ["slot_count"] = 1
            });
        snapshot["state"]!["farm"]!["crops"]!["value"] = new JsonArray(
            ReadyCrop(1));
        File.WriteAllText(
            snapshotPath,
            snapshot.ToJsonString(JsonDefaults.Options));
        var ledgerPath = Path.Combine(outputRoot, "strategy-ledger.json");
        WriteEmptyStrategyLedger(ledgerPath, stateHash);
        var proposalPath = Path.Combine(outputRoot, "proposal.json");
        var portfolioInputs = BuildTargetDatePortfolioInputs(
            template,
            snapshotPath,
            ledgerPath,
            proposalPath,
            outputRoot,
            targetTotalDay: 0);
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            portfolioInputs.TargetDateOpportunityCostPath,
            "Portfolio continuation fixture opportunity cost");
        var shop = opportunity.Routes.Single(route =>
            route.RouteOccurrenceId == shopRouteOccurrenceId);
        var fish = opportunity.Routes.Single(route =>
            route.RouteOccurrenceId == fishRouteOccurrenceId);
        var crop = opportunity.Routes.Single(route =>
            route.RouteOccurrenceId.StartsWith(
                "full_shipment:",
                StringComparison.Ordinal));
        var shopRequirement = TargetDateRequirementRoute(shop);
        var fishRequirement = TargetDateRequirementRoute(fish);
        var cropRequirement = TargetDateRequirementRoute(crop);
        var requestPath = Path.Combine(outputRoot, "preference-request.json");
        Write(requestPath, new AcquisitionRoutePortfolioTeacherPreferenceRequest
        {
            RequestId = "self-test-shop-fish-teacher",
            GoalId = opportunity.GoalId,
            SnapshotStateHash = opportunity.SnapshotStateHash,
            ExpectedLedgerRevision = 0,
            ScopedRequirements = new[]
            {
                new AcquisitionRoutePortfolioRequirementScope(
                    shopRequirement.RequirementSetId,
                    shopRequirement.RequirementId),
                new AcquisitionRoutePortfolioRequirementScope(
                    fishRequirement.RequirementSetId,
                    fishRequirement.RequirementId),
                new AcquisitionRoutePortfolioRequirementScope(
                    cropRequirement.RequirementSetId,
                    cropRequirement.RequirementId)
            }
        });

        var preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .Build(portfolioInputs, requestPath);
        var preferencePath = Path.Combine(outputRoot, "preference.json");
        Write(preferencePath, preference);
        var expectedRoutes = new[]
            {
                shopRouteOccurrenceId,
                fishRouteOccurrenceId,
                crop.RouteOccurrenceId
            }
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(preference.TeacherPreferenceLabelEligible &&
                preference.CandidateDenominatorComplete &&
                preference.CandidateDenominatorCount == 1 &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                preference.SelectedProposal.SelectedRouteOccurrenceIds
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(expectedRoutes, StringComparer.Ordinal),
            "Three-route portfolio Teacher preference drifted: status=" +
            preference.Status + "; denominator=" +
            preference.CandidateDenominatorCount + "; selected=" +
            string.Join(",", preference.SelectedProposal?
                .SelectedRouteOccurrenceIds ?? Array.Empty<string>()) +
            "; blocks=" + string.Join(",", preference.BlockingReasons));
        var admissionPath = Path.Combine(outputRoot, "admission.json");
        Write(proposalPath, preference.SelectedProposal!);
        Write(admissionPath, preference.SelectedAdmission!);

        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Portfolio continuation fixture snapshot");
        var baseLedger = CurrentTeacherFrontierSupport.Read<
            StrategyCommitmentLedger>(
            ledgerPath,
            "Portfolio continuation fixture base ledger");
        var commit = new ReservationPortfolioLedgerService().Commit(
            baseLedger,
            before,
            preference.SelectedAdmission!.AtomicCommitRequest!,
            "2026-09-21T00:00:00Z");
        Require(commit.Accepted && commit.Ledger is not null,
            "Three-route portfolio atomic commit failed.");
        var commitResultPath = Path.Combine(outputRoot, "commit-result.json");
        var committedLedgerPath = Path.Combine(
            outputRoot,
            "committed-ledger.json");
        Write(commitResultPath, commit);
        Write(committedLedgerPath, commit.Ledger!);
        var commitReceipt = AcquisitionRoutePortfolioCommitReceiptBuilder.Build(
            portfolioInputs,
            admissionPath,
            committedLedgerPath,
            commitResultPath);
        Require(commitReceipt.PortfolioCommitVerified &&
                commitReceipt.AtomicMutationObserved,
            "Three-route portfolio commit receipt drifted.");
        var commitReceiptPath = Path.Combine(outputRoot, "commit-receipt.json");
        Write(commitReceiptPath, commitReceipt);

        var executionInputs = ContinuationBindingInputs(
            portfolioInputs,
            admissionPath,
            requestPath,
            preferencePath,
            commitReceiptPath,
            committedLedgerPath,
            commitResultPath,
            Path.Combine(outputRoot, "queue.json"),
            shopRouteOccurrenceId);
        VerifyTargetDateFreshTerminalReceipt(
            executionInputs,
            Path.Combine(outputRoot, "execution-binding.json"),
            Path.Combine(outputRoot, "after-snapshot.json"),
            Path.Combine(outputRoot, "execution-receipt.json"),
            Path.Combine(outputRoot, "insufficient-after-snapshot.json"),
            expectedPortfolioCompletion: false);

        static JsonObject ReadyCrop(int tileX) => new()
        {
            ["location_id"] = "Farm",
            ["tile_x"] = tileX,
            ["tile_y"] = 1,
            ["harvest_item_qualified_id"] = "(O)24",
            ["harvest_item_projection_status"] =
                "exact_from_live_index_of_harvest",
            ["dead"] = false,
            ["ready_for_harvest"] = true,
            ["days_until_next_harvest_if_watered"] = 0
        };
    }

}
