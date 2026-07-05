using System;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.MockModel
{
    public sealed class MockSmallModelPolicy
    {
        private readonly TaskIntentClassifier classifier;

        public MockSmallModelPolicy()
            : this(new TaskIntentClassifier())
        {
        }

        public MockSmallModelPolicy(TaskIntentClassifier classifier)
        {
            this.classifier = classifier;
        }

        public SmallModelActionEnvelope Generate(SnapshotEnvelope snapshot, string goal, string executionMode)
        {
            var classification = classifier.Classify(goal);
            var actor = string.Equals(executionMode, "coop_companion", StringComparison.Ordinal)
                ? new ActionActorRef
                {
                    ActorId = "ai_companion.main",
                    ActorType = "ai_companion",
                    ControlSurface = "companion_actor"
                }
                : new ActionActorRef
                {
                    ActorId = "training_farmer.main",
                    ActorType = "training_farmer",
                    ControlSurface = "training_sandbox"
                };

            return new SmallModelActionEnvelope
            {
                ModelOutputId = "mock_model_output." + Guid.NewGuid().ToString("N"),
                SourceModel = "mock-small-model.rule.v1",
                StateHash = snapshot.StateHash,
                GoalId = "goal.mock." + Guid.NewGuid().ToString("N"),
                ExecutionMode = string.IsNullOrWhiteSpace(executionMode) ? "training_singleplayer" : executionMode,
                Actor = actor,
                Actions = new[]
                {
                    new SmallModelAction
                    {
                        ActionId = "mock_action." + Guid.NewGuid().ToString("N"),
                        OptionId = classification.OptionId,
                        Rationale = "mock policy classified goal as " + classification.Category,
                        Parameters = AppendCategory(classification.Parameters, classification.Category)
                    }
                }
            };
        }

        private static SmallModelActionParameter[] AppendCategory(SmallModelActionParameter[] parameters, string category)
        {
            var result = new SmallModelActionParameter[parameters.Length + 1];
            Array.Copy(parameters, result, parameters.Length);
            result[result.Length - 1] = new SmallModelActionParameter
            {
                Name = "intent_category",
                Value = category
            };
            return result;
        }
    }
}
