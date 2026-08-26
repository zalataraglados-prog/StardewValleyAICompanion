using System;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] ObjectTrapRecoveryCandidates(
        SnapshotEnvelope snapshot)
    {
        var context = ReadStateFieldValue(
            snapshot,
            "player",
            "object_trap_recovery");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            ReadBool(
                context.Value,
                "trapped_by_four_non_passable_objects") != true)
        {
            return Array.Empty<EventCandidate>();
        }

        var probe = CompilerProbeItem(
            snapshot,
            new OptionAvailabilityCandidate
            {
                OptionId = "recovery.escape_object_trap"
            });
        var reasons = probe is null
            ? new[] { "object_trap_compiler_probe_unavailable" }
            : CompilerProbeBlockingReasons(probe);
        var parameters = probe?.NormalizedCommand.Parameters ??
            Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "recovery:escape_object_trap",
                Kind = "recovery_escape_object_trap",
                Available = reasons.Length == 0,
                LocationId = ReadStateFieldString(
                    snapshot,
                    "player",
                    "location_id"),
                TileX = ReadParameterInt(parameters, "target_tile_x"),
                TileY = ReadParameterInt(parameters, "target_tile_y"),
                ExpectedEffect =
                    "one_exact_safe_adjacent_machine_recovered;player_not_trapped",
                EstimatedTicks = 90,
                BlockReasons = reasons,
                Parameters = parameters
            }
        };
    }
}
