using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileRecoverySteps(SnapshotEnvelope snapshot)
        {
            if (Infrastructure.SleepPromptResumeProjection.IsAvailable(
                    snapshot))
            {
                return new[]
                {
                    Step(
                        "confirm_sleep_yes",
                        "menus.sleep_prompt_context",
                        "day_safely_ended",
                        120)
                };
            }

            if (ActiveMenuOpen(snapshot))
            {
                return Array.Empty<CompiledActionStep>();
            }

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (!GameClockBudgetPolicy.RecoveryWindowStarted(time))
            {
                return new[] { Step("refresh_plan_after_stabilization", "planner", "urgent_risks_rechecked", 0) };
            }

            var sleepSteps = CompileSleepSteps(snapshot);
            if (sleepSteps.Length > 0)
            {
                return sleepSteps;
            }

            var routePlan = BuildRecoveryRoutePlan(snapshot);
            if (routePlan.Step is null)
            {
                return Array.Empty<CompiledActionStep>();
            }

            var edge = routePlan.Step.Edge;
            var expected = "location=" + edge.TargetLocation + ";rolling_horizon_replan=true";
            if (edge.TargetX.HasValue && edge.TargetY.HasValue)
            {
                expected += ";player.tile=" + edge.TargetX.Value + "," + edge.TargetY.Value;
            }

            return new[]
            {
                Step(
                    "traverse_connector",
                    edge.FromLocation + "(" + edge.FromX!.Value + "," + edge.FromY!.Value + ")",
                    expected,
                    routePlan.Step.EstimatedTicks)
            };
        }

        private static bool IsMachineReady(JsonElement machine)
        {
            foreach (var property in new[] { "ready", "ready_for_harvest", "has_output", "needs_processing" })
            {
                if (machine.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True)
                {
                    return true;
                }
            }

            return false;
        }

        private static int? SafeSlotIndex(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "player", "safe_item_context");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !ReadBool(context.Value, "safe_slot_available"))
            {
                return null;
            }

            return ReadNullableInt(context.Value, "safe_slot_index");
        }

        private static CompiledActionStep Step(string stepType, string target, string expectedEffect, int estimatedTicks)
        {
            return new CompiledActionStep
            {
                StepId = "step." + Guid.NewGuid().ToString("N"),
                StepType = stepType,
                Target = target,
                ExpectedEffect = expectedEffect,
                EstimatedTicks = estimatedTicks
            };
        }

        private static string[] SplitList(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => item.Length > 0)
                    .ToArray();
        }
    }
}
