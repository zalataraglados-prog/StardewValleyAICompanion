using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void FriendshipTeacherReusesControlAndOriginalCompilersWithFullNeutralDenominator()
    {
        var files = FriendshipTeacherFiles();
        var before = File.ReadAllText(files.Snapshot);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.TeacherPreferenceLabelEligible, JsonSerializer.Serialize(result));
        Assert.True(result.IndependentPairwiseLabelEligible);
        Assert.Equal(new[] { "social.gift_npc", "social.talk_npc" }, result.ConsideredOptionIds);
        Assert.Equal(result.OriginalTeacher.SelectedCandidateId, result.SelectedCandidate!.CandidateId);
        Assert.True(result.OriginalTeacher.Itinerary.TrainingLabelEligible);
        Assert.False(result.OriginalTeacher.TrainingLabelEligible);
        Assert.Contains(result.SelectedCandidate.Parameters,
            parameter => parameter.Name == "friendship_current_admission_state_hash" && parameter.Value == result.Deficit.SourceStateHash);
        Assert.Single(result.PairwisePreferences);
        Assert.Empty(result.UncomparedCandidateIds);
        Assert.Equal(2, result.Candidates.Count(row => row.Status == "admitted_current_social_comparison"));
        Assert.All(result.DecisionCandidates, row =>
        {
            Assert.Equal(0, row.Score);
            Assert.Null(row.ModelScore);
            Assert.Equal(0, row.ExpectedReward);
            Assert.Equal(1, row.Rank);
        });
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FullCriterionVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(before, File.ReadAllText(files.Snapshot));
        var stored = Path.Combine(files.Directory, "preference.json");
        File.WriteAllText(stored, JsonSerializer.Serialize(result, JsonOptions));
        var verified = CurrentFriendshipTeacherPreferenceBuilder.Verify(stored);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(verified));
        var dispatch = FriendshipTeacherDispatchBuilder.Compile(stored);
        Assert.Equal("pending", dispatch.Queue.Status);
        Assert.All(dispatch.Queue.Items, item => Assert.Equal("pending", item.Status));
        Assert.Contains(dispatch.Queue.Items, item => item.OptionId == "executor.social_interact");
    }

    [Fact]
    public void FriendshipTeacherKeepsGiftTradeoffsAndDoesNotPromoteControlOnlyLabels()
    {
        var files = FriendshipTeacherFiles(gifts: true);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.OriginalTeacher.Itinerary.TrainingLabelEligible, JsonSerializer.Serialize(result));
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.True(result.IndependentPairwiseLabelEligible);
        Assert.Contains(result.Candidates, row => row.OptionId == "social.gift_npc" && row.ComparisonRequired);
        Assert.NotEmpty(result.UncomparedCandidateIds);
        Assert.DoesNotContain(result.PairwisePreferences, pair => result.Candidates
            .Single(row => row.CandidateId == pair.AlternativeCandidateId).OptionId == "social.gift_npc");
    }

    [Fact]
    public void FriendshipTeacherVerifyRejectsEditedComparisonAndSourceDrift()
    {
        var files = FriendshipTeacherFiles();
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.TeacherPreferenceLabelEligible, JsonSerializer.Serialize(result));
        var stored = Path.Combine(files.Directory, "edited.json");
        var json = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        json["pairwisePreferences"]![0]!["alternativeCandidateId"] = result.SelectedCandidate!.CandidateId;
        File.WriteAllText(stored, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentFriendshipTeacherPreferenceBuilder.Verify(stored));
        File.WriteAllText(stored, JsonSerializer.Serialize(result, JsonOptions));
        File.AppendAllText(files.Calibration, " ");
        Assert.Throws<InvalidDataException>(() => CurrentFriendshipTeacherPreferenceBuilder.Verify(stored));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(9)]
    public void FriendshipTeacherForecastsBothNativeCountThresholdsWithoutClaimingOutcomes(int qualifying)
    {
        var files = FriendshipTeacherFiles(qualifying: qualifying);
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.TeacherPreferenceLabelEligible, JsonSerializer.Serialize(result));
        Assert.Equal(qualifying, result.Deficit.QualifyingVillagerCount);
        Assert.Equal(1, result.Candidates.Single(row => row.CandidateId == result.SelectedCandidate!.CandidateId).ExpectedNewQualifiers);
        Assert.False(result.NativeOutcomeVerified);
    }

    [Theory]
    [InlineData("tie")]
    [InlineData("single")]
    [InlineData("unknown_energy")]
    public void FriendshipTeacherDoesNotInventPreferencesWhenTheOriginalControlCanStillRun(string mode)
    {
        var files = FriendshipTeacherFiles();
        var source = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var state = source["state"]!;
        if (mode == "tie")
        {
            state["npcs"]!["social_interaction"]!["value"]![1]!["tile_x"] = 6;
            state["npcs"]!["positions"]!["value"]![1]!["tile_x"] = 6;
            state["npcs"]!["schedule_catalog"]!["value"]!["villagers"]![1]!["default_tile_x"] = 6;
        }
        else if (mode == "single")
        {
            var npc = state["npcs"]!["social_interaction"]!["value"]![1]!;
            npc["talked_to_today"] = true;
            npc["expected_talk_friendship_delta"] = 0;
            npc["expected_talk_friendship_points_after"] = 1960;
            state["npcs"]!["friendships"]!["value"]![1]!["talked_to_today"] = true;
            state["npcs"]!["schedule_catalog"]!["value"]!["villagers"]![1]!["talked_to_today"] = true;
        }
        else
            state["player"]!["energy"]!["status"] = "unknown";
        var snapshot = source.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.Empty(result.PairwisePreferences);
        Assert.False(result.IndependentPairwiseLabelEligible);
        var path = Path.Combine(files.Directory, "control-only.json");
        File.WriteAllText(path, JsonSerializer.Serialize(result, JsonOptions));
        Assert.Throws<InvalidDataException>(() => FriendshipTeacherDispatchBuilder.Compile(path));
        Assert.Throws<InvalidDataException>(() => FriendshipTeacherDispatchBuilder.Compile(path, FriendshipTeacherAdmissionMode.IndependentPairs));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    public void FriendshipTeacherUsesFullOfflineSourceWhileRuntimeKeepsItsOriginalWindow(int targets)
    {
        var files = FriendshipTeacherFiles(targets: targets);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), JsonOptions)!;
        var runtime = new CandidateOptionAvailabilityEvaluator();
        var injectedRuntime = new CandidateOptionAvailabilityEvaluator(
            new StardewAI.Core.OptionRegistry.OptionRegistry(), new StardewAI.Core.Verifier.Verifier());
        var complete = CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison();
        Assert.Equal(64, runtime.SocialCandidateLimit);
        Assert.Equal(64, injectedRuntime.SocialCandidateLimit);
        Assert.Null(complete.SocialCandidateLimit);
        var optionIds = new[] { "social.talk_npc" };
        var bounded = Assert.Single(runtime.Evaluate(snapshot, optionIds).Options).SocialCandidates;
        Assert.Equal(64, bounded.Length);
        Assert.Equal(bounded.Select(row => row.CandidateId), Assert.Single(injectedRuntime.Evaluate(snapshot, optionIds).Options)
            .SocialCandidates.Select(row => row.CandidateId));
        var full = Assert.Single(complete.Evaluate(snapshot, optionIds).Options).SocialCandidates;
        Assert.Equal(targets, full.Length);
        Assert.Equal(bounded.Select(row => row.CandidateId), full.Take(64).Select(row => row.CandidateId));
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.CandidateDenominatorComplete, string.Join(",", result.BlockingReasons));
        var audit = result.OptionAudit.Single(option => option.OptionId == "social.talk_npc");
        Assert.Equal(targets, audit.SourceSocialCandidateCount);
        Assert.Null(audit.AppliedSocialCandidateLimit);
        Assert.False(audit.AtLegacySocialCandidateLimit);
        Assert.Equal(targets, result.DecisionCandidates.Length);
        Assert.Equal(63, result.PairwisePreferences.Length);
        if (targets == 64)
        {
            Assert.True(result.TeacherPreferenceLabelEligible, string.Join(",", result.BlockingReasons));
            Assert.Empty(result.UncomparedCandidateIds);
        }
        else
        {
            // The old compiler's immediate matcher has its own bounded source; do not bypass it.
            Assert.False(result.TeacherPreferenceLabelEligible);
            var unmatched = Assert.Single(result.UncomparedCandidateIds);
            var row = result.Candidates.Single(candidate => candidate.CandidateId == unmatched);
            Assert.Equal("excluded", row.Status);
            Assert.Contains("social_current_state_candidate_not_available_for_executor", row.Reasons);
        }
        Assert.False(result.NativeOutcomeVerified);
        Assert.Equal(64, Assert.Single(runtime.Evaluate(snapshot, optionIds).Options).SocialCandidates.Length);
    }

    [Theory]
    [InlineData("current_friendship_teacher_preference.v1")]
    [InlineData("current_friendship_teacher_preference.v2")]
    [InlineData("current_friendship_teacher_preference.v3")]
    public void FriendshipTeacherRejectsLegacyArtifactsAndEditedIntakeClaims(string legacySchema)
    {
        var files = FriendshipTeacherFiles();
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        var stored = Path.Combine(files.Directory, "intake-edited.json");
        var json = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        json["schemaVersion"] = legacySchema;
        File.WriteAllText(stored, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentFriendshipTeacherPreferenceBuilder.Verify(stored));
        json["schemaVersion"] = result.SchemaVersion;
        json["optionAudit"]![0]!["appliedSocialCandidateLimit"] = 64;
        File.WriteAllText(stored, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentFriendshipTeacherPreferenceBuilder.Verify(stored));
    }

    [Fact]
    public void FriendshipTeacherAuditsDuplicateSocialSourceIdsBeforeOriginalRankerDeduplication()
    {
        var files = FriendshipTeacherFiles();
        var source = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var rows = source["state"]!["npcs"]!["social_interaction"]!["value"]!.AsArray();
        rows.Add(rows[0]!.DeepClone());
        var snapshot = source.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.False(result.CandidateDenominatorComplete);
        Assert.False(result.TeacherPreferenceLabelEligible);
        Assert.Contains("social:talk:Abigail", result.OptionAudit.Single(option => option.OptionId == "social.talk_npc").DuplicateSourceCandidateIds);
        Assert.Contains("social:talk:Abigail", result.OptionAudit.Single(option => option.OptionId == "social.talk_npc").BlockingDuplicateSourceCandidateIds);
        Assert.Empty(result.PairwisePreferences);
    }

    [Fact]
    public void FriendshipTeacherRetainsEveryKnownUnavailableDuplicateWithoutBlockingUnrelatedTargets()
    {
        var files = FriendshipTeacherFiles();
        var source = JsonNode.Parse(File.ReadAllText(files.Snapshot))!;
        var rows = source["state"]!["npcs"]!["social_interaction"]!["value"]!.AsArray();
        var inactive = rows[0]!.DeepClone();
        inactive["name"] = "Inactive";
        inactive["can_socialize"] = false;
        inactive["can_receive_gifts"] = false;
        rows.Add(inactive);
        rows.Add(inactive.DeepClone());
        var snapshot = source.Deserialize<SnapshotEnvelope>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        File.WriteAllText(files.Snapshot, JsonSerializer.Serialize(snapshot, JsonOptions));
        var result = CurrentFriendshipTeacherPreferenceBuilder.Build(files.Snapshot, files.Calibration);
        Assert.True(result.CandidateDenominatorComplete, string.Join(",", result.BlockingReasons));
        var audit = result.OptionAudit.Single(option => option.OptionId == "social.talk_npc");
        Assert.Contains("social:talk:Inactive", audit.DuplicateSourceCandidateIds);
        Assert.Empty(audit.BlockingDuplicateSourceCandidateIds);
        Assert.Equal(4, audit.SourceSocialCandidateCount);
        Assert.Equal(2, audit.UnavailableDuplicateSourceOccurrences.Length);
        Assert.All(audit.UnavailableDuplicateSourceOccurrences, row =>
        {
            Assert.False(row.Available);
            Assert.NotEmpty(row.BlockReasons);
            Assert.NotEqual(true, row.AllowedNow);
            Assert.NotEqual(true, row.AllowedToday);
        });
        Assert.DoesNotContain(result.PairwisePreferences, pair => pair.AlternativeCandidateId == "social:talk:Inactive");
        Assert.False(result.NativeOutcomeVerified);
    }

    private static (string Snapshot, string Calibration, string Directory) FriendshipTeacherFiles(bool gifts = false, int qualifying = 0, int targets = 2)
    {
        var snapshot = CompleteSocialSnapshot(inventoryValue: gifts ? null : "[]");
        snapshot.GameVersion = "1.6.15";
        JsonNode Section(string name) => JsonNode.Parse(snapshot.State[name].GetRawText())!;
        static JsonNode Field(object value) => JsonSerializer.SerializeToNode(new
        {
            status = "available", value, confidence = 1, adapter = "test", read_at_tick = 1,
            source = new { kind = "test", path = "test" }
        })!;
        var player = Section("player");
        player["total_money_earned"] = Field(0);
        player["has_skull_key"] = Field(false);
        player["has_rusty_key"] = Field(false);
        player["level"] = Field(0);
        player["active_object_qualified_id"] = Field("");
        player["movement_timing_context"] = JsonNode.Parse(FutureRouteDateEvidenceProducerTests.MovementTimingContext().GetRawText());
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var time = Section("time");
        time["total_days"] = Field(412);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(new
        {
            achievements = Field(Array.Empty<int>()),
            community_center = Field(new { location_accessible = false, completed = false })
        });
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { grandpa_score = Field(0) });
        snapshot.State["quests"] = JsonSerializer.SerializeToElement(new { mail_received = Field(Array.Empty<string>()) });
        snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new
        { map = Field(new { location_id = "Town", width = 20, height = 20 }) });
        var npcs = Section("npcs");
        var original = npcs["social_interaction"]!["value"]![0]!.DeepClone();
        var names = Enumerable.Range(0, targets).Select(index => index == 0 ? "Abigail" : index == 1 ? "George" : "Target" + index).ToArray();
        npcs["social_interaction"]!["value"] = new JsonArray(names.Select((name, index) =>
        {
            var npc = original.DeepClone();
            npc["name"] = name; npc["display_name"] = name;
            npc["tile_x"] = index == 0 ? 10 : 16; npc["friendship_points"] = 1960;
            npc["expected_talk_friendship_points_after"] = 1980; npc["schedule_loaded"] = false;
            return npc;
        }).ToArray());
        npcs["friendships"] = Field(names.Select(name => new
        {
            npc_name = name, points = 1960, gifts_today = 0, gifts_this_week = 0,
            talked_to_today = false, is_divorced = false
        }).ToArray());
        npcs["schedules"] = Field(names.Select(name => new
        { name, follow_schedule = false, schedule_loaded = false, entries = Array.Empty<object>() }).ToArray());
        npcs["positions"] = Field(names.Select((name, index) => new
        { name, location_id = "Town", tile_x = index == 0 ? 10 : 16, tile_y = 10 }).ToArray());
        npcs["schedule_catalog"] = Field(new
        {
            current_selection_context = new { capture_total_days = 412, year = 2, is_green_rain = false },
            villagers = names.Select((name, index) => new
            {
                npc_name = name, is_villager = true, event_actor = false, is_player_spouse = false,
                currently_married = false, master_schedule_present = false, master_schedule_entry_count = 0,
                current_location_name = "Town", default_map = "Town", default_tile_x = index == 0 ? 10 : 16, default_tile_y = 10,
                vanilla_social_query_supported = true, character_master_data_present = true,
                can_socialize_now = true, can_receive_gifts_now = true, can_socialize_condition = "",
                simple_non_villager_npc = false, gift_taste_master_data_present = true, is_child = false,
                is_birthday_on_capture_date = false, friendship_row_exists = true, talked_to_today = false,
                friendship_is_divorced = false, can_receive_gifts_data = true, gifts_today = 0, gifts_this_week = 0
            }).ToArray()
        });
        npcs["grandpa_friendship_progress"] = Field(new
        {
            threshold_points = 1975, maximum_points = 999999, romance_only = false,
            projection_status = "complete_live_native_iteration", eligible_villager_count = targets + qualifying, qualifying_count = qualifying,
            eligible_villager_rows = names.Select((name, index) => new
            {
                native_iteration_index = index, npc_name = name, is_villager = true, event_actor = false,
                friendship_row_exists = true, friendship_points = 1960, qualifies = false
            }).Concat(Enumerable.Range(0, qualifying).Select(index => new
            {
                native_iteration_index = targets + index, npc_name = "AlreadyQualified" + index, is_villager = true, event_actor = false,
                friendship_row_exists = true, friendship_points = 1975, qualifies = true
            })).ToArray()
        });
        npcs["gift_tastes"] = Field(names.Select(name => new
        {
            npc_name = name, slot_index = 0, qualified_item_id = "(O)66", quality = 0, taste = "love",
            expected_friendship_delta = "80", expected_friendship_delta_complete = true, complete = true
        }).ToArray());
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(npcs);
        var locations = Section("locations");
        locations["social_route_date_evidence"] = Field(new
        {
            schema_version = "social_route_date_evidence.v2", capture_total_days = 412,
            all_location_static_walkability_complete = true,
            projection_status = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            location_count = 1, locations = new[] { new
            {
                location_id = "Town", map_width = 20, map_height = 20,
                projection_status = "exact_current_date_static_native_walkability", static_walkable_tile_count = 400,
                static_walkable_tile_ranges = Enumerable.Range(0, 20).Select(y => new { y, start_x = 0, end_x = 19 }).ToArray(),
                build_conditions = (string?)null, build_conditions_met = (bool?)null,
                unsupported_route_action_record_count = 0, unsupported_route_action_tile_count = 0,
                unsupported_route_action_tiles = Array.Empty<object>(), action_gates = Array.Empty<object>()
            } }
        });
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var directory = Path.Combine(Path.GetTempPath(), "stardewai-friendship-teacher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory, "snapshot.json");
        var calibration = Path.Combine(directory, "calibration.json");
        File.WriteAllText(source, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.WriteAllText(calibration, FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson());
        return (source, calibration, directory);
    }
}
