using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineFacilityResolution()
    {
        const string machineJson = """
        {
          "farm": {
            "machines": {
              "status": "available",
              "value": [
                {
                  "location_id": "Farm",
                  "tile_x": 12,
                  "tile_y": 34,
                  "qualified_item_id": "(BC)12",
                  "location_is_player_controlled": true,
                  "owner_player_id": 42,
                  "ready_for_harvest": false,
                  "minutes_until_ready": 1750,
                  "machine_has_input": true,
                  "machine_has_output": true,
                  "machine_row_count_total": 1,
                  "machine_row_snapshot_status": "complete_no_row_truncation",
                  "machine_input_probe_eligible_count": 0
                }
              ]
            }
          }
        }
        """;
        using var document = JsonDocument.Parse(machineJson);
        var fleet = AcquisitionMachineFleetSnapshotState.Read(
            document.RootElement);
        Require(fleet.EvidenceAvailable && fleet.Rows.Length == 1,
            "Complete machine fleet evidence was not accepted.");
        var machine = fleet.Rows.Single();
        Require(machine.LocationId == "Farm" &&
                machine.TileX == 12 &&
                machine.TileY == 34 &&
                machine.QualifiedItemId == "(BC)12" &&
                machine.CapacityState == "processing",
            "Machine fleet identity or capacity state drifted.");

        var staticRoute = MachineFacilityStaticRoute();
        var resolution = AcquisitionLocationRouteTargetResolver.ResolveMachine(
            staticRoute,
            fleet);
        var target = resolution.Targets.Single();
        Require(resolution.EvidenceComplete &&
                target.BindingKind == "runtime_machine_source_location" &&
                target.SourceKey == staticRoute.SourceId &&
                target.LocationId == "Farm" &&
                target.TargetTileX == 12 &&
                target.TargetTileY == 34,
            "Machine location target binding drifted.");
        var targetEvaluation = new AcquisitionLocationRouteTargetEvaluation(
            target.BindingKind,
            target.SourceKey,
            target.LocationId,
            "Default",
            "resolved_location_route_match",
            "all",
            610,
            1,
            "Exact",
            "fixture",
            Array.Empty<StardewAI.Core.Infrastructure.TransparentRouteEdge>(),
            target.EvidencePaths,
            Array.Empty<string>(),
            target.TargetTileX,
            target.TargetTileY);
        var facility = AcquisitionRouteTargetDateFacilityBuilder
            .EvaluateMachineTarget(
                targetEvaluation,
                staticRoute.MachineSource!,
                fleet);
        Require(facility.MachineSourceMatches == true &&
                facility.MachineQualifiedItemId == "(BC)12" &&
                facility.TargetTileX == 12 &&
                facility.TargetTileY == 34 &&
                facility.MachineCapacityState == "processing",
            "Machine facility capacity binding drifted.");

        const string emptyFleetJson = """
        {
          "farm": {
            "machines": {
              "status": "available",
              "value": []
            }
          }
        }
        """;
        using var emptyDocument = JsonDocument.Parse(emptyFleetJson);
        var emptyResolution = AcquisitionLocationRouteTargetResolver
            .ResolveMachine(
                staticRoute,
                AcquisitionMachineFleetSnapshotState.Read(
                    emptyDocument.RootElement));
        Require(emptyResolution.EvidenceComplete &&
                emptyResolution.Targets.Length == 0 &&
                emptyResolution.Reasons.SequenceEqual(new[]
                {
                    "matching_machine_runtime_location_not_present"
                }),
            "An empty complete machine fleet did not resolve as a source miss.");

        var invalidJson = machineJson.Replace(
            "\"machine_row_count_total\": 1",
            "\"machine_row_count_total\": 2",
            StringComparison.Ordinal);
        using var invalidDocument = JsonDocument.Parse(invalidJson);
        var invalidFleet = AcquisitionMachineFleetSnapshotState.Read(
            invalidDocument.RootElement);
        Require(!invalidFleet.EvidenceAvailable &&
                invalidFleet.BlockingReasons.SequenceEqual(new[]
                {
                    "machine_fleet_row_completeness_invalid"
                }),
            "A truncated machine fleet was not rejected.");
    }

    private static AcquisitionRouteCalendarResolution
        MachineFacilityStaticRoute() => new(
            "full_shipment:full_shipment:item:346:0:0",
            "full_shipment",
            "full_shipment:item:346",
            0,
            0,
            "346",
            "(O)346",
            "item_id",
            1,
            0,
            "machine_output",
            "deterministic_or_condition_bound",
            "machine:(BC)12:rule:keg_wheat",
            "Data/Machines",
            "payload.(BC)12.OutputRules[0].OutputItem[0].ItemId",
            "resolved_static_source_window_target_date_pending",
            "runtime_machine_output_rule",
            Array.Empty<AuthoritativeCalendarSourceWindow>(),
            Array.Empty<string>(),
            MachineSource: new AcquisitionMachineSourceEvidence(
                "(BC)12",
                "keg_wheat",
                0,
                0,
                string.Empty,
                true,
                1750,
                -1,
                false,
                false,
                0,
                Array.Empty<AcquisitionMachineNumericModifierEvidence>(),
                new[]
                {
                    new AcquisitionMachineTriggerEvidence(
                        string.Empty,
                        1,
                        "262",
                        Array.Empty<string>(),
                        1,
                        string.Empty)
                },
                Array.Empty<AcquisitionMachineConsumedItemEvidence>(),
                "(O)346",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                1,
                1,
                0,
                false,
                0,
                Array.Empty<AcquisitionMachineNumericModifierEvidence>(),
                0,
                Array.Empty<AcquisitionMachineNumericModifierEvidence>(),
                1,
                false));
}
