using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Verifier
{
    public sealed class Verifier
    {
        private readonly RequiredFactGate requiredFactGate = new RequiredFactGate();

        public SafetyResult Verify(SnapshotEnvelope snapshot, OptionSpec option)
        {
            return requiredFactGate.Evaluate(snapshot, option);
        }
    }
}
