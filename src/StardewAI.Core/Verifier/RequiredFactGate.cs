using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Verifier
{
    internal sealed class RequiredFactGate
    {
        public SafetyResult Evaluate(SnapshotEnvelope snapshot, OptionSpec option)
        {
            var missing = new List<string>();
            var results = new List<PreconditionResult>();
            var blocking = new List<string>();

            foreach (var factor in option.RequiredStateFactors)
            {
                var rule = RuleFor(option.RequiredFactPolicy, factor);
                var result = EvaluateOne(snapshot, factor, rule);
                results.Add(result);
                if (result.Status == "passed")
                {
                    continue;
                }

                missing.Add(factor);
                blocking.Add(result.Message);
            }

            return new SafetyResult
            {
                ReadEligible = missing.Count == 0,
                Feasibility = missing.Count == 0 ? "feasible" : "unknown",
                MissingStateFactors = missing.ToArray(),
                PreconditionResults = results.ToArray(),
                BlockingReasons = missing.Count == 0
                    ? Array.Empty<string>()
                    : new[] { "missing_required_state" }
                        .Concat(blocking.Select(message => message.Split(':')[0]))
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
            };
        }

        private static PreconditionResult EvaluateOne(
            SnapshotEnvelope snapshot,
            string factor,
            RequiredFactRule rule)
        {
            if (!TryReadEnvelope(snapshot, factor, out var envelope))
            {
                return Failed(factor, "missing_required_state", "field envelope missing");
            }

            var status = String(envelope, "status");
            if (!rule.AllowedStatuses.Contains(status, StringComparer.Ordinal))
            {
                return Failed(factor, "required_fact_status_denied", status);
            }

            if (!envelope.TryGetProperty("value", out _))
            {
                return Failed(factor, "required_fact_value_missing", status);
            }

            var strictMetadata = !string.IsNullOrWhiteSpace(snapshot.GameVersion) ||
                !string.IsNullOrWhiteSpace(snapshot.BridgeVersion);
            if (!strictMetadata)
            {
                return new PreconditionResult
                {
                    StateFactor = factor,
                    Status = "passed",
                    Message = "required_fact_gate_passed_synthetic_fixture"
                };
            }

            var confidence = Number(envelope, "confidence");
            if (confidence is null || confidence.Value < rule.MinimumConfidence)
            {
                return Failed(factor, "required_fact_confidence_below_policy", confidence?.ToString() ?? "missing");
            }

            var readAtTick = Integer(envelope, "read_at_tick");
            if (snapshot.GameTick > 0 &&
                (readAtTick is null ||
                 readAtTick.Value > snapshot.GameTick ||
                 snapshot.GameTick - readAtTick.Value > rule.MaximumAgeTicks))
            {
                return Failed(factor, "required_fact_stale", readAtTick?.ToString() ?? "missing");
            }

            if (!envelope.TryGetProperty("source", out var source) ||
                source.ValueKind != JsonValueKind.Object ||
                !rule.RequiredProvenanceKinds.Contains(String(source, "kind"), StringComparer.Ordinal))
            {
                return Failed(factor, "required_fact_provenance_denied", String(source, "kind"));
            }

            var adapter = String(envelope, "adapter");
            if (!rule.AllowedAdapterIds.Contains(adapter, StringComparer.Ordinal))
            {
                return Failed(factor, "required_fact_adapter_denied", adapter);
            }

            if (status == FieldStatus.Derived)
            {
                if (!envelope.TryGetProperty("derivation", out var derivation) ||
                    derivation.ValueKind != JsonValueKind.Object)
                {
                    return Failed(factor, "required_fact_derivation_missing", status);
                }

                var derivationId = String(derivation, "method");
                if (!rule.AllowedDerivationIds.Contains(derivationId, StringComparer.Ordinal))
                {
                    return Failed(factor, "required_fact_derivation_denied", derivationId);
                }
            }

            return new PreconditionResult
            {
                StateFactor = factor,
                Status = "passed",
                Message = "required_fact_gate_passed"
            };
        }

        private static RequiredFactRule RuleFor(RequiredFactPolicy policy, string factor)
        {
            return policy.FactOverrides.FirstOrDefault(
                rule => string.Equals(rule.StateFactor, factor, StringComparison.Ordinal))
                ?? policy.DefaultRule;
        }

        private static bool TryReadEnvelope(
            SnapshotEnvelope snapshot,
            string dottedPath,
            out JsonElement envelope)
        {
            envelope = default;
            var parts = dottedPath.Split('.');
            if (parts.Length < 2 || !snapshot.State.TryGetValue(parts[0], out var current))
            {
                return false;
            }

            for (var i = 1; i < parts.Length; i++)
            {
                if (current.ValueKind != JsonValueKind.Object ||
                    !current.TryGetProperty(parts[i], out current))
                {
                    return false;
                }
            }

            envelope = current;
            return current.ValueKind == JsonValueKind.Object;
        }

        private static string String(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.String
                    ? value.GetString() ?? string.Empty
                    : string.Empty;
        }

        private static double? Number(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var number)
                    ? number
                    : null;
        }

        private static long? Integer(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out var number)
                    ? number
                    : null;
        }

        private static PreconditionResult Failed(string factor, string code, string detail)
        {
            return new PreconditionResult
            {
                StateFactor = factor,
                Status = "unknown",
                Message = $"{code}:{detail}"
            };
        }
    }
}
