namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static MachineProcessingTargetResult ReadMachineTargets(
        AcquisitionRouteTargetDateFacility facility,
        AcquisitionMachineSourceEvidence source,
        AcquisitionMachineFleetSnapshotState fleet)
    {
        var targets = new List<MachineProcessingTarget>();
        var blocking = new List<string>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in facility.TargetEvaluations.Where(value =>
                     value.MachineSourceMatches == true &&
                     value.MachineQualifiedItemId ==
                        source.MachineQualifiedItemId))
        {
            if (!target.TargetTileX.HasValue ||
                !target.TargetTileY.HasValue)
            {
                blocking.Add("machine_processing_target_tile_missing");
                continue;
            }
            if (!fleet.TryGet(
                    target.TargetLocationId,
                    target.TargetTileX.Value,
                    target.TargetTileY.Value,
                    out var machine) ||
                machine.QualifiedItemId != source.MachineQualifiedItemId)
            {
                blocking.Add(
                    "machine_processing_target_drifted_from_snapshot");
                continue;
            }
            var key = MachineTargetKey(machine);
            if (!keys.Add(key))
            {
                blocking.Add("machine_processing_target_duplicate:" + key);
                continue;
            }
            targets.Add(new MachineProcessingTarget(machine));
        }
        return new MachineProcessingTargetResult(
            targets.ToArray(),
            blocking.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    private static string[] ValidateMachineProcessingSource(
        AcquisitionMachineSourceEvidence source,
        int minimumQuality)
    {
        var reasons = new List<string>();
        if (source.Triggers.Length == 0)
            reasons.Add("authoritative_machine_trigger_missing");
        if (source.MinutesUntilReady < -1 || source.DaysUntilReady < -1)
            reasons.Add("machine_processing_duration_invalid");
        if (source.OnlyCompleteOvernight)
            reasons.Add("machine_only_complete_overnight_not_bound");
        if (source.ReadyTimeModifiers.Length > 0)
            reasons.Add("machine_ready_time_modifiers_not_bound");
        if (!string.IsNullOrWhiteSpace(source.OutputMethod))
            reasons.Add("machine_output_method_time_override_not_bound");
        if (!MachineStackModifiersPreserveLowerBound(source))
            reasons.Add("machine_output_stack_lower_bound_unresolved");
        if (minimumQuality > 0 &&
            (source.CopyQuality || source.QualityModifiers.Length > 0 ||
             MachineMinimumQuality(source) < minimumQuality))
        {
            reasons.Add("machine_output_minimum_quality_unresolved");
        }
        return reasons.ToArray();
    }

    private static bool MachineStackModifiersPreserveLowerBound(
        AcquisitionMachineSourceEvidence source)
    {
        var lowerBound = Math.Max(1, source.MinimumStack);
        foreach (var modifier in source.StackModifiers)
        {
            if (modifier.RandomAmount.HasValue)
                return false;
            var safe = modifier.Modification switch
            {
                0 => modifier.Amount >= 0,
                1 => modifier.Amount <= 0,
                2 => modifier.Amount >= 1,
                3 => modifier.Amount > 0 && modifier.Amount <= 1,
                4 => modifier.Amount >= lowerBound,
                _ => false
            };
            if (!safe)
                return false;
        }
        return true;
    }

    private static int MachineMinimumQuality(
        AcquisitionMachineSourceEvidence source) =>
        source.CopyQuality || source.Quality < 0
            ? 0
            : source.Quality;

    private static bool TryMachineCompletionOffset(
        AcquisitionMachineSourceEvidence source,
        int availableOffsetMinutes,
        int remainingPlayableMinutes,
        out int completionOffsetMinutes)
    {
        completionOffsetMinutes = 0;
        if (availableOffsetMinutes > remainingPlayableMinutes ||
            source.DaysUntilReady > 0)
        {
            return false;
        }
        var duration = source.DaysUntilReady == 0
            ? 0
            : Math.Max(0, source.MinutesUntilReady);
        completionOffsetMinutes = checked(availableOffsetMinutes + duration);
        return completionOffsetMinutes <= remainingPlayableMinutes;
    }

    private static bool TryRemainingPlayableMinutes(
        int timeOfDay,
        out int remainingMinutes)
    {
        remainingMinutes = 0;
        var hour = timeOfDay / 100;
        var minute = timeOfDay % 100;
        if (timeOfDay < 600 || timeOfDay > 2600 ||
            minute < 0 || minute >= 60)
        {
            return false;
        }
        remainingMinutes = checked((26 - hour) * 60 - minute);
        return remainingMinutes >= 0;
    }

    private static string MachineTargetKey(
        AcquisitionMachineRouteState machine) =>
        machine.LocationId + ":" + machine.TileX + "," + machine.TileY;

    private sealed record MachineProcessingTarget(
        AcquisitionMachineRouteState Machine);

    private sealed record MachineProcessingTargetResult(
        MachineProcessingTarget[] Targets,
        string[] BlockingReasons);

    private sealed class MutableMachineSchedule
    {
        public MutableMachineSchedule(
            MachineProcessingTarget target,
            int availableOffsetMinutes)
        {
            Target = target;
            AvailableOffsetMinutes = availableOffsetMinutes;
        }

        public MachineProcessingTarget Target { get; }

        public int AvailableOffsetMinutes { get; set; }

        public int ScheduledAttemptCount { get; set; }

        public int? LastCompletionOffsetMinutes { get; set; }
    }
}
