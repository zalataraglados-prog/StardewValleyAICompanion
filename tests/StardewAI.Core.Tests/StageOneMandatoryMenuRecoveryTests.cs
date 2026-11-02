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
    public void ExactSleepPromptRecoveryPassesMandatoryMenuPreflight()
    {
        var snapshot = ExactSleepPromptSnapshot();

        Assert.True(StageOneMandatoryMenuRecovery.RequiresPreflight(snapshot));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            ["recovery.stabilize_day"],
            includeExecutorCalibrationOptions: true);
        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("recovery:resume_sleep_prompt", selected.CandidateId);
        Assert.Equal(
            StageOneMandatoryMenuRecovery.ResumeSleepPromptKind,
            selected.Kind);
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
    public void ExplicitProfessionChoicePreemptsGenericMenuRecovery()
    {
        var ranking = Ranking(
            Candidate(
                "generic-close",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 1,
                score: 1,
                available: true),
            Candidate(
                "profession-1",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 3,
                score: 0.4,
                available: true),
            Candidate(
                "profession-4",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 2,
                score: 0.5,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(ranking);

        Assert.NotNull(selected);
        Assert.Equal("profession-4", selected.CandidateId);
        Assert.Equal("skills.choose_profession", selected.OptionId);
    }

    [Fact]
    public void ExplicitProfessionCandidateIdentityOverridesBaselinePreference()
    {
        var ranking = Ranking(
            Candidate(
                "skills:profession:0:5:1",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 1,
                score: 1,
                available: true),
            Candidate(
                "skills:profession:0:5:0",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 2,
                score: 0.5,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: true,
            preferredProfessionCandidateId: "skills:profession:0:5:0");

        Assert.NotNull(selected);
        Assert.Equal("skills:profession:0:5:0", selected.CandidateId);
    }

    [Fact]
    public void ExplicitProfessionIdentityWaitsForInputButNeverSubstitutesChoice()
    {
        var ranking = Ranking(
            Candidate(
                "skills:profession:0:5:1",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 1,
                score: 1,
                available: true),
            Candidate(
                "profession-input-settle",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.ProfessionInputSettleKind,
                rank: 2,
                score: 0.5,
                available: true));

        var selected = StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: true,
            preferredProfessionCandidateId: "skills:profession:0:5:0");

        Assert.NotNull(selected);
        Assert.Equal("profession-input-settle", selected.CandidateId);

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(
            Ranking(Candidate(
                "skills:profession:0:5:1",
                "skills.choose_profession",
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                rank: 1,
                score: 1,
                available: true)),
            professionChoiceRequired: true,
            preferredProfessionCandidateId: "skills:profession:0:5:0"));
    }

    [Fact]
    public void RequiredUnavailableProfessionChoiceFailsClosedWithoutGenericFallback()
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
                            CandidateId = "generic-close",
                            Kind = StageOneMandatoryMenuRecovery.CloseMenuKind,
                            Available = true
                        }
                    ]
                },
                new OptionAvailability
                {
                    OptionId = "skills.choose_profession",
                    EventCandidates =
                    [
                        new EventCandidate
                        {
                            CandidateId = "profession-blocked",
                            Kind = StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                            Available = false,
                            BlockReasons = ["profession_title_missing"]
                        }
                    ]
                }
            ]
        };
        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");

        Assert.DoesNotContain(
            ranking.RankedEventCandidates,
            candidate => candidate.Kind ==
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind);

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: true));
    }

    [Fact]
    public void OrdinaryLevelMenuBlockedProfessionPlaceholderAllowsGenericRecovery()
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
                            CandidateId = "generic-close",
                            Kind = StageOneMandatoryMenuRecovery.CloseMenuKind,
                            Available = true
                        }
                    ]
                },
                new OptionAvailability
                {
                    OptionId = "skills.choose_profession",
                    EventCandidates =
                    [
                        new EventCandidate
                        {
                            CandidateId = "profession-not-active",
                            Kind = StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                            Available = false,
                            BlockReasons =
                                ["level_up_menu_not_profession_chooser"]
                        }
                    ]
                }
            ]
        };
        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");

        var selected = StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: false);

        Assert.NotNull(selected);
        Assert.Equal("generic-close", selected.CandidateId);
        Assert.Equal("recovery.stabilize_day", selected.OptionId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void LevelUpMenuUsesFailClosedProfessionAuthority(
        bool? isProfessionChooser,
        bool expected)
    {
        var specific = isProfessionChooser.HasValue
            ? "{\"is_profession_chooser\":" +
                isProfessionChooser.Value.ToString().ToLowerInvariant() + "}"
            : "{}";
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>($$"""
        {
          "schema_version":"snapshot.v1",
          "state_hash":"profession-menu-authority",
          "state":{
            "menus":{
              "active_menu":{"value":{"is_open":true,"type":"LevelUpMenu"},"status":"available"},
              "menu_specific_state":{"value":{{specific}},"status":"available"}
            }
          }
        }
        """) ?? throw new InvalidDataException("Test snapshot is empty.");

        Assert.Equal(
            expected,
            StageOneMandatoryMenuRecovery.RequiresExplicitProfessionChoice(
                snapshot));
    }

    [Fact]
    public void MissingProfessionCandidateFailsClosedWhenMenuAuthorityRequiresIt()
    {
        var ranking = Ranking(
            Candidate(
                "generic-close",
                "recovery.stabilize_day",
                StageOneMandatoryMenuRecovery.CloseMenuKind,
                rank: 1,
                score: 1,
                available: true));

        Assert.Null(StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: true));
    }

    [Fact]
    public void ProfessionInputTimerCompilesBoundedWaitBeforeExactChoice()
    {
        var snapshot = ProfessionSnapshot(
            canReceiveInput: false,
            timerBeforeStart: 234);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            ["recovery.stabilize_day", "skills.choose_profession"],
            includeExecutorCalibrationOptions: true);
        var recovery = Assert.Single(
            availability.Options,
            option => option.OptionId == "recovery.stabilize_day");
        var settle = Assert.Single(
            recovery.EventCandidates,
            candidate => candidate.Kind ==
                StageOneMandatoryMenuRecovery.ProfessionInputSettleKind);
        Assert.True(settle.Available, string.Join(",", settle.BlockReasons));
        Assert.Equal("16", Assert.Single(
            settle.Parameters,
            parameter => parameter.Name == "wait_ticks").Value);

        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");
        var selected = StageOneMandatoryMenuRecovery.TrySelect(
            ranking,
            professionChoiceRequired: true);

        Assert.NotNull(selected);
        Assert.Equal(
            StageOneMandatoryMenuRecovery.ProfessionInputSettleKind,
            selected.Kind);
        var plan = new DailyPlanCompiler().Compile(
            [selected],
            snapshot.StateHash,
            "grandpa_evaluation_max_score",
            maxCandidates: 1);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        Assert.Equal("pending", item.Status);
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "profession_input_settle_choice_ids" &&
                parameter.Value == "0,1");
        Assert.Contains(
            item.NormalizedCommand.Parameters,
            parameter =>
                parameter.Name == "safety_constraint" &&
                parameter.Value ==
                    "bounded_native_level_up_menu_input_settle_wait");
    }

    [Fact]
    public void ProfessionInputSettleRequiresInformationPanelAlreadyOpen()
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            ProfessionSnapshot(
                canReceiveInput: false,
                timerBeforeStart: 234,
                informationUp: false),
            ["recovery.stabilize_day", "skills.choose_profession"],
            includeExecutorCalibrationOptions: true);
        var recovery = Assert.Single(
            availability.Options,
            option => option.OptionId == "recovery.stabilize_day");

        Assert.DoesNotContain(
            recovery.EventCandidates,
            candidate => candidate.Kind ==
                StageOneMandatoryMenuRecovery.ProfessionInputSettleKind);
    }

    [Fact]
    public void ProfessionInputSettleWaitPreemptsOrdinaryCollectionRanking()
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
                            CandidateId = "profession-input-settle",
                            Kind = StageOneMandatoryMenuRecovery
                                .ProfessionInputSettleKind,
                            Available = true
                        }
                    ]
                },
                new OptionAvailability
                {
                    OptionId = "fishing.catch_fish",
                    EventCandidates =
                    [
                        new EventCandidate
                        {
                            CandidateId = "ordinary-collection",
                            Kind = "catch_fish_current",
                            Available = true
                        }
                    ]
                }
            ]
        };

        var ranking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            "grandpa_evaluation_max_score");

        Assert.Contains(
            ranking.RankedEventCandidates,
            candidate => candidate.CandidateId ==
                "profession-input-settle");
        Assert.DoesNotContain(
            ranking.RankedEventCandidates,
            candidate => candidate.CandidateId == "ordinary-collection");
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
        var loopSource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs");
        var collectionSource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs");
        var recoverySource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneMandatoryMenuRecovery.cs");
        var preflightInvocation = loopSource.IndexOf(
            "TryBuildStageOneMandatoryMenuRecoveryQueueAsync(",
            StringComparison.Ordinal);
        var maintenance = loopSource.IndexOf(
            "StageOneDailyMaintenancePreemptionPolicy.Evaluate(",
            preflightInvocation,
            StringComparison.Ordinal);
        var collectionInvocation = loopSource.IndexOf(
            "TryBuildStageOneCollectionTeacherQueueAsync(",
            maintenance,
            StringComparison.Ordinal);
        var preflight = recoverySource.IndexOf(
            "StageOneMandatoryMenuRecovery.RequiresPreflight(typedSnapshot)",
            StringComparison.Ordinal);
        var selection = recoverySource.IndexOf(
            "StageOneMandatoryMenuRecovery.TrySelect(",
            preflight,
            StringComparison.Ordinal);
        var intentBuilder = collectionSource.IndexOf(
            "MasterAnglerTargetDateIntentBuilder.Build(",
            StringComparison.Ordinal);
        var continuationFilter = collectionSource.IndexOf(
            "activeObjectiveContinuation is not null",
            intentBuilder,
            StringComparison.Ordinal);
        var membership = collectionSource.IndexOf(
            "CurrentStageOneCollectionTeacherPreferenceBuilder.Build(",
            continuationFilter,
            StringComparison.Ordinal);

        Assert.True(preflightInvocation >= 0);
        Assert.True(maintenance > preflightInvocation);
        Assert.True(collectionInvocation > maintenance);
        Assert.True(preflight >= 0);
        Assert.True(selection > preflight);
        Assert.Contains(
            "StageOneMandatoryMenuRecovery.RequiresExplicitProfessionChoice(",
            recoverySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "blocked_mandatory_menu_recovery_unavailable",
            recoverySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryBuildStageOneMandatoryMenuRecoveryAsync(",
            collectionSource,
            StringComparison.Ordinal);
        Assert.True(intentBuilder >= 0);
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

    private static SnapshotEnvelope ProfessionSnapshot(
        bool canReceiveInput,
        int timerBeforeStart,
        bool informationUp = true)
    {
        var json = $$"""
        {
          "schema_version":"snapshot.v1",
          "state_hash":"profession-menu-settle-state",
          "state":{
            "time":{
              "time":{
                "value":610,
                "status":"available"
              }
            },
            "player":{
              "location_id":{"value":"FarmHouse","status":"available"},
              "tile_x":{"value":9,"status":"available"},
              "tile_y":{"value":9,"status":"available"},
              "energy":{"value":270,"status":"available"}
            },
            "current_location":{
              "home_context":{"value":{
                "home_available":true,
                "home_location_id":"FarmHouse",
                "current_location_id":"FarmHouse",
                "current_location_is_home":true,
                "bed_tile_x":3,
                "bed_tile_y":8,
                "bed_tile_has_bed":true
              },"status":"available"}
            },
            "menus":{
              "active_menu":{"value":{
                "is_open":true,
                "type":"LevelUpMenu",
                "full_type":"StardewValley.Menus.LevelUpMenu",
                "is_sleep_prompt":false
              },"status":"available"},
              "menu_specific_state":{"value":{
                "kind":"level_up",
                "information_up":{{informationUp.ToString().ToLowerInvariant()}},
                "is_active":true,
                "is_profession_chooser":true,
                "has_updated_professions":true,
                "can_receive_input":{{canReceiveInput.ToString().ToLowerInvariant()}},
                "current_skill":0,
                "current_level":5,
                "timer_before_start":{{timerBeforeStart}},
                "reflection_fields_complete":true,
                "profession_choices":[
                  {"profession_id":0,"title":"Rancher","description_lines":["animal bonus"]},
                  {"profession_id":1,"title":"Tiller","description_lines":["crop bonus"]}
                ]
              },"status":"available"},
              "sleep_prompt_context":{"value":{
                "prompt_open":false
              },"status":"available"}
            },
            "locations":{
              "collision_grid":{"value":{
                "location_id":"FarmHouse",
                "width":12,
                "height":12,
                "notable_tiles":[]
              },"status":"available"},
              "route_action_branch_coverage":{"value":{
                "rows":[]
              },"status":"available"}
            }
          }
        }
        """;
        return JsonSerializer.Deserialize<SnapshotEnvelope>(json) ??
            throw new InvalidDataException("Test snapshot is empty.");
    }

    private static SnapshotEnvelope ExactSleepPromptSnapshot()
    {
        const string json = """
        {
          "schema_version":"snapshot.v1",
          "state_hash":"exact-sleep-prompt-preflight",
          "state":{
            "time":{
              "time":{"value":600,"status":"available"}
            },
            "player":{
              "location_id":{"value":"FarmHouse","status":"available"},
              "tile_x":{"value":3,"status":"available"},
              "tile_y":{"value":9,"status":"available"},
              "current_item_qualified_id":{"value":"","status":"available"},
              "active_object_qualified_id":{"value":"","status":"available"},
              "energy":{"value":270,"status":"available"}
            },
            "current_location":{
              "home_context":{"value":{
                "home_available":true,
                "home_location_id":"FarmHouse",
                "current_location_id":"FarmHouse",
                "current_location_is_home":true,
                "bed_tile_x":3,
                "bed_tile_y":8,
                "bed_tile_has_bed":true,
                "sleep_executor_enabled":true
              },"status":"available"}
            },
            "menus":{
              "active_menu":{"value":{
                "is_open":true,
                "type":"DialogueBox",
                "last_question_key":"Sleep",
                "is_sleep_prompt":true,
                "event_up":false
              },"status":"available"},
              "menu_specific_state":{"value":{"kind":"none"},"status":"available"},
              "sleep_prompt_context":{"value":{
                "prompt_open":true,
                "active_menu_open":true,
                "active_menu_type":"DialogueBox",
                "last_question_key":"Sleep",
                "can_confirm_sleep":true,
                "confirm_executor_enabled":true,
                "confirm_action_key":"Sleep_Yes"
              },"status":"available"}
            },
            "locations":{
              "collision_grid":{"value":{
                "location_id":"FarmHouse",
                "width":12,
                "height":12,
                "notable_tiles":[]
              },"status":"available"},
              "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
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
