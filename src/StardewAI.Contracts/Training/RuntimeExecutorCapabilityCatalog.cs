using System;
using System.Collections.Generic;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Contracts.Training
{
    [Obsolete("Use RuntimeTestHarnessDispatchCatalog. Harness dispatch is not product runtime support.")]
    public static class RuntimeExecutorCapabilityCatalog
    {
        public static IReadOnlyCollection<string> OptionIds =>
            RuntimeTestHarnessDispatchCatalog.OptionIds;

        public static bool IsSupported(string optionId)
        {
            return RuntimeTestHarnessDispatchCatalog.IsSupported(optionId);
        }
    }
}
