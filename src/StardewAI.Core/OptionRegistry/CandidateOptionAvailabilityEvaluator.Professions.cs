using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static readonly int[] GrandpaPerfectionProfessionOrder =
            { 1, 4, 6, 8, 13, 16, 18, 21, 24, 26 };

        private static EventCandidate[] ProfessionChoiceCandidates(SnapshotEnvelope snapshot)
        {
            if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "LevelUpMenu", StringComparison.Ordinal))
            {
                return Array.Empty<EventCandidate>();
            }

            var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
            if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { BlockedProfessionCandidate("level_up_menu_transparent_state_missing") };
            }

            var reasons = ProfessionMenuBlockReasons(state.Value);
            if (!state.Value.TryGetProperty("profession_choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array)
            {
                reasons.Add("profession_choices_missing");
                return new[] { BlockedProfessionCandidate(reasons.ToArray()) };
            }

            var skill = ReadInt(state.Value, "current_skill");
            var level = ReadInt(state.Value, "current_level");
            var rows = choices.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    row.TryGetProperty("profession_id", out var id) && id.TryGetInt32(out _))
                .Select(row => new
                {
                    Id = row.GetProperty("profession_id").GetInt32(),
                    Title = ReadString(row, "title"),
                    Description = row.TryGetProperty("description_lines", out var lines) &&
                        lines.ValueKind == JsonValueKind.Array
                            ? string.Join("\n", lines.EnumerateArray()
                                .Where(line => line.ValueKind == JsonValueKind.String)
                                .Select(line => line.GetString() ?? string.Empty))
                            : string.Empty
                })
                .GroupBy(row => row.Id)
                .Select(group => group.First())
                .ToArray();
            if (rows.Length != 2)
            {
                reasons.Add("exactly_two_profession_choices_required");
            }
            if (rows.Any(row => string.IsNullOrWhiteSpace(row.Title)))
            {
                reasons.Add("profession_title_missing");
            }
            if (rows.Any(row => string.IsNullOrWhiteSpace(row.Description)))
            {
                reasons.Add("profession_description_missing");
            }
            if (rows.Length == 0)
            {
                return new[] { BlockedProfessionCandidate(reasons.ToArray()) };
            }

            return rows.Select(row =>
            {
                var preferenceRank = Array.IndexOf(GrandpaPerfectionProfessionOrder, row.Id);
                if (preferenceRank < 0)
                {
                    preferenceRank = GrandpaPerfectionProfessionOrder.Length + row.Id;
                }

                return new EventCandidate
                {
                    CandidateId = $"skills:profession:{skill}:{level}:{row.Id}",
                    Kind = "choose_profession",
                    Available = reasons.Count == 0,
                    LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                    DisplayName = row.Title,
                    ExpectedEffect = $"profession_id={row.Id};profession_title={row.Title};skill_id={skill};level={level};baseline_preference_rank={preferenceRank};native_level_up_menu_completed=true",
                    EstimatedTicks = 10,
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = new[]
                    {
                        Parameter("execution_option_id", "executor.close_menu"),
                        Parameter("profession_choice_id", row.Id.ToString()),
                        Parameter("profession_choice_source", "small_model_exact_offered_choice"),
                        Parameter("profession_skill_id", skill.ToString()),
                        Parameter("profession_level", level.ToString()),
                        Parameter("profession_title", row.Title),
                        Parameter("profession_description", row.Description)
                    }
                };
            }).ToArray();
        }

        private static List<string> ProfessionMenuBlockReasons(JsonElement state)
        {
            var reasons = new List<string>();
            if (!string.Equals(ReadString(state, "kind"), "level_up", StringComparison.Ordinal))
                reasons.Add("level_up_menu_transparent_state_missing");
            if (ReadBool(state, "reflection_fields_complete") != true)
                reasons.Add("level_up_menu_reflection_fields_incomplete");
            if (ReadBool(state, "is_active") != true)
                reasons.Add("level_up_menu_not_active");
            if (ReadBool(state, "is_profession_chooser") != true)
                reasons.Add("level_up_menu_not_profession_chooser");
            if (ReadBool(state, "can_receive_input") != true)
                reasons.Add("level_up_menu_input_not_ready");
            if (!HasNumber(state, "current_skill"))
                reasons.Add("level_up_menu_current_skill_missing");
            if (!HasNumber(state, "current_level"))
                reasons.Add("level_up_menu_current_level_missing");
            return reasons;
        }

        private static EventCandidate BlockedProfessionCandidate(params string[] reasons)
        {
            return new EventCandidate
            {
                CandidateId = "skills:profession:blocked",
                Kind = "choose_profession",
                Available = false,
                ExpectedEffect = "profession_not_selected",
                EstimatedTicks = 10,
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray()
            };
        }
    }
}
