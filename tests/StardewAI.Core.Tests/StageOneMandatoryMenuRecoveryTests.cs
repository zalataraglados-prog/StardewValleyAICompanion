using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StageOneMandatoryMenuRecoveryTests
{
    [Fact]
    public void AvailableFishingRewardRecoveryPreemptsCollectionCandidates()
    {
        var ranking = Ranking(
            Candidate(
                "collection",
                "fishing.catch_fish",
                "catch_fish_current",
                rank: 1,
                score: 1,
                available: true),
            Candidate(
                "reward-recovery",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 2,
                score: 0.3,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("reward-recovery", selected.CandidateId);
    }

    [Fact]
    public void UnavailableOrNonMenuRecoveryDoesNotPreemptCollection()
    {
        var ranking = Ranking(
            Candidate(
                "unavailable-menu",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 1,
                score: 1,
                available: false),
            Candidate(
                "day-recovery",
                "recovery.stabilize_day",
                "recovery_return_home",
                rank: 2,
                score: 0.5,
                available: true));

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(ranking));
    }

    [Fact]
    public void LowestRankWinsWithStableTieBreaking()
    {
        var ranking = Ranking(
            Candidate(
                "close-menu",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 2,
                score: 0.9,
                available: true),
            Candidate(
                "reward-b",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 1,
                score: 0.3,
                available: true),
            Candidate(
                "reward-a",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.FishingItemGrabRewardKind,
                rank: 1,
                score: 0.4,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("reward-a", selected.CandidateId);
    }

    [Fact]
    public void OpenDialoguePreflightBuildsDispatchableRecoveryBeforeRouteTiming()
    {
        var snapshot = DialogueSnapshot(
            menuOpen: true,
            routeTimingReadyNow: false);

        Assert.True(StageOneMandatoryMenuRecovery.RequiresPreflight(snapshot));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            ["recovery.stabilize_day"],
            includeExecutorCalibrationOptions: true);
        var recoveryOption = Assert.Single(availability.Options);
        var closeCandidate = Assert.Single(
            recoveryOption.EventCandidates,
            candidate => string.Equals(
                candidate.Kind,
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                StringComparison.Ordinal));
        Assert.True(
            closeCandidate.Available,
            string.Join(",", closeCandidate.BlockReasons));
        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");
        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal(StageOneMandatoryMenuRecovery.CloseMenuKind, selected.Kind);
        var plan = new DailyPlanCompiler().Compile(
            [selected],
            snapshot.StateHash,
            "grandpa_evaluation_max_score",
            maxCandidates: 1);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        Assert.NotEmpty(queue.Items);
        Assert.All(queue.Items, item => Assert.Equal("pending", item.Status));
    }

    [Fact]
    public void ClosedMenuSkipsMandatoryRecoveryPreflight()
    {
        Assert.False(StageOneMandatoryMenuRecovery.RequiresPreflight(
            DialogueSnapshot(
                menuOpen: false,
                routeTimingReadyNow: false)));
    }

    [Fact]
    public void MailProcessingSuppressesGenericMenuRecoveryInPreflightRanking()
    {
        var availability = new OptionAvailabilityEnvelope
        {
            Options =
            [
                new OptionAvailability
                {
                    OptionId = "recovery.stabilize_day",
                    EventCandidates =
                    [
                        new EventCandidate
                        {
                            CandidateId = "recovery:close_blocking_menu",
                            Kind = StageOneMandatoryMenuRecovery.CloseMenuKind,
                            Available = true
                        }
                    ]
                },
                new OptionAvailability
                {
                    OptionId = "mail.process_letter",
                    EventCandidates =
                    [
                        new EventCandidate
                        {
                            CandidateId = "mail:process_open_letter",
                            Kind = "process_open_letter",
                            Available = true
                        }
                    ]
                }
            ]
        };

        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(ranking));
        Assert.Contains(
            ranking.RankedEventCandidates,
            candidate => candidate.Kind == "process_open_letter");
    }

    [Fact]
    public void LiveLoopDispatchesMandatoryMenuRecoveryBeforeMembershipFiltering()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs");
        var recoverySource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneMandatoryMenuRecovery.cs");
        var preflightInvocation = source.IndexOf(
            "TryBuildStageOneMandatoryMenuRecoveryAsync(",
            StringComparison.Ordinal);
        var preflight = recoverySource.IndexOf(
            "StageOneMandatoryMenuRecovery.RequiresPreflight(typedSnapshot)",
            StringComparison.Ordinal);
        var selection = recoverySource.IndexOf(
            "StageOneMandatoryMenuRecovery.TrySelect(",
            preflight,
            StringComparison.Ordinal);
        var intentBuilder = source.IndexOf(
            "MasterAnglerTargetDateIntentBuilder.Build(",
            StringComparison.Ordinal);
        var continuationFilter = source.IndexOf(
            "activeObjectiveContinuation is not null",
            intentBuilder,
            StringComparison.Ordinal);
        var membership = source.IndexOf(
            "CurrentStageOneCollectionTeacherPreferenceBuilder.Build(",
            continuationFilter,
            StringComparison.Ordinal);

        Assert.True(preflightInvocation >= 0);
        Assert.True(preflight >= 0);
        Assert.True(selection > preflight);
        Assert.True(intentBuilder > preflightInvocation);
        Assert.True(continuationFilter > intentBuilder);
        Assert.True(membership > continuationFilter);
        Assert.Contains(
            "\"skills.choose_profession\"",
            recoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"mail.process_letter\"",
            recoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ready_mandatory_menu_recovery_control",
            recoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "teacher_preference_label_eligible = false",
            recoverySource,
            StringComparison.Ordinal);
    }

    private static SnapshotEnvelope DialogueSnapshot(
        bool menuOpen,
        bool routeTimingReadyNow)
    {
        var json = $$"""
        {
          "schema_version":"snapshot.v1",
          "state_hash":"mandatory-menu-preflight-state",
          "state":{
            "time":{
              "time":{"value":1630,"status":"available"}
            },
            "player":{
              "location_id":{"value":"ScienceHouse","status":"available"},
              "tile_x":{"value":8,"status":"available"},
              "tile_y":{"value":20,"status":"available"},
              "current_item_qualified_id":{"value":"","status":"available"},
              "active_object_qualified_id":{"value":"","status":"available"},
              "energy":{"value":270,"status":"available"}
            },
            "current_location":{
              "home_context":{"value":{
                "home_available":true,
                "home_location_id":"FarmHouse",
                "current_location_id":"ScienceHouse",
                "current_location_is_home":false,
                "entry_tile_x":3,
                "entry_tile_y":9,
                "bed_tile_x":3,
                "bed_tile_y":8,
                "bed_tile_has_bed":true,
                "sleep_executor_enabled":true
              },"status":"available"},
              "movement_timing_context":{"value":{
                "compatible":true,
                "route_timing_ready_now":{{routeTimingReadyNow.ToString().ToLowerInvariant()}},
                "player_can_move_now":false,
                "native_control_gate_open":true,
                "global_fade":false
              },"status":"available"}
            },
            "menus":{
              "active_menu":{"value":{
                "is_open":{{menuOpen.ToString().ToLowerInvariant()}},
                "type":"DialogueBox",
                "full_type":"StardewValley.Menus.DialogueBox",
                "last_question_key":null,
                "is_sleep_prompt":false,
                "event_up":false,
                "dialogue_is_question":false,
                "dialogue_response_count":0,
                "dialogue_transitioning":false,
                "dialogue_safety_timer":0,
                "dialogue_character_present":true,
                "dialogue_speaker_name":"Robin"
              },"status":"available"},
              "menu_specific_state":{"value":{},"status":"available"},
              "sleep_prompt_context":{"value":{
                "prompt_open":false
              },"status":"available"}
            },
            "locations":{
              "collision_grid":{"value":{
                "width":32,
                "height":32,
                "tiles":[]
              },"status":"available"},
              "route_action_branch_coverage":{"value":[],"status":"available"}
            }
          }
        }
        """;
        return JsonSerializer.Deserialize<SnapshotEnvelope>(json) ??
            throw new InvalidDataException("Test snapshot is empty.");
    }

    private static AvailabilityAwarePolicyPredictionEnvelope Ranking(
        params PolicyEventCandidatePrediction[] candidates) => new()
        {
            RankedEventCandidates = candidates
        };

    private static PolicyEventCandidatePrediction Candidate(
        string id,
        string optionId,
        string kind,
        int rank,
        double score,
        bool available) => new()
        {
            CandidateId = id,
            OptionId = optionId,
            Kind = kind,
            Rank = rank,
            Score = score,
            Available = available
        };

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
