using System.Collections.Generic;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Verifier
{
    public sealed class Verifier
    {
        public SafetyResult Verify(SnapshotEnvelope snapshot, OptionSpec option)
        {
            var missing = new List<string>();
            var results = new List<PreconditionResult>();

            foreach (var factor in option.RequiredStateFactors)
            {
                var status = ReadStatus(snapshot, factor);
                if (status == "available")
                {
                    results.Add(new PreconditionResult
                    {
                        StateFactor = factor,
                        Status = "passed",
                        Message = "available"
                    });
                    continue;
                }

                missing.Add(factor);
                results.Add(new PreconditionResult
                {
                    StateFactor = factor,
                    Status = "unknown",
                    Message = "required transparent field is unavailable or missing"
                });
            }

            return new SafetyResult
            {
                Feasibility = missing.Count == 0 ? "feasible" : "unknown",
                MissingStateFactors = missing.ToArray(),
                PreconditionResults = results.ToArray(),
                BlockingReasons = missing.Count == 0
                    ? new string[0]
                    : new[] { "missing_required_state" }
            };
        }

        private static string ReadStatus(SnapshotEnvelope snapshot, string dottedPath)
        {
            var parts = dottedPath.Split('.');
            if (parts.Length < 2 || !snapshot.State.TryGetValue(parts[0], out var current))
            {
                return "unavailable";
            }

            for (var i = 1; i < parts.Length; i++)
            {
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(parts[i], out current))
                {
                    return "unavailable";
                }
            }

            if (current.ValueKind == JsonValueKind.Object &&
                current.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.String)
            {
                return status.GetString() ?? "unavailable";
            }

            return "unavailable";
        }
    }
}
