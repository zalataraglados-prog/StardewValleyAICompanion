using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeCropHarvestSkillBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void MissingWateringCanStillProjectsThePlayersFarmingLevel(int experience)
    {
        var f = MissingCanFixture(experience);
        _ = Read(f.Request, f.Before, f.After);
    }

    [Theory]
    [InlineData("level")]
    [InlineData("slot")]
    [InlineData("cost")]
    [InlineData("bowl")]
    public void MissingWateringCanRejectsContradictoryToolOrUnrelatedBowlState(string change)
    {
        var f = MissingCanFixture(0);
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            if (change == "level") Edit(snapshot, "player", "watering_can_state", row => row["farming_level"] = 1);
            if (change == "slot") Edit(snapshot, "player", "watering_can_state", row => row["slot_index"] = 1);
            if (change == "cost") Edit(snapshot, "player", "watering_can_state", row => row["energy_cost_per_power_zero_action"] = 99d);
        }
        if (change == "bowl") Edit(f.After, "farm", "pet_bowls", rows => rows[0]!["future_persistent"] = "changed");
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State); f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        f.Request.BeforeStateHash = f.Before.StateHash;
        Assert.Throws<InvalidDataException>(() => Read(f.Request, f.Before, f.After));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(90, true)]
    [InlineData(90, false)]
    public void CropHarvestSkillConsumesOriginalTargetProjectionAndDerivedLevelMirrors(int experience, bool mirrors)
    {
        var (request, before, after) = Fixture(experience, mirrors);
        var first = JsonSerializer.Serialize(before); var last = JsonSerializer.Serialize(after);
        var result = Read(request, before, after);
        Assert.Equal(experience, result.Before.Skills.Single(row => row.SkillId == "farming").Experience);
        Assert.Equal(experience + 23, result.After.Skills.Single(row => row.SkillId == "farming").Experience);
        Assert.Equal(first, JsonSerializer.Serialize(before));
        Assert.Equal(last, JsonSerializer.Serialize(after));
        Assert.Equal(before.StateHash, SnapshotHash.ComputeStateHash(before.State));
        Assert.Equal(after.StateHash, SnapshotHash.ComputeStateHash(after.State));
    }

    [Theory]
    [InlineData("request-source")]
    [InlineData("foreign-save")]
    [InlineData("wrong-location")]
    [InlineData("wrong-target")]
    [InlineData("not-ready")]
    [InlineData("dead")]
    [InlineData("duplicate-target")]
    [InlineData("inexact-xp")]
    [InlineData("short-status")]
    [InlineData("xp-range")]
    [InlineData("wrong-xp")]
    [InlineData("wrong-skill-index")]
    [InlineData("can-before-level")]
    [InlineData("can-before-cost")]
    [InlineData("can-after-cost")]
    [InlineData("can-other-state")]
    [InlineData("bowl-other-state")]
    [InlineData("mastery-before-fingerprint")]
    [InlineData("mastery-after-fingerprint")]
    [InlineData("mastery-other-state")]
    public void CropHarvestSkillRejectsUnboundXpOrUnexplainedMirrorChanges(string change)
    {
        var (request, before, after) = Fixture(90);
        switch (change)
        {
            case "request-source": request.BeforeStateHash = "foreign"; break;
            case "foreign-save": after.SaveId.Value = "foreign"; break;
            case "wrong-location": request.LocationId = "Town"; break;
            case "wrong-target": request.TargetTileX = 99; break;
            case "not-ready": Crop(row => row["ready_for_harvest"] = false); break;
            case "dead": Crop(row => row["dead"] = true); break;
            case "duplicate-target": Edit(before, "farm", "crops", rows => rows.AsArray().Add(rows[0]!.DeepClone())); break;
            case "inexact-xp": Crop(row => row["harvest_experience_projection_status"] = "unavailable"); break;
            case "short-status": Crop(row => row["harvest_experience_projection_status"] = "exact"); break;
            case "xp-range": Crop(row => row["harvest_experience_on_success_max"] = 24); break;
            case "wrong-xp": Crop(row => { row["harvest_experience_on_success_min"] = 24; row["harvest_experience_on_success_max"] = 24; }); break;
            case "wrong-skill-index": Crop(row => row["harvest_experience_skill_index"] = 2); break;
            case "can-before-level": Edit(before, "player", "watering_can_state", row => row["farming_level"] = 2); break;
            case "can-before-cost": Edit(before, "player", "watering_can_state", row => row["energy_cost_per_power_zero_action"] = 99d); break;
            case "can-after-cost": Edit(after, "player", "watering_can_state", row => row["energy_cost_per_power_zero_action"] = 99d); break;
            case "can-other-state": Edit(after, "player", "watering_can_state", row => row["water_left"] = 9); break;
            case "bowl-other-state": Edit(after, "farm", "pet_bowls", rows => rows[0]!["future_persistent"] = "changed"); break;
            case "mastery-before-fingerprint": Edit(before, "player", "mastery_claim", row => row["projection_fingerprint"] = "forged"); break;
            case "mastery-after-fingerprint": Edit(after, "player", "mastery_claim", row => row["projection_fingerprint"] = "forged"); break;
            case "mastery-other-state": Edit(after, "player", "mastery_claim", row => row["mastery_experience"] = 1); break;
        }
        before.StateHash = SnapshotHash.ComputeStateHash(before.State); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        if (change != "request-source") request.BeforeStateHash = before.StateHash;
        Assert.Throws<InvalidDataException>(() => Read(request, before, after));
        void Crop(Action<JsonNode> edit) => Edit(before, "farm", "crops", rows => edit(rows[0]!));
    }

    private static (CurrentSkillExperienceDeficit Before, CurrentSkillExperienceDeficit After) Read(TrainingExecutionRequest request,
        SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { return ((CurrentSkillExperienceDeficit, CurrentSkillExperienceDeficit))type.GetMethod("VerifyCropHarvestSkillEffects",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [before, after, request])!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    internal static (TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(int experience, bool mirrors = true)
    {
        var before = NativeWildTreeClearanceBoundaryTests.Fixture(0).Before;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!;
        after.GameTick += 2;
        var names = new[] { "farming", "fishing", "foraging", "mining", "combat", "luck" };
        int Level(int xp) => Enumerable.Range(1, 10).Count(level => NativeSkillExperienceRules.ExperienceForLevel(level) <= xp);
        foreach (var snapshot in new[] { before, after })
        {
            var isAfter = ReferenceEquals(snapshot, after);
            var xp = experience + (isAfter ? 23 : 0); var level = Level(xp);
            var skills = Enumerable.Range(0, 6).Select(index =>
            {
                var current = index == 0 ? level : 0; var amount = index == 0 ? xp : 0;
                int? next = current < 10 ? current + 1 : null;
                int? threshold = next.HasValue ? NativeSkillExperienceRules.ExperienceForLevel(next.Value) : null;
                return new { skill_index = index, skill_id = names[index], unmodified_level = current, effective_level = current,
                    temporary_buff_delta = 0, experience = amount, next_level = next, next_level_experience = threshold,
                    experience_to_next_level = threshold - amount, at_vanilla_level_cap = current == 10 };
            }).ToArray();
            var pending = new JsonArray();
            if (isAfter) for (var value = Level(experience) + 1; value <= level; value++)
                pending.Add(new JsonObject { ["skill_index"] = 0, ["level"] = value });
            Put(snapshot, "player", "skills_detail", new JsonObject { ["scoring_level"] = level / 2, ["vanilla_level_cap"] = 10,
                ["skills"] = JsonSerializer.SerializeToNode(skills), ["new_levels"] = pending, ["profession_ids"] = new JsonArray() });
            Put(snapshot, "player", "level", JsonValue.Create(level / 2)!);
            Put(snapshot, "farm", "crops", new JsonArray(new JsonObject { ["location_id"] = "Farm", ["tile_x"] = 10, ["tile_y"] = 11,
                ["ready_for_harvest"] = true, ["dead"] = false, ["harvest_experience_skill_id"] = "farming", ["harvest_experience_skill_index"] = 0,
                ["harvest_experience_on_success_min"] = 23, ["harvest_experience_on_success_max"] = 23,
                ["harvest_experience_projection_status"] = "exact_from_decompiled_native_harvest", ["harvest_experience_condition"] = "native_player_crop_harvest_from_harvest_item_base_price" }));
            if (!mirrors) continue;
            var cost = Math.Max(0d, 2d - level * 0.1d);
            var energy = snapshot.State["player"].GetProperty("energy").GetProperty("value").GetSingle();
            Put(snapshot, "player", "watering_can_state", new JsonObject { ["projection_status"] = "exact_vanilla_power_zero", ["slot_index"] = 1,
                ["runtime_type"] = "StardewValley.Tools.WateringCan", ["upgrade_level"] = 0, ["water_left"] = 10, ["capacity"] = 40,
                ["is_bottomless"] = false, ["is_efficient"] = false, ["farmer_has_watering_can_enchantment"] = false,
                ["farming_level"] = level, ["energy_cost_per_power_zero_action"] = cost });
            Put(snapshot, "farm", "pet_bowls", new JsonArray(new JsonObject { ["watering_can_slot_index"] = 1,
                ["watering_can_runtime_type"] = "StardewValley.Tools.WateringCan", ["watering_can_upgrade_level"] = 0,
                ["watering_can_bottomless"] = false, ["farmer_has_watering_can_enchantment"] = false, ["watering_can_efficient"] = false,
                ["watering_can_water_left"] = 10, ["expected_watering_can_water_after"] = 9, ["energy_before"] = energy,
                ["watering_energy_cost"] = cost, ["expected_energy_after"] = energy - cost, ["energy_delta_projection_status"] = "exact_vanilla_power_zero",
                ["friendship_before_next_day"] = 0, ["friendship_after_fill_and_next_day_update"] = 6, ["action_status"] = "ready", ["future_persistent"] = "retained" }));
            var options = Enumerable.Range(0, 5).Select(index => new MasteryClaimOptionRef { SkillId = index, SkillKey = names[index],
                SkillLevel = index == 0 ? level : 0, MasteryStatKey = "declared." + index }).ToArray();
            foreach (var option in options) option.OptionFingerprint = MasteryClaimIdentity.ComputeOptionFingerprint(option);
            var mastery = new MasteryClaimProjectionRef { ProjectionStatus = "complete_locked_base_1.6.15", Skills = options, PlayerId = 123 };
            mastery.ProjectionFingerprint = MasteryClaimIdentity.ComputeProjectionFingerprint(mastery);
            Put(snapshot, "player", "mastery_claim", JsonSerializer.SerializeToNode(mastery)!);
        }
        before.StateHash = SnapshotHash.ComputeStateHash(before.State); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return (new TrainingExecutionRequest { OptionId = "executor.harvest_crop", LocationId = "Farm", TargetTileX = 10,
            TargetTileY = 11, BeforeStateHash = before.StateHash }, before, after);
    }

    private static (TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After) MissingCanFixture(int experience)
    {
        var f = Fixture(experience);
        foreach (var snapshot in new[] { f.Before, f.After })
        {
            var level = snapshot.State["player"].GetProperty("watering_can_state").GetProperty("value").GetProperty("farming_level").GetInt32();
            Put(snapshot, "player", "watering_can_state", new JsonObject { ["projection_status"] = "watering_can_missing", ["slot_index"] = null,
                ["runtime_type"] = "", ["upgrade_level"] = null, ["water_left"] = null, ["capacity"] = null, ["is_bottomless"] = null,
                ["is_efficient"] = null, ["farmer_has_watering_can_enchantment"] = false, ["farming_level"] = level,
                ["energy_cost_per_power_zero_action"] = null });
            Edit(snapshot, "farm", "pet_bowls", rows =>
            {
                var row = rows[0]!;
                foreach (var name in new[] { "watering_can_slot_index", "watering_can_upgrade_level", "watering_can_water_left", "watering_can_bottomless",
                    "watering_can_efficient", "expected_watering_can_water_after", "watering_energy_cost", "expected_energy_after" }) row[name] = null;
                row["watering_can_runtime_type"] = ""; row["energy_delta_projection_status"] = "watering_can_unavailable";
                row["action_status"] = "watering_can_missing";
            });
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        f.Request.BeforeStateHash = f.Before.StateHash;
        return f;
    }

    private static void Put(SnapshotEnvelope snapshot, string domain, string name, JsonNode value)
    {
        var fields = snapshot.State.TryGetValue(domain, out var source) ? JsonNode.Parse(source.GetRawText())!.AsObject() : new JsonObject();
        fields[name] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
    }

    private static void Edit(SnapshotEnvelope snapshot, string domain, string name, Action<JsonNode> edit)
    {
        var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
        edit(fields[name]!["value"]!); snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
    }
}
