using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputSupportingTransition()
    {
        var snapshot = AcquisitionDispatchMachineInputSnapshot();
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.machine-support.self-test",
            SaveId = "machine-support-save",
            PlayerId = "123",
            Revision = 3,
            SourceStateHash = snapshot.StateHash
        };
        var requirement = BushRequirement() with
        {
            RouteOccurrenceId = "full_shipment:machine:keg-wheat",
            RequirementId = "ship_beer",
            QualifiedItemId = "(O)346",
            RouteKind = "machine_output",
            SourceId = "machine:(BC)12:rule:keg_wheat",
            UncertaintyMode = "native_outcome_domain_and_retry_bound"
        };
        var lowering = BushLowering() with
        {
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            SupervisionMode = "policy_option",
            UncertaintyMode = requirement.UncertaintyMode,
            EndpointOptionIds = new[] { "farm.collect_machine_outputs" },
            SupportingOptionIds = new[]
            {
                "farm.establish_supported_machine_capacity",
                "farm.load_supported_machine_input",
                "farm.process_machines"
            }
        };
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "farm.process_machines" },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: ledger);
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "grandpa.stage1.21_points");
        var load = ranked.Where(candidate =>
                candidate.Kind == "load_machine_input_tile")
            .ToArray();
        Require(load.Length == 1 && load[0].Available,
            "Machine support snapshot did not emit one available native load candidate: " +
            string.Join("|", availability.Options.SelectMany(option =>
                option.EventCandidates).Select(candidate =>
                candidate.Kind + ":" + candidate.Available + ":" +
                string.Join(",", candidate.BlockReasons))));
        var rebuilt = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                "grandpa.stage1.21_points",
                requirement,
                new[] { "farm.process_machines" },
                load,
                out var rebuildReasons);
        Require(rebuildReasons.Length == 0 && rebuilt.Length == 1,
            "Machine input support candidate did not rebuild exactly: " +
            string.Join(",", rebuildReasons));
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    requirement,
                    lowering,
                    snapshot,
                    rebuilt).Length == 0,
            "A machine input load was misclassified as a terminal output receipt.");

        var support = AcquisitionRouteDispatchCompilationBuilder
            .SelectSupportingCandidates(requirement, lowering, rebuilt);
        Require(support.Length == 1 &&
                support[0].Candidate.Kind == "load_machine_input_tile" &&
                support[0].RouteOptionRole == "supporting_transition",
            "The exact machine input support transition was not selected uniquely.");

        var wrongOutput = CloneCandidate(support[0].Candidate);
        wrongOutput.Parameters = wrongOutput.Parameters.Select(parameter =>
            parameter.Name == "predicted_output_qualified_item_id"
                ? Parameter(parameter.Name, "(O)348")
                : parameter).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder
                .SelectSupportingCandidates(
                    requirement,
                    lowering,
                    new[] { wrongOutput }).Length == 0,
            "A machine load with the wrong predicted output was admitted.");

        var wrongSource = CloneCandidate(support[0].Candidate);
        wrongSource.Parameters = wrongSource.Parameters.Select(parameter =>
            parameter.Name == "authoritative_route_sources_json"
                ? Parameter(
                    parameter.Name,
                    "[{\"route_kind\":\"machine_output\",\"source_id\":\"machine:(BC)12:rule:wrong\",\"qualified_item_id\":\"(O)346\"}]")
                : parameter).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder
                .SelectSupportingCandidates(
                    requirement,
                    lowering,
                    new[] { wrongSource }).Length == 0,
            "A machine load with the wrong native rule source was admitted.");

        var ambiguous = CloneCandidate(support[0].Candidate);
        ambiguous.Parameters = ambiguous.Parameters.Select(parameter =>
            parameter.Name == "authoritative_route_sources_json"
                ? Parameter(
                    parameter.Name,
                    "[{\"route_kind\":\"machine_output\",\"source_id\":\"machine:(BC)12:rule:keg_wheat\",\"qualified_item_id\":\"(O)346\"},{\"route_kind\":\"native_machine_item_query_output\",\"source_id\":\"machine:(BC)12:rule:0:output:0\",\"qualified_item_id\":\"(O)346\"}]")
                : parameter).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder
                .SelectSupportingCandidates(
                    requirement,
                    lowering,
                    new[] { ambiguous }).Length == 0,
            "An ambiguous machine output source set was admitted.");

        var compilation = AcquisitionRouteDispatchCompilationBuilder.Compile(
            "grandpa.stage1.21_points",
            requirement,
            lowering,
            support[0],
            snapshot,
            ledger,
            "machine-support.self-test",
            ledger.Revision,
            new string('b', 64));
        Require(compilation.DispatchReady &&
                compilation.Status ==
                    "ready_for_supporting_transition_dispatch" &&
                compilation.FreshReplanRequiredAfterSuccess &&
                !compilation.TerminalReceiptEligible &&
                compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.load_machine_input",
            "Exact machine input support did not compile through the shared native queue: " +
            string.Join(",", compilation.BlockingReasons));
        VerifyMachineInputSupportingRequest(
            snapshot,
            ledger,
            requirement,
            lowering,
            support);
        VerifyMachineInputMaterialStagingTransition();
    }

    private static SnapshotEnvelope AcquisitionDispatchMachineInputSnapshot()
    {
        const string json = """
        {
          "time":{"time":{"value":900,"status":"available"},"total_days":{"value":0,"status":"available"}},
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":63,"status":"available"},
            "tile_y":{"value":15,"status":"available"},
            "inventory":{"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":25}],"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available"}
          },
          "farm":{"machines":{"value":[{
            "location_id":"Farm","location_kind":"farm_outdoor","location_is_current":true,
            "machine_has_input":true,"machine_has_output":true,
            "tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg",
            "ready_for_harvest":false,"minutes_until_ready":-1,
            "machine_execution_semantics":{"status":"available","execution_status":"available_data_driven","input_dispatch_kind":"base_object_data_driven","prediction_training_status":"exact_current_snapshot_probe_supported"},
            "machine_data":{"status":"available","has_output":true,"additional_consumed_item_count":0,"output_rule_count":1,"output_rules":[{"id":"keg_wheat","required_item_id":"(O)262","minutes_until_ready":1750,"output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200}}]},
            "held_item":null,
            "loadable_inputs":[{
              "slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":25,
              "predicted_output":{"status":"available","training_eligibility_status":"exact_current_snapshot_probe_supported","source":"MachineDataUtility.GetOutputItem(probe:true)","matched_rule_id":"keg_wheat","matched_rule_index":0,"matched_output_index":0,"required_item_id":"(O)262","required_count":1,"additional_consumed_item_count":0,"effective_minutes_until_ready":1750,"output_context_tags":["artisan_good","id_o_346"],"item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"quality":0,"sale_price":200},"sale_price":200,"stack":1,"quality":0,"authoritative_route_sources":[{"route_kind":"machine_output","source_id":"machine:(BC)12:rule:keg_wheat","qualified_item_id":"(O)346"}]},
              "probe_source":"Object.performObjectDropInAction(probe:true)","load_executor_status":"covered_for_runtime_load"
            }]
          }],"status":"available"},
          "material_inventory_graph":{"value":{"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","actor_use_authorized":true,"slots":[{"slot_index":0,"qualified_item_id":"(O)262","stack":2,"quality":0,"sale_price":25}]}]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Machine input support snapshot is null.");
        return new SnapshotEnvelope
        {
            SaveId = new FieldEnvelope<string?>
            {
                Value = "machine-support-save",
                Status = "available"
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = "available"
            },
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-27T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
