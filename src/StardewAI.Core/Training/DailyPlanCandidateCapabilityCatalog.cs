using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Training
{
    public sealed class DailyPlanCandidateCapability
    {
        public DailyPlanCandidateCapability(string kind, bool compilable, string blockReason = "")
        {
            Kind = kind;
            Compilable = compilable;
            BlockReason = blockReason;
        }

        public string Kind { get; }
        public bool Compilable { get; }
        public string BlockReason { get; }
    }

    public static class DailyPlanCandidateCapabilityCatalog
    {
        private static readonly IReadOnlyList<DailyPlanCandidateCapability> Catalog =
            new ReadOnlyCollection<DailyPlanCandidateCapability>(
                OptionCapabilityRegistrySource.DailyCandidates
                    .Select(row => new DailyPlanCandidateCapability(
                        row.Kind,
                        row.Compilable,
                        row.BlockReason))
                    .ToArray());

        private static readonly IReadOnlyDictionary<string, DailyPlanCandidateCapability> ByKind =
            new ReadOnlyDictionary<string, DailyPlanCandidateCapability>(
                Catalog.ToDictionary(row => row.Kind, StringComparer.Ordinal));

        public static IReadOnlyList<DailyPlanCandidateCapability> All => Catalog;

        public static IReadOnlyCollection<string> CompilableKinds { get; } =
            new ReadOnlyCollection<string>(Catalog
                .Where(row => row.Compilable)
                .Select(row => row.Kind)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static bool TryGet(string kind, out DailyPlanCandidateCapability capability)
        {
            return ByKind.TryGetValue(kind, out capability!);
        }

    }
}
