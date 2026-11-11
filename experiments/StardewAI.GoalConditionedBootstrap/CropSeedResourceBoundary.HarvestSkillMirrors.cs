using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void VerifyHarvestSkillMirrors(SnapshotEnvelope before, SnapshotEnvelope after,
        CurrentSkillExperienceDeficit first, CurrentSkillExperienceDeficit next)
    {
        var can = Field(before, "player", "watering_can_state", required: false);
        var actualCan = Field(after, "player", "watering_can_state", required: false);
        var canValue = Readable(can) ? Value(can!) as JsonObject : null;
        var projectedCan = canValue?["projection_status"]?.GetValue<string>() == "exact_vanilla_power_zero";
        if (projectedCan)
        {
            var value = Value(can!);
            var prior = first.Skills.Single(row => row.SkillId == "farming").EffectiveLevel;
            var level = next.Skills.Single(row => row.SkillId == "farming").EffectiveLevel;
            var efficient = HarvestMirrorBoolean(value, "is_efficient");
            double Cost(int farming) => efficient ? 0d : Math.Max(0d, 2d - farming * 0.1d);
            Require(Int(value, "farming_level") == prior && value["energy_cost_per_power_zero_action"] is JsonValue cost &&
                    cost.TryGetValue<double>(out var number) && double.IsFinite(number) && number == Cost(prior),
                "Crop harvest original watering-can level/cost mirror is inconsistent.");
            // This is PlayerReadAdapter's double/clamped display projection, not a native stamina debit.
            value["farming_level"] = level;
            value["energy_cost_per_power_zero_action"] = Cost(level);
        }
        EqualHarvestResourceField("player", "watering_can_state", can, actualCan);
        if (projectedCan) _ = VerifyWateringBowlMirrors(before, after);
        else EqualHarvestResourceField("farm", "pet_bowls", Field(before, "farm", "pet_bowls", required: false),
            Field(after, "farm", "pet_bowls", required: false));
        VerifyHarvestMasteryMirror(before, after, first, next);
    }

    private static void VerifyHarvestMasteryMirror(SnapshotEnvelope before, SnapshotEnvelope after,
        CurrentSkillExperienceDeficit first, CurrentSkillExperienceDeficit next)
    {
        var expected = Field(before, "player", "mastery_claim", required: false);
        var actual = Field(after, "player", "mastery_claim", required: false);
        if (!Readable(expected) || !Readable(actual))
        {
            EqualHarvestResourceField("player", "mastery_claim", expected, actual);
            return;
        }
        Require(Value(expected!) is JsonObject, "Crop harvest mastery projection is not an object.");
        var value = (JsonObject)Value(expected!);
        var projection = value.Deserialize<MasteryClaimProjectionRef>() ?? throw new InvalidDataException("Crop harvest mastery projection is invalid.");
        var rows = Array(value["skills"], "crop harvest mastery skills");
        var firstAll = first.Skills.Sum(row => row.UnmodifiedLevel / 10) >= 5;
        var nextAll = next.Skills.Sum(row => row.UnmodifiedLevel / 10) >= 5;
        Require(projection.SchemaVersion == "mastery_claim.v1" && projection.ProjectionStatus == "complete_locked_base_1.6.15" &&
                rows.Count == 5 && rows.OfType<JsonObject>().Select(row => Int(row, "skill_id")).Order().SequenceEqual(Enumerable.Range(0, 5)) &&
                projection.AllBaseSkillsLevelTen == firstAll && projection.UnspentMasteryLevels == Math.Max(0,
                    projection.CurrentMasteryLevel - projection.MasteryLevelsSpent) &&
                projection.ProjectionFingerprint == MasteryClaimIdentity.ComputeProjectionFingerprint(projection),
            "Crop harvest original mastery projection/fingerprint is inconsistent.");
        var claimableBefore = new JsonArray(rows.OfType<JsonObject>().Where(row => HarvestMirrorBoolean(row, "claimable"))
            .Select(row => (JsonNode)row.DeepClone()).ToArray());
        Require(JsonNode.DeepEquals(claimableBefore, value["claimable_options"]), "Crop harvest mastery claimable denominator is inconsistent.");
        foreach (var raw in rows.OfType<JsonObject>())
        {
            var index = Int(raw, "skill_id");
            var prior = first.Skills.Single(row => row.SkillIndex == index);
            var target = next.Skills.Single(row => row.SkillIndex == index);
            var option = raw.Deserialize<MasteryClaimOptionRef>()!;
            Require(option.SkillKey == prior.SkillId && option.SkillLevel == prior.UnmodifiedLevel &&
                    option.Claimed == (option.MasteryStatValue != 0) &&
                    option.Claimable == (firstAll && projection.UnspentMasteryLevels > 0 && option.MasteryStatValue == 0) &&
                    option.OptionFingerprint == MasteryClaimIdentity.ComputeOptionFingerprint(option),
                "Crop harvest original mastery skill/claim/fingerprint is inconsistent.");
            option.SkillLevel = target.UnmodifiedLevel;
            option.Claimable = nextAll && projection.UnspentMasteryLevels > 0 && option.MasteryStatValue == 0;
            raw["skill_level"] = option.SkillLevel;
            raw["claimable"] = option.Claimable;
            raw["option_fingerprint"] = MasteryClaimIdentity.ComputeOptionFingerprint(option);
        }
        value["all_base_skills_level_ten"] = nextAll;
        value["claimable_options"] = new JsonArray(rows.OfType<JsonObject>().Where(row => HarvestMirrorBoolean(row, "claimable"))
            .Select(row => (JsonNode)row.DeepClone()).ToArray());
        value["projection_fingerprint"] = MasteryClaimIdentity.ComputeProjectionFingerprint(value.Deserialize<MasteryClaimProjectionRef>()!);
        EqualHarvestResourceField("player", "mastery_claim", expected, actual);
    }

    private static void EqualHarvestResourceField(string domain, string name, JsonObject? expected, JsonObject? actual)
    {
        if (expected is null || actual is null) { EqualField(expected, actual, domain + "." + name); return; }
        var first = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope(domain, name, JsonSerializer.SerializeToElement(expected));
        var next = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope(domain, name, JsonSerializer.SerializeToElement(actual));
        Require(SnapshotHash.CanonicalizeSemantic(first) == SnapshotHash.CanonicalizeSemantic(next),
            "Crop harvest changed unexpected " + domain + "." + name + ".");
    }

    private static bool HarvestMirrorBoolean(JsonNode value, string name) => value[name] is JsonValue flag && flag.TryGetValue<bool>(out var result)
        ? result : throw new InvalidDataException("Crop harvest boolean is unavailable: " + name);
}
