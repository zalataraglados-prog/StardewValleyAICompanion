using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMasterAnglerTeacherFrontierBuilder
{
    private static void ValidateSets(
        GoalRequirementSet inventory,
        AcquisitionRequirementSetLowering lowering)
    {
        if (!inventory.AcquisitionRoutesComplete ||
            inventory.RequiredGroupCount != 72 ||
            inventory.Groups.Length != 72 ||
            lowering.RequiredGroupCount != 72 ||
            lowering.TeacherAdmittedGroupCount != 72 ||
            lowering.Groups.Length != 72)
        {
            throw new InvalidDataException(
                "Master Angler inventory and acquisition lowering must contain 72 admitted species.");
        }

        var lowered = lowering.Groups.ToDictionary(
            value => value.RequirementId,
            StringComparer.Ordinal);
        var seenQualifiedItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in inventory.Groups)
        {
            if (group.SelectionRule != "all_required" ||
                group.RequiredAlternativeCount != 1 ||
                group.Alternatives.Length != 1 ||
                !lowered.TryGetValue(group.RequirementId, out var loweredGroup) ||
                !loweredGroup.TeacherAdmissionReady ||
                loweredGroup.Alternatives.Length != 1)
            {
                throw new InvalidDataException(
                    "Every Master Angler requirement must be one admitted exact species.");
            }
            var expected = group.Alternatives.Single();
            var actual = loweredGroup.Alternatives.Single();
            if (expected.MatchKind != "item_id" ||
                expected.Amount != 1 || expected.MinimumQuality != 0 ||
                expected.QualifiedItemId != "(O)" + expected.ItemId ||
                !seenQualifiedItemIds.Add(expected.QualifiedItemId) ||
                expected.ItemId != actual.ItemId ||
                expected.QualifiedItemId != actual.QualifiedItemId ||
                expected.MatchKind != actual.MatchKind ||
                expected.Amount != actual.Amount ||
                expected.MinimumQuality != actual.MinimumQuality ||
                !actual.TeacherAdmissionReady ||
                !actual.Routes.Any(value =>
                    value.TeacherAdmissionReady && value.EndpointOptionIds.Length > 0))
            {
                throw new InvalidDataException(
                    "Master Angler requirement identity or acquisition lowering drifted.");
            }
        }
    }

    private static Dictionary<string, bool> ReadExactProgress(
        SnapshotEnvelope snapshot,
        GoalRequirementSet inventory)
    {
        if (!snapshot.State.TryGetValue("world_progress", out var world) ||
            world.ValueKind != JsonValueKind.Object ||
            !world.TryGetProperty("fish_collection_progress", out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !field.TryGetProperty("value", out var progress) ||
            progress.ValueKind != JsonValueKind.Object ||
            !progress.TryGetProperty("eligible_species_count", out var eligibleValue) ||
            !eligibleValue.TryGetInt32(out var eligible) || eligible != 72 ||
            !progress.TryGetProperty("caught_eligible_species_count", out var caughtValue) ||
            !caughtValue.TryGetInt32(out var reportedCaught) || reportedCaught < 0 ||
            !progress.TryGetProperty("missing_species_count", out var missingValue) ||
            !missingValue.TryGetInt32(out var reportedMissing) || reportedMissing < 0 ||
            reportedCaught + reportedMissing != eligible ||
            !progress.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array || items.GetArrayLength() != eligible ||
            !progress.TryGetProperty("missing_item_ids", out var missingIds) ||
            missingIds.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Live fish_collection_progress is unavailable or lacks the exact 72-species denominator.");
        }

        var expected = inventory.Groups.ToDictionary(
            value => value.Alternatives.Single().QualifiedItemId,
            value => value.Alternatives.Single().ItemId,
            StringComparer.Ordinal);
        var reportedMissingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in missingIds.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()) ||
                !reportedMissingIds.Add(value.GetString()!))
            {
                throw new InvalidDataException(
                    "Live fish_collection_progress has invalid missing_item_ids.");
            }
        }

        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var actualMissingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in items.EnumerateArray())
        {
            var itemId = CurrentTeacherFrontierSupport.RequiredString(row, "item_id");
            var qualifiedItemId = CurrentTeacherFrontierSupport.RequiredString(
                row,
                "qualified_item_id");
            if (!row.TryGetProperty("caught", out var caught) ||
                caught.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !expected.TryGetValue(qualifiedItemId, out var expectedItemId) ||
                expectedItemId != itemId ||
                !result.TryAdd(qualifiedItemId, caught.GetBoolean()))
            {
                throw new InvalidDataException(
                    "Live fish_collection_progress contains a mismatched or duplicated species row.");
            }
            if (!caught.GetBoolean())
                actualMissingIds.Add(itemId);
        }
        var actualCaught = result.Count(value => value.Value);
        var actualMissing = result.Count - actualCaught;
        if (result.Count != 72 ||
            actualCaught != reportedCaught || actualMissing != reportedMissing ||
            !reportedMissingIds.SetEquals(actualMissingIds) ||
            !MasterAnglerWindowIntentValidator.TryReadExactMissingSpecies(
                snapshot,
                out var validatorMissing) ||
            !validatorMissing.SetEquals(result.Where(value => !value.Value)
                .Select(value => value.Key)))
        {
            throw new InvalidDataException(
                "Live fish_collection_progress aggregate and per-species identities disagree.");
        }
        return result;
    }

    private static ValidatedIntentProvenance ValidateIntentProvenance(
        string inventoryPath,
        AuthoritativeRequirementInventoryReport inventory,
        GoalRequirementSet inventorySet,
        SnapshotEnvelope snapshot,
        MasterAnglerTargetDateIntentSet intents,
        IReadOnlyDictionary<string, bool> progress)
    {
        if (intents.SchemaVersion != "master_angler_target_date_intents.v1" ||
            intents.Status is not (
                "validated_current_date_full_route_intents_runtime_terminal_pending" or
                "no_authoritative_static_rod_intent_available_now") ||
            intents.TrainingLabelEligible ||
            intents.CandidateCount != intents.Candidates.Length ||
            intents.MissingSpeciesCount != progress.Count(value => !value.Value) ||
            intents.CurrentTotalDay != ReadStateInt(snapshot, "time", "total_days") ||
            intents.CurrentTime != ReadStateInt(snapshot, "time", "time") ||
            !string.Equals(
                intents.CurrentLocationId,
                ReadStateString(snapshot, "player", "location_id"),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Master Angler target-date intent metadata does not match the current snapshot.");
        }

        var windowPath = Path.GetFullPath(intents.WindowIndexPath);
        var timingPath = Path.GetFullPath(intents.RouteTimingCalibrationPath);
        if (!File.Exists(windowPath) || !File.Exists(timingPath) ||
            !string.Equals(
                CurrentTeacherFrontierSupport.HashFile(windowPath),
                intents.WindowIndexSha256,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                CurrentTeacherFrontierSupport.HashFile(timingPath),
                intents.RouteTimingCalibrationSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Master Angler window index or route timing calibration hash drifted.");
        }
        var windowIndex = CurrentTeacherFrontierSupport.Read<
            MasterAnglerStageOneWindowIndex>(windowPath, "Master Angler window index");
        var expectedQualifiedItemIds = inventorySet.Groups
            .Select(value => value.Alternatives.Single().QualifiedItemId)
            .ToHashSet(StringComparer.Ordinal);
        if (windowIndex.SchemaVersion != "master_angler_stage_one_window_index.v1" ||
            windowIndex.Status != "complete_static_windows_dynamic_execution_pending" ||
            !windowIndex.StaticWindowCoverageComplete ||
            windowIndex.TrainingLabelEligible ||
            windowIndex.NativeDenominatorCount != 72 ||
            windowIndex.Species.Length != 72 ||
            windowIndex.UnresolvedSpeciesIds.Length != 0 ||
            windowIndex.GoalId != inventory.GoalId ||
            !expectedQualifiedItemIds.SetEquals(windowIndex.Species.Select(
                value => value.QualifiedItemId)))
        {
            throw new InvalidDataException(
                "Master Angler window index does not match the authoritative requirement set.");
        }

        var catalogPath = Path.GetFullPath(windowIndex.CatalogPath);
        if (!File.Exists(catalogPath) ||
            !string.Equals(
                CurrentTeacherFrontierSupport.HashFile(catalogPath),
                windowIndex.CatalogSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Master Angler opportunity catalog hash drifted.");
        }
        var catalog = CurrentTeacherFrontierSupport.Read<
            MasterAnglerOpportunityCatalogReport>(
            catalogPath,
            "Master Angler opportunity catalog");
        if (catalog.SchemaVersion != "master_angler_opportunity_catalog.v1" ||
            catalog.Status != "complete" || !catalog.SourceInventoryComplete ||
            !catalog.StaticCalendarConstraintComplete ||
            !catalog.LocationRuleSpawnChanceInputInventoryComplete ||
            catalog.NativeDenominatorCount != 72 || catalog.Species.Length != 72 ||
            catalog.UnresolvedSpeciesIds.Length != 0 ||
            catalog.UnresolvedCalendarRuleIds.Length != 0 ||
            catalog.UnresolvedSpawnChanceInputRuleIds.Length != 0 ||
            catalog.GoalId != inventory.GoalId ||
            !string.Equals(
                catalog.RequirementInventorySha256,
                CurrentTeacherFrontierSupport.HashFile(inventoryPath),
                StringComparison.OrdinalIgnoreCase) ||
            !expectedQualifiedItemIds.SetEquals(catalog.Species.Select(
                value => value.QualifiedItemId)) ||
            (!string.IsNullOrWhiteSpace(snapshot.GameVersion) &&
             !string.Equals(snapshot.GameVersion, catalog.GameVersion,
                 StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Master Angler opportunity catalog does not bind the current authoritative inventory.");
        }

        var validated = new List<ValidatedIntent>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intent in intents.Candidates)
        {
            if (string.IsNullOrWhiteSpace(intent.IntentId) ||
                !seenIds.Add(intent.IntentId) ||
                intent.RuntimeTerminalValidationRequired != true)
            {
                throw new InvalidDataException(
                    "Master Angler target-date intent identity is missing or duplicated.");
            }
            if (!MasterAnglerWindowIntentValidator.TryValidate(
                    snapshot,
                    intent.Parameters,
                    out var validation,
                    out var rejectionReason))
            {
                throw new InvalidDataException(
                    "Master Angler target-date intent failed shared validation: " +
                    rejectionReason);
            }
            var expectedOption = validation.SourceKind == "crab_pot"
                ? "fishing.collect_crab_pots"
                : "fishing.catch_fish";
            if (intent.OptionId != expectedOption ||
                intent.TargetQualifiedItemId != validation.TargetQualifiedItemId ||
                !string.Equals(intent.TargetLocation, validation.TargetLocation,
                    StringComparison.OrdinalIgnoreCase) ||
                intent.SourceKind != validation.SourceKind ||
                intent.SourceKey != validation.SourceKey ||
                intent.FirstTotalDay != validation.WindowFirstTotalDay ||
                intent.LastTotalDay != validation.WindowLastTotalDay ||
                intent.EffectiveStartTime != validation.EffectiveStartTime ||
                intent.LastCastTimeExclusive != validation.LastCastTimeExclusive ||
                intent.DeadlineSlackDays !=
                    validation.WindowLastTotalDay - validation.TargetTotalDay ||
                !string.Equals(validation.WindowIndexPath, windowPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(validation.WindowIndexSha256,
                    intents.WindowIndexSha256, StringComparison.OrdinalIgnoreCase) ||
                !seenKeys.Add(IntentKey(validation)))
            {
                throw new InvalidDataException(
                    "Master Angler target-date intent fields drifted from shared validation.");
            }
            validated.Add(new ValidatedIntent(intent, validation));
        }
        return new ValidatedIntentProvenance(
            CurrentTeacherFrontierSupport.HashFile(catalogPath),
            validated.ToArray());
    }

    private static int ReadStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadStateValue(snapshot, section, field);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException($"Snapshot field {section}.{field} is not an integer.");
    }

    private static string ReadStateString(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        var value = ReadStateValue(snapshot, section, field);
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new InvalidDataException($"Snapshot field {section}.{field} is not a string.");
    }

    private static JsonElement ReadStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (!snapshot.State.TryGetValue(section, out var sectionValue) ||
            sectionValue.ValueKind != JsonValueKind.Object ||
            !sectionValue.TryGetProperty(field, out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Snapshot field {section}.{field} is unavailable.");
        }
        return value;
    }

    private sealed record ValidatedIntent(
        MasterAnglerTargetDateIntent Intent,
        MasterAnglerWindowIntentValidation Validation);

    private sealed record ValidatedIntentProvenance(
        string OpportunityCatalogSha256,
        ValidatedIntent[] ValidatedIntents);
}
