using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed class MachineRelocationIntentPlanBinderTests
{
    [Fact]
    public void BindsCrossLocationRouteAndBenefitFields()
    {
        var repository = new CapturingRepository();
        var snapshot = new SnapshotEnvelope
        {
            StateHash = "state:source"
        };
        var plan = new SmallModelPlanEnvelope
        {
            Steps = new[]
            {
                new SmallModelPlanStep
                {
                    StepId = "step:remove",
                    Kind = "remove_machine_item",
                    TargetLocation = "Farm",
                    TargetTileX = 56,
                    TargetTileY = 15,
                    Preconditions = new[]
                    {
                        "candidate_id:machine-relocate:cross"
                    },
                    Parameters = new[]
                    {
                        Parameter(
                            "relocation_intent_id",
                            "layout:Farm:56,15->FarmHouse:27,29:(BC)12"),
                        Parameter("qualified_item_id", "(BC)12"),
                        Parameter("item_id", "12"),
                        Parameter(
                            "relocation_target_location_id",
                            "FarmHouse"),
                        Parameter("relocation_target_tile_x", "27"),
                        Parameter("relocation_target_tile_y", "29"),
                        Parameter(
                            "machine_placement_projection_fingerprint",
                            "placement:fingerprint"),
                        Parameter("layout_net_benefit_ticks", "2280"),
                        Parameter(
                            "relocation_route_connector_count",
                            "1"),
                        Parameter(
                            "relocation_route_connector_kind",
                            "building_door"),
                        Parameter(
                            "relocation_route_estimated_ticks",
                            "1200"),
                        Parameter(
                            "relocation_route_segments_json",
                            """[{"index":0,"kind":"building_door","from_location_id":"Farm","from_tile_x":20,"from_tile_y":5,"target_location_id":"FarmHouse","arrival_tile_x":27,"arrival_tile_y":30,"approach_distance_tiles":19,"estimated_ticks":1200}]"""),
                        Parameter(
                            "relocation_target_arrival_tile_x",
                            "27"),
                        Parameter(
                            "relocation_target_arrival_tile_y",
                            "30"),
                        Parameter(
                            "relocation_target_stand_tile_x",
                            "31"),
                        Parameter(
                            "relocation_target_stand_tile_y",
                            "30"),
                        Parameter(
                            "relocation_target_route_distance_tiles",
                            "4"),
                        Parameter(
                            "layout_relocation_cost_ticks",
                            "1620"),
                        Parameter(
                            "layout_benefit_policy",
                            "existing_machine_cluster_resolved_route_over_eight_cycles"),
                        Parameter(
                            "relocation_target_selection_policy",
                            "resolved_route_final_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked"),
                        Parameter(
                            "layout_time_estimate_policy",
                            "source_approach_plus_resolved_route_static_bfs_plus_target_static_bfs_runtime_rechecked")
                    }
                }
            }
        };

        var result = MachineRelocationIntentPlanBinder.Bind(
            plan,
            snapshot,
            repository);

        Assert.NotNull(result);
        Assert.True(result.Accepted);
        var request = Assert.IsType<MachineRelocationIntentUpsertRequest>(
            repository.Request);
        Assert.Equal(7, request.ExpectedLedgerRevision);
        Assert.Equal("machine-relocate:cross", request.SourceDecisionId);
        Assert.Equal("Farm", request.SourceLocationId);
        Assert.Equal("FarmHouse", request.TargetLocationId);
        Assert.Equal(1, request.RouteConnectorCount);
        Assert.Equal("building_door", request.RouteConnectorKind);
        Assert.Equal(1200, request.RouteEstimatedTicks);
        var segment = Assert.Single(request.RouteSegments);
        Assert.Equal("Farm", segment.FromLocationId);
        Assert.Equal("FarmHouse", segment.TargetLocationId);
        Assert.Equal(19, segment.ApproachDistanceTiles);
        Assert.Equal(27, request.TargetArrivalTileX);
        Assert.Equal(30, request.TargetArrivalTileY);
        Assert.Equal(31, request.TargetStandTileX);
        Assert.Equal(30, request.TargetStandTileY);
        Assert.Equal(4, request.TargetRouteDistanceTiles);
        Assert.Equal(1620, request.LayoutRelocationCostTicks);
        Assert.Equal(
            "existing_machine_cluster_resolved_route_over_eight_cycles",
            request.LayoutBenefitPolicy);
        Assert.Equal(
            "resolved_route_final_arrival_static_bfs_reachable_native_legal_then_runtime_rechecked",
            request.TargetSelectionPolicy);
        Assert.Equal(
            "source_approach_plus_resolved_route_static_bfs_plus_target_static_bfs_runtime_rechecked",
            request.TimeEstimatePolicy);
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private sealed class CapturingRepository :
        IStrategyCommitmentRepository
    {
        public MachineRelocationIntentUpsertRequest? Request { get; private set; }

        public StrategyCommitmentLedger Get(SnapshotEnvelope snapshot) =>
            new()
            {
                LedgerId = "ledger:test",
                Revision = 7,
                SourceStateHash = snapshot.StateHash
            };

        public StrategyCommitmentMutationResult UpsertMachineRelocation(
            SnapshotEnvelope snapshot,
            MachineRelocationIntentUpsertRequest request)
        {
            Request = request;
            return new StrategyCommitmentMutationResult
            {
                Accepted = true,
                Ledger = Get(snapshot)
            };
        }

        public StrategyCommitmentMutationResult Upsert(
            SnapshotEnvelope snapshot,
            CropPlantingCommitmentUpsertRequest request) =>
            throw new NotSupportedException();

        public StrategyCommitmentMutationResult Cancel(
            SnapshotEnvelope snapshot,
            string commitmentId,
            StrategyCommitmentCancelRequest request) =>
            throw new NotSupportedException();

        public StrategyCommitmentMutationResult UpsertMaterial(
            SnapshotEnvelope snapshot,
            MaterialReservationUpsertRequest request) =>
            throw new NotSupportedException();

        public StrategyCommitmentMutationResult CancelMaterial(
            SnapshotEnvelope snapshot,
            string reservationId,
            StrategyCommitmentCancelRequest request) =>
            throw new NotSupportedException();
    }
}
