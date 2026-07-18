using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static MachineHarvestExperienceProjection ReadMachineHarvestExperience(object? machineData, Farmer player)
    {
        var raw = machineData is null ? string.Empty : ReadString(machineData, "ExperienceGainOnHarvest") ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return new MachineHarvestExperienceProjection(
                raw,
                Array.Empty<MachineHarvestExperienceEntry>(),
                Array.Empty<MachineHarvestExperienceDelta>(),
                0,
                "exact_no_configured_experience");
        }

        var tokens = raw.Split(' ');
        var entries = new List<MachineHarvestExperienceEntry>();
        var deltas = new Dictionary<int, int>();
        var experience = Enumerable.Range(0, 6).Select(index => player.experiencePoints[index]).ToArray();
        var levels = Enumerable.Range(0, 6).Select(player.GetUnmodifiedSkillLevel).ToArray();
        var masteryDelta = 0;
        try
        {
            for (var index = 0; index < tokens.Length; index += 2)
            {
                var skillToken = tokens[index];
                var amountToken = index + 1 < tokens.Length ? tokens[index + 1] : string.Empty;
                var skillIndex = Farmer.getSkillNumberFromName(skillToken);
                var amountValid = ArgUtility.TryGetInt(tokens, index + 1, out var amount, out _, "int amount");
                if (skillIndex == -1 || !amountValid)
                {
                    entries.Add(new MachineHarvestExperienceEntry(
                        index / 2,
                        skillToken,
                        amountToken,
                        skillIndex == -1 ? null : skillIndex,
                        skillIndex == -1 ? string.Empty : SkillId(skillIndex),
                        amountValid ? amount : null,
                        0,
                        0,
                        skillIndex == -1 ? "native_skips_unknown_skill" : "native_skips_invalid_amount"));
                    continue;
                }

                var levelBeforeCall = levels.Sum() / 2;
                var effectiveDelta = skillIndex == Farmer.luckSkill || amount <= 0 ? 0 : amount;
                var entryMasteryDelta = effectiveDelta > 0 && levelBeforeCall >= 25
                    ? Math.Max(1, skillIndex == Farmer.farmingSkill ? effectiveDelta / 2 : effectiveDelta)
                    : 0;
                entries.Add(new MachineHarvestExperienceEntry(
                    index / 2,
                    skillToken,
                    amountToken,
                    skillIndex,
                    SkillId(skillIndex),
                    amount,
                    effectiveDelta,
                    entryMasteryDelta,
                    effectiveDelta > 0 ? "native_gain_applies" : skillIndex == Farmer.luckSkill ? "native_sink_ignores_luck" : "native_sink_ignores_nonpositive_amount"));
                deltas[skillIndex] = deltas.TryGetValue(skillIndex, out var current) ? checked(current + effectiveDelta) : effectiveDelta;
                masteryDelta = checked(masteryDelta + entryMasteryDelta);
                if (effectiveDelta <= 0)
                {
                    continue;
                }

                var oldExperience = experience[skillIndex];
                var newExperience = checked(oldExperience + effectiveDelta);
                var gainedLevel = Farmer.checkForLevelGain(oldExperience, newExperience);
                experience[skillIndex] = newExperience;
                if (gainedLevel != -1)
                {
                    levels[skillIndex] = gainedLevel;
                }
            }
        }
        catch (OverflowException)
        {
            return new MachineHarvestExperienceProjection(
                raw,
                entries.ToArray(),
                Array.Empty<MachineHarvestExperienceDelta>(),
                0,
                "blocked_integer_overflow_in_modded_experience_data");
        }

        var projectedDeltas = deltas
            .OrderBy(pair => pair.Key)
            .Select(pair => new MachineHarvestExperienceDelta(SkillId(pair.Key), pair.Key, pair.Value))
            .ToArray();
        return new MachineHarvestExperienceProjection(
            raw,
            entries.ToArray(),
            projectedDeltas,
            masteryDelta,
            "exact_native_pair_parser_and_gain_sink");
    }

    private static string SkillId(int skillIndex) => Farmer.getSkillNameFromIndex(skillIndex).ToLowerInvariant();

    private sealed record MachineHarvestExperienceProjection(
        string Raw,
        MachineHarvestExperienceEntry[] Entries,
        MachineHarvestExperienceDelta[] Deltas,
        int MasteryExperienceDelta,
        string Status);

    private sealed record MachineHarvestExperienceEntry(
        int PairIndex,
        string SkillToken,
        string AmountToken,
        int? NativeSkillIndex,
        string SkillId,
        int? ConfiguredAmount,
        int EffectiveExperienceDelta,
        int MasteryExperienceDelta,
        string Status);

    private sealed record MachineHarvestExperienceDelta(string SkillId, int SkillIndex, int Delta);
}
