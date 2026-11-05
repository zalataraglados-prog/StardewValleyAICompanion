using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingControlComparisonScope(List<string> passed)
    {
        foreach (var mode in new[] { "unknown_alternative", "selected_unknown", "protected_input" })
        {
            var fixture = new RustyKeyPortfolioFixture();
            var alternative = JsonSerializer.SerializeToNode(fixture.Opportunity.Routes[59], JsonDefaults.Options)!;
            var cursor = alternative;
            while (cursor is JsonObject row)
            {
                if (row.ContainsKey("route_occurrence_id")) row["route_occurrence_id"] = "unresolved-alternative-59";
                cursor = row["upstream_route"];
            }
            alternative["opportunity_cost_axis_resolved"] = false;
            alternative["blocking_reasons"] = new JsonArray("synthetic_unsupported_other_route");
            fixture.Opportunity.Routes = fixture.Opportunity.Routes.Append(
                alternative.Deserialize<AcquisitionRouteTargetDateOpportunityCost>(JsonDefaults.Options)!).ToArray();
            fixture.Opportunity.RouteOccurrenceCount++;
            for (var index = 60; index < 95; index++) fixture.ChangeRoute(index, row =>
            {
                row["opportunity_cost_axis_resolved"] = false;
                row["blocking_reasons"] = new JsonArray("synthetic_other_item_unavailable");
            });
            if (mode == "selected_unknown") fixture.ChangeRoute(59, row => row["opportunity_cost_axis_resolved"] = false);
            if (mode == "protected_input")
            {
                fixture.AddMaterialClaim(59);
                fixture.Ledger.MaterialReservations = new[] { new MaterialReservation
                {
                    ReservationId = "other-goal", SourceDecisionId = "other-goal", GoalId = "other.goal", Revision = 1,
                    OwnerPlayerId = 1, SourceStateHash = fixture.Snapshot.StateHash, NodeId = "player:1", SlotIndex = 0,
                    QualifiedItemId = "(O)388", Quantity = 1, Purpose = "protected other goal"
                } };
            }
            var before = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var control = fixture.RunRolling();
            Require(control.RollingStepReady == (mode == "unknown_alternative") &&
                control.CompleteMuseumItemDenominator.Length == 95 && !control.TeacherPreferenceLabelEligible &&
                !control.FormalTrainingAuthorized && before == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "Unknown alternatives cannot block known safe control or authorize unknown/protected selected work: " + mode);
            if (mode == "unknown_alternative")
            {
                var admission = control.SelectedAcquisitionAdmission!;
                Require(admission.PortfolioAdmissionReady && admission.AtomicCommitPreflightPassed &&
                    !admission.AllRoutesOnCompleteParetoFrontier && admission.ControlOnlyIncompleteComparisonScopes is { Length: 1 } &&
                    control.SelectedCandidateId == "route-59" && control.CandidateEvaluations.Any(row =>
                        row.CandidateId == "unresolved-alternative-59" && !row.Ready),
                    "Control must retain the incomplete comparison and rejected alternative, not invent a complete Pareto certificate.");
                var strict = AcquisitionRoutePortfolioBuilder.Build(fixture.Context, control.SelectedAcquisitionProposal!, "strict-proposal");
                Require(!strict.PortfolioAdmissionReady && strict.ControlOnlyIncompleteComparisonScopes is null &&
                    strict.BlockingReasons.Any(reason => reason.StartsWith("scoped_opportunity_cost_frontier_incomplete:", StringComparison.Ordinal)),
                    "The original generic/Teacher admission must still reject this incomplete comparison scope.");
                var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(fixture.Opportunity.Routes[59]);
                Require(AcquisitionRouteExecutionBindingBuilder.AllowsVerifiedControlComparisonScope(control, requirement) &&
                    !AcquisitionRouteExecutionBindingBuilder.AllowsVerifiedControlComparisonScope(null, requirement),
                    "Only the verified control source may consume its explicitly incomplete comparison scope.");
                var foreignRequirement = JsonSerializer.SerializeToNode(requirement, JsonDefaults.Options)!;
                foreignRequirement["requirement_id"] = "foreign-scope";
                Require(!AcquisitionRouteExecutionBindingBuilder.AllowsVerifiedControlComparisonScope(control,
                    foreignRequirement.Deserialize<AcquisitionRouteTargetDateUnlock>(JsonDefaults.Options)!),
                    "A verified control source cannot exempt a different requirement scope.");
                foreignRequirement = JsonSerializer.SerializeToNode(requirement, JsonDefaults.Options)!;
                foreignRequirement["route_occurrence_id"] = "foreign-route";
                Require(!AcquisitionRouteExecutionBindingBuilder.AllowsVerifiedControlComparisonScope(control,
                    foreignRequirement.Deserialize<AcquisitionRouteTargetDateUnlock>(JsonDefaults.Options)!),
                    "A verified control source cannot exempt an unselected route.");
            }
            passed.Add("control_comparison_scope_" + mode);
        }
    }
}
