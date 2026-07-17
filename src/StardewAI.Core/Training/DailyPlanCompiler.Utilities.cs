using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static string StepId(PolicyEventCandidatePrediction candidate, string suffix, int index)
        {
            return Sanitize(candidate.CandidateId) + "." + suffix + "." + index;
        }

        private static string Sanitize(string value)
        {
            var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            var sanitized = new string(chars);
            return string.IsNullOrWhiteSpace(sanitized) ? "candidate" : sanitized;
        }

        private static int TicksToMinutes(int ticks)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, ticks) / 60.0));
        }

        private static string CandidateParameter(PolicyEventCandidatePrediction candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value
                ?? string.Empty;
        }

        private static int? CandidateInt(PolicyEventCandidatePrediction candidate, string name)
        {
            return int.TryParse(CandidateParameter(candidate, name), out var value) ? value : null;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }

        private static void AddParsedParameter(List<SmallModelActionParameter> parameters, string expectedEffect, string name)
        {
            var value = ParseValue(expectedEffect, name + "=");
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static (int X, int Y)? ParseCoordinate(string source, string prefix)
        {
            var value = ParseValue(source, prefix);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var parts = value.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var x) ||
                !int.TryParse(parts[1], out var y))
            {
                return null;
            }

            return (x, y);
        }

        private static string ParseValue(string source, string prefix)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            foreach (var segment in source.Split(';'))
            {
                if (segment.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return segment.Substring(prefix.Length);
                }
            }

            return string.Empty;
        }    }
}
