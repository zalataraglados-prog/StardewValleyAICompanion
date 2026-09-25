using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunAcquisitionRouteDispatch()
    {
        var snapshot = AcquisitionDispatchSnapshot();
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[] { "foraging.harvest_bushes" },
                true));
        var source = ranked.Single(value => value.Kind == "harvest_bush");
        source.Rank = 999;
        source.Score = 999_999;
        source.ModelScore = 999_999;
        source.ExpectedReward = 999_999;
        source.EstimatedTicks = 120;
        var faster = CloneCandidate(source);
        faster.CandidateId += ":faster";
        faster.Rank = 10_000;
        faster.Score = -999_999;
        faster.ModelScore = -999_999;
        faster.ExpectedReward = -999_999;
        faster.EstimatedTicks = 60;

        var requirement = BushRequirement();
        var lowered = BushLowering();
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectCandidates(
                requirement,
                lowered,
                snapshot,
                new[] { source, faster });
        Require(matches.Length == 2 &&
                matches[0].Candidate.CandidateId == faster.CandidateId,
            "Acquisition dispatch selection used learner rank or score instead of deterministic live cost.");

        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.self-test",
            Revision = 1,
            SourceStateHash = snapshot.StateHash
        };
        var compilation = AcquisitionRouteDispatchCompilationBuilder.Compile(
            "grandpa.stage1.21_points",
            requirement,
            lowered,
            matches[0],
            snapshot,
            ledger,
            "portfolio.dispatch.self-test",
            1,
            new string('a', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue is not null &&
                compilation.ActionQueue.Status == "pending" &&
                !compilation.UsesLearnerRankOrScore &&
                !compilation.FormalTrainingAuthorized,
            "Exact source-bound acquisition route did not compile to a pending native queue.");
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Acquisition dispatch self-test queue is null.");
        var item = queue.Items.Single();
        Require(item.OptionId == "executor.harvest_bush" &&
                item.NormalizedCommand.CommandType == "compiled_action_steps" &&
                item.NormalizedCommand.Parameters.Any(value =>
                    value.Name == "acquisition_endpoint_option_id" &&
                    value.Value == "foraging.harvest_bushes") &&
                item.NormalizedCommand.Parameters.Any(value =>
                    value.Name == "acquisition_source_candidate_id" &&
                    value.Value == faster.CandidateId),
            "Expanded native queue lost its authoritative route lineage.");

        var invalidSource = requirement with
        {
            RouteKind = "native_geode_default_drop",
            SourceId = "Utility.getTreasureFromGeode"
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    invalidSource,
                    lowered,
                    snapshot,
                    new[] { faster }).Length == 0,
            "A same-item candidate from the wrong authoritative source was admitted.");

        var spoofed = CloneCandidate(faster);
        spoofed.Parameters = spoofed.Parameters.Concat(new[]
        {
            new SmallModelActionParameter
            {
                Name = "acquisition_source_id",
                Value = requirement.SourceId
            }
        }).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    requirement,
                    lowered,
                    snapshot,
                    new[] { spoofed }).Length == 0,
            "A live candidate was allowed to inject acquisition lineage.");

        var endpoint = item.NormalizedCommand.Parameters.Single(value =>
            value.Name == "acquisition_endpoint_option_id");
        endpoint.Value = "farm.maintain_crops";
        var tamperReasons = AcquisitionRouteExecutionBindingBuilder
            .ValidateQueue(
                queue,
                compilation.GoalId,
                snapshot.StateHash,
                compilation.SelectedCandidateId,
                requirement,
                lowered,
                "portfolio.dispatch.self-test",
                1)
            .ToArray();
        Require(tamperReasons.Contains(
                "route_queue_option_outside_authoritative_route",
                StringComparer.Ordinal),
            "Tampered expanded-route lineage was not rejected.");

        endpoint.Value = "foraging.harvest_bushes";
        item.NormalizedCommand.CommandType = "option_request";
        var shapeReasons = AcquisitionRouteExecutionBindingBuilder
            .ValidateQueue(
                queue,
                compilation.GoalId,
                snapshot.StateHash,
                compilation.SelectedCandidateId,
                requirement,
                lowered,
                "portfolio.dispatch.self-test",
                1)
            .ToArray();
        Require(shapeReasons.Contains(
                "route_queue_command_binding_invalid",
                StringComparer.Ordinal),
            "An expanded primitive queue was accepted as a legacy option request.");
    }

    private static PolicyEventCandidatePrediction CloneCandidate(
        PolicyEventCandidatePrediction source) =>
        JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Acquisition dispatch self-test candidate clone failed.");

    private static AcquisitionRouteTargetDateUnlock BushRequirement() => new(
        RouteOccurrenceId: "full_shipment:bush:salmonberry",
        RequirementSetId: "full_shipment",
        RequirementId: "ship_salmonberry",
        AlternativeIndex: 0,
        RouteIndex: 0,
        QualifiedItemId: "(O)296",
        MatchKind: "item_id",
        RequiredAmount: 1,
        MinimumQuality: 0,
        RouteKind: "native_bush_shake",
        UncertaintyMode: "deterministic_fresh_receipt",
        SourceId: "Bush.GetShakeOffItem",
        SourceResolutionStatus: "resolved",
        CalendarAxisStatus: "resolved",
        StaticWindowMatchesTargetDate: true,
        MatchingWindows: Array.Empty<AuthoritativeCalendarSourceWindow>(),
        UnlockAxisStatus: "resolved",
        UnlockAxisResolved: true,
        UnlockStateMatchesTargetDate: true,
        UnlockConditions: Array.Empty<AcquisitionUnlockConditionEvaluation>(),
        PendingCalendarConditions: Array.Empty<string>(),
        PendingStochasticConditions: Array.Empty<string>(),
        PendingResourceConditions: Array.Empty<string>(),
        PendingLocationConditions: Array.Empty<string>(),
        UnsupportedConditions: Array.Empty<string>(),
        BlockingReasons: Array.Empty<string>());

    private static AcquisitionRequirementRouteLowering BushLowering() => new(
        RouteKind: "native_bush_shake",
        SourceId: "Bush.GetShakeOffItem",
        SourceAsset: "decompiled native method",
        SourcePath: "Season.Spring => (O)296",
        SupervisionMode: "policy_option",
        UncertaintyMode: "deterministic_fresh_receipt",
        RequiredDownstreamDependencyAxes: Array.Empty<string>(),
        EndpointOptionIds: new[] { "foraging.harvest_bushes" },
        SupportingOptionIds: Array.Empty<string>(),
        RuntimeAdmissionReady: true,
        TeacherAdmissionReady: true);

    private static SnapshotEnvelope AcquisitionDispatchSnapshot()
    {
        const string json = """
        {
          "player": {
            "location_id":{"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills_detail":{"value":{"foraging":{"level":8}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "large_terrain_features":{"value":[{"tile_x":12,"tile_y":10,"runtime_type":"StardewValley.TerrainFeatures.Bush","bounding_tile_width":2,"bounding_tile_height":1,
              "is_bush":true,"bush_size":1,"bush_kind":"ordinary_berry","ready_for_harvest":true,"in_bloom":true,"tile_sheet_offset_before":1,"tile_sheet_offset_expected_after":0,
              "bush_harvest_status":"ready","bush_projection_status":"exact_from_native_bush_shake","bush_output_qualified_item_id":"(O)296",
              "bush_output_quantity_min":1,"bush_output_quantity_max":1,"bush_output_quality":0,
              "bush_foraging_experience_on_success_min":7,"bush_foraging_experience_on_success_max":7,
              "bush_nut_key":"","bush_nut_collected_before":false,"bush_nut_collected_expected_after":false}],
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Forest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Acquisition dispatch self-test snapshot is null.");
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
