using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure
{
    public sealed class MasterAnglerWindowIntentValidation
    {
        public string TargetQualifiedItemId { get; init; } = string.Empty;
        public string TargetLocation { get; init; } = string.Empty;
        public string SourceKind { get; init; } = string.Empty;
        public string SourceKey { get; init; } = string.Empty;
        public int WindowFirstTotalDay { get; init; }
        public int WindowLastTotalDay { get; init; }
        public int TargetTotalDay { get; init; }
        public int EffectiveStartTime { get; init; }
        public int LastCastTimeExclusive { get; init; }
        public int DeadlineTotalDayExclusive { get; init; }
        public string WindowIndexPath { get; init; } = string.Empty;
        public string WindowIndexSha256 { get; init; } = string.Empty;
        public MasterAnglerRouteTimingValidation RouteTiming { get; init; } = new();
    }

    public static partial class MasterAnglerWindowIntentValidator
    {
        private const string ValidationStatus = "authoritative_stage_one_window_match";

        public static bool TryReadExactMissingSpecies(
            SnapshotEnvelope snapshot,
            out HashSet<string> missing) =>
            MissingSpecies(snapshot, 72, out missing);

        public static bool TryValidate(
            SnapshotEnvelope snapshot,
            IEnumerable<SmallModelActionParameter> parameters,
            out MasterAnglerWindowIntentValidation validation,
            out string rejectionReason)
        {
            validation = new MasterAnglerWindowIntentValidation();
            rejectionReason = string.Empty;
            var values = parameters.ToArray();
            if (!TryRead(values, "master_angler_target_qualified_item_id", out var targetQid) ||
                !TryRead(values, "master_angler_target_location", out var targetLocation) ||
                !TryRead(values, "master_angler_source_kind", out var sourceKind) ||
                !TryRead(values, "master_angler_source_key", out var sourceKey) ||
                !TryReadInt(values, "master_angler_window_first_total_day", out var firstDay) ||
                !TryReadInt(values, "master_angler_window_last_total_day", out var lastDay) ||
                !TryReadInt(values, "master_angler_target_total_day", out var targetDay) ||
                !TryReadInt(values, "master_angler_effective_start_time", out var effectiveStart) ||
                !TryReadInt(values, "master_angler_last_cast_time_exclusive", out var lastCast) ||
                !TryReadInt(values, "master_angler_stage_one_deadline_total_day_exclusive", out var deadline) ||
                !TryRead(values, "master_angler_window_index_path", out var indexPath) ||
                !TryRead(values, "master_angler_window_index_sha256", out var expectedHash) ||
                !TryRead(values, "master_angler_validation_status", out var status) ||
                !TryRead(values, "master_angler_runtime_terminal_validation_required", out var terminalRequired))
            {
                rejectionReason = "master_angler_window_intent_contract_incomplete_or_duplicated";
                return false;
            }
            if (status != ValidationStatus || terminalRequired != "true" ||
                sourceKind is not ("location_rule" or "mine_override" or "crab_pot") ||
                !IsSha256(expectedHash))
            {
                rejectionReason = "master_angler_window_intent_contract_invalid";
                return false;
            }

            var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
            var currentTime = ReadStateFieldInt(snapshot, "time", "time");
            if (currentDay != targetDay || currentDay < 0 || currentDay >= deadline ||
                currentDay < firstDay || currentDay > lastDay ||
                currentTime < 600 || currentTime >= lastCast || effectiveStart < currentTime)
            {
                rejectionReason = "master_angler_window_intent_snapshot_time_mismatch";
                return false;
            }
            if (!MissingSpecies(snapshot, 72, out var missing) || !missing.Contains(targetQid))
            {
                rejectionReason = "master_angler_window_intent_target_not_in_exact_missing_set";
                return false;
            }

            if (!TryLoadArtifact(indexPath, expectedHash, out var artifact, out rejectionReason))
                return false;
            if (artifact.DeadlineTotalDayExclusive != deadline || deadline != 224 ||
                (!string.IsNullOrWhiteSpace(snapshot.GameVersion) &&
                 !string.Equals(snapshot.GameVersion, artifact.GameVersion, StringComparison.Ordinal)))
            {
                rejectionReason = "master_angler_window_intent_artifact_identity_mismatch";
                return false;
            }

            if (!artifact.WindowsBySpecies.TryGetValue(targetQid, out var windows))
            {
                rejectionReason = "master_angler_window_intent_species_not_in_artifact";
                return false;
            }
            var matchingWindow = windows.SingleOrDefault(window =>
                window.SourceKind == sourceKind && window.SourceKey == sourceKey &&
                (sourceKind == "crab_pot"
                    ? string.IsNullOrWhiteSpace(window.LocationId)
                    : string.Equals(window.LocationId, targetLocation, StringComparison.OrdinalIgnoreCase)) &&
                window.FirstTotalDay == firstDay && window.LastTotalDay == lastDay &&
                window.TimeWindows.Any(time =>
                    Math.Max(currentTime, time.Start) == effectiveStart && time.End == lastCast &&
                    effectiveStart < lastCast));
            if (matchingWindow is null)
            {
                rejectionReason = "master_angler_window_intent_not_an_exact_artifact_window";
                return false;
            }
            if (matchingWindow.DynamicConditionCount > 0 &&
                (sourceKind != "location_rule" ||
                 !RuntimeLocationRuleIsEligible(
                     snapshot,
                     sourceKey,
                     targetQid)))
            {
                rejectionReason =
                    "master_angler_dynamic_location_rule_not_runtime_eligible";
                return false;
            }
            if (sourceKind != "crab_pot" &&
                (matchingWindow.MinimumFishingLevel > ReadFishingLevel(snapshot) ||
                 matchingWindow.RequireMagicBait && !AnyRodContextFlag(snapshot, "has_magic_bait", true) ||
                 matchingWindow.TrainingRodAllowed == false && !AnyRodContextFlag(snapshot, "uses_training_rod", false)))
            {
                rejectionReason = "master_angler_window_intent_equipment_or_skill_requirement_unmet";
                return false;
            }

            var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
            if (sourceKind == "crab_pot" && !string.Equals(
                    currentLocation,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "master_angler_crab_pot_requires_loaded_target_location";
                return false;
            }
            if (!MasterAnglerRouteTimingValidator.TryResolve(
                    snapshot,
                    values,
                    targetLocation,
                    out var routeTiming,
                    out rejectionReason))
            {
                return false;
            }

            validation = new MasterAnglerWindowIntentValidation
            {
                TargetQualifiedItemId = targetQid,
                TargetLocation = targetLocation,
                SourceKind = sourceKind,
                SourceKey = sourceKey,
                WindowFirstTotalDay = firstDay,
                WindowLastTotalDay = lastDay,
                TargetTotalDay = targetDay,
                EffectiveStartTime = effectiveStart,
                LastCastTimeExclusive = lastCast,
                DeadlineTotalDayExclusive = deadline,
                WindowIndexPath = Path.GetFullPath(indexPath),
                WindowIndexSha256 = expectedHash,
                RouteTiming = routeTiming
            };
            return true;
        }

        private static bool MissingSpecies(
            SnapshotEnvelope snapshot,
            int expectedDenominator,
            out HashSet<string> missing)
        {
            missing = new HashSet<string>(StringComparer.Ordinal);
            var progress = ReadStateFieldValue(snapshot, "world_progress", "fish_collection_progress");
            if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
                ReadInt(progress.Value, "eligible_species_count") != expectedDenominator ||
                !progress.Value.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                items.GetArrayLength() != expectedDenominator)
                return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var caught = 0;
            foreach (var row in items.EnumerateArray())
            {
                var qid = ReadString(row, "qualified_item_id");
                var isCaught = ReadBool(row, "caught");
                if (string.IsNullOrWhiteSpace(qid) || !seen.Add(qid) || !isCaught.HasValue)
                    return false;
                if (isCaught.Value)
                    caught++;
                else
                    missing.Add(qid);
            }
            return caught == ReadInt(progress.Value, "caught_eligible_species_count") &&
                missing.Count == ReadInt(progress.Value, "missing_species_count") &&
                caught + missing.Count == expectedDenominator;
        }

        private static int ReadFishingLevel(SnapshotEnvelope snapshot)
        {
            var detail = ReadStateFieldValue(snapshot, "player", "skills_detail");
            if (!detail.HasValue || detail.Value.ValueKind != JsonValueKind.Object ||
                !detail.Value.TryGetProperty("skills", out var skills) || skills.ValueKind != JsonValueKind.Array)
                return -1;
            foreach (var row in skills.EnumerateArray())
            {
                if (ReadString(row, "skill_id") == "fishing")
                    return ReadInt(row, "effective_level");
            }
            return -1;
        }

        private static bool AnyRodContextFlag(SnapshotEnvelope snapshot, string name, bool expected)
        {
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            return contexts.HasValue && contexts.Value.ValueKind == JsonValueKind.Array &&
                contexts.Value.EnumerateArray().Any(row => ReadBool(row, name) == expected);
        }

        private static bool RuntimeLocationRuleIsEligible(
            SnapshotEnvelope snapshot,
            string sourceKey,
            string targetQualifiedItemId)
        {
            var separator = sourceKey.LastIndexOf(':');
            if (separator <= 0 ||
                !int.TryParse(
                    sourceKey[(separator + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var sourceIndex))
            {
                return false;
            }
            var expectedSource = "Data/Locations:" + sourceKey[..separator];
            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue || contexts.Value.ValueKind != JsonValueKind.Array)
                return false;

            return contexts.Value.EnumerateArray().Any(context =>
                ReadBool(context, "complete") == true &&
                context.TryGetProperty("spawn_rules", out var spawnRules) &&
                spawnRules.ValueKind == JsonValueKind.Object &&
                ReadBool(spawnRules, "item_query_resolution_complete") == true &&
                spawnRules.TryGetProperty("rules", out var rules) &&
                rules.ValueKind == JsonValueKind.Array &&
                rules.EnumerateArray().Any(rule =>
                    ReadBool(rule, "condition_met") == true &&
                    ReadBool(rule, "eligible_before_random_rolls") == true &&
                    ReadString(rule, "source") == expectedSource &&
                    ReadInt(rule, "source_index") == sourceIndex &&
                    rule.TryGetProperty("outputs", out var outputs) &&
                    outputs.ValueKind == JsonValueKind.Array &&
                    outputs.EnumerateArray().Any(output =>
                        ReadBool(output, "resolution_complete") == true &&
                        ReadBool(output, "output_eligible_before_random_rolls") == true &&
                        ReadString(output, "qualified_item_id") == targetQualifiedItemId)));
        }

        private static bool TryRead(
            SmallModelActionParameter[] parameters,
            string name,
            out string value)
        {
            var matches = parameters
                .Where(parameter => parameter.Name == name || parameter.Name == "continuation." + name)
                .Select(parameter => parameter.Value)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            value = matches.Length == 1 ? matches[0] : string.Empty;
            return matches.Length == 1;
        }

        private static bool TryReadInt(
            SmallModelActionParameter[] parameters,
            string name,
            out int value)
        {
            value = 0;
            return TryRead(parameters, name, out var text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsSha256(string value) => value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private static int ReadInt(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value) &&
            value.TryGetInt32(out var result) ? result : 0;

        private static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

        private static bool? ReadBool(JsonElement source, string name) => ReadNullableBool(source, name);

        private static bool? ReadNullableBool(JsonElement source, string name)
        {
            if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(name, out var value) ||
                value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return null;
            return value.GetBoolean();
        }

    }
}
