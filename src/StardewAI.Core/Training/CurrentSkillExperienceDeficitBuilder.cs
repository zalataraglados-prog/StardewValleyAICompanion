using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed class CurrentSkillExperienceDeficitBuilder
{
    private static readonly string[] SkillIds =
        { "farming", "fishing", "foraging", "mining", "combat", "luck" };

    public CurrentSkillExperienceDeficit Build(SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
            throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.SchemaVersion != "snapshot.v1" ||
            snapshot.GameVersion != NativeSkillExperienceRules.GameVersion || snapshot.GameTick < 0)
            return Blocked("skill_deficit_snapshot_version_or_tick_unsupported");
        if (snapshot.State is null ||
            !SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash))
            return Blocked("skill_deficit_state_hash_mismatch");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "skills_detail")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "player", "level")))
            return Blocked("skill_deficit_source_unreadable");

        var detail = ReadStateFieldValue(snapshot, "player", "skills_detail");
        var playerLevel = ReadStateFieldValue(snapshot, "player", "level");
        if (!detail.HasValue || !playerLevel.HasValue ||
            playerLevel.Value.ValueKind != JsonValueKind.Number ||
            !playerLevel.Value.TryGetInt32(out var scoringLevel) ||
            !Integer(detail.Value, "scoring_level", out var detailLevel) ||
            !Integer(detail.Value, "vanilla_level_cap", out var cap) || cap != 10 ||
            !Property(detail.Value, "skills", out var skills) ||
            skills.ValueKind != JsonValueKind.Array || skills.GetArrayLength() != 6)
            return Blocked("skill_deficit_source_shape_invalid");

        var rows = new Dictionary<int, CurrentSkillExperienceDeficitRow>();
        foreach (var row in skills.EnumerateArray())
        {
            if (!TryReadRow(row, out var projected) ||
                rows.ContainsKey(projected.SkillIndex))
                return Blocked("skill_deficit_skill_row_invalid_or_duplicate");
            rows.Add(projected.SkillIndex, projected);
        }
        // Native scoring includes the dormant Luck field; vanilla XP cannot raise it.
        if (rows[5].UnmodifiedLevel != 0 || rows[5].Experience != 0)
            return Blocked("skill_deficit_nonvanilla_luck_state");
        var selected = Enumerable.Range(0, 5).Select(index => rows[index]).ToArray();
        var baseLevels = selected.Sum(row => row.UnmodifiedLevel);
        if (scoringLevel != baseLevels / 2 || detailLevel != scoringLevel)
            return Blocked("skill_deficit_scoring_level_inconsistent");

        return new CurrentSkillExperienceDeficit
        {
            Status = "ready_current_state_only",
            SourceStateHash = snapshot.StateHash,
            GameTick = snapshot.GameTick,
            ScoringLevel = scoringLevel,
            BaseLevelsMissingForFirstCriterion = Math.Max(0, 30 - baseLevels),
            BaseLevelsMissingForSecondCriterion = 50 - baseLevels,
            ExperienceMissingForFiveLevelTenSkills =
                selected.Sum(row => row.ExperienceToLevelTen),
            Skills = selected
        };
    }

    private static bool TryReadRow(
        JsonElement row,
        out CurrentSkillExperienceDeficitRow projected)
    {
        projected = new CurrentSkillExperienceDeficitRow();
        if (!Integer(row, "skill_index", out var index) || index is < 0 or > 5 ||
            !Property(row, "skill_id", out var id) || id.ValueKind != JsonValueKind.String ||
            id.GetString() != SkillIds[index] ||
            !Integer(row, "unmodified_level", out var level) || level is < 0 or > 10 ||
            !Integer(row, "effective_level", out var effective) ||
            !Integer(row, "temporary_buff_delta", out var buff) ||
            (long)effective - level != buff ||
            !Integer(row, "experience", out var experience) || experience < 0 ||
            !Property(row, "at_vanilla_level_cap", out var atCap) ||
            atCap.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            atCap.GetBoolean() != (level == 10) ||
            experience < (level == 0 ? 0 : NativeSkillExperienceRules.ExperienceForLevel(level)) ||
            (level < 10 && experience >= NativeSkillExperienceRules.ExperienceForLevel(level + 1)))
            return false;

        var next = level < 10 ? level + 1 : (int?)null;
        var nextThreshold = level < 10 ? NativeSkillExperienceRules.ExperienceForLevel(level + 1) : (int?)null;
        var nextDeficit = nextThreshold.HasValue
            ? nextThreshold.Value - experience : (int?)null;
        if (!NullableInteger(row, "next_level", next) ||
            !NullableInteger(row, "next_level_experience", nextThreshold) ||
            !NullableInteger(row, "experience_to_next_level", nextDeficit))
            return false;

        projected = new CurrentSkillExperienceDeficitRow
        {
            SkillId = SkillIds[index],
            SkillIndex = index,
            UnmodifiedLevel = level,
            EffectiveLevel = effective,
            Experience = experience,
            NextLevel = next,
            ExperienceToNextLevel = nextDeficit,
            ExperienceToLevelTen = Math.Max(0, NativeSkillExperienceRules.ExperienceForLevel(10) - experience)
        };
        return true;
    }

    private static bool Property(JsonElement value, string name, out JsonElement property)
    {
        property = default;
        if (value.ValueKind != JsonValueKind.Object)
            return false;
        var found = false;
        foreach (var item in value.EnumerateObject())
        {
            if (!item.NameEquals(name))
                continue;
            if (found)
                return false;
            property = item.Value;
            found = true;
        }
        return found;
    }

    private static bool Integer(JsonElement value, string name, out int number)
    {
        number = 0;
        return Property(value, name, out var property) &&
            property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out number);
    }

    private static bool NullableInteger(JsonElement value, string name, int? expected) =>
        expected.HasValue
            ? Integer(value, name, out var actual) && actual == expected.Value
            : Property(value, name, out var property) && property.ValueKind == JsonValueKind.Null;

    private static CurrentSkillExperienceDeficit Blocked(string reason) => new()
    {
        BlockingReasons = new[] { reason }
    };
}
