using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

public sealed partial class LiveTrainingOptions
{
    internal bool ObserveSelectedTillingResources { get; private set; }

    internal LiveTrainingOptions WithSelectedTillingResources()
    {
        var copy = (LiveTrainingOptions)MemberwiseClone();
        copy.ObserveSelectedTillingResources = true;
        copy.ExecutionSnapshotProfile = "full";
        return copy;
    }
}

namespace StardewAI.LiveTrainingLoop
{
    internal static class NativeTillingExecutionObservation
    {
        internal static void Apply(LiveTrainingOptions options, TrainingExecutionRequest request)
        {
            if (!options.ObserveSelectedTillingResources || !options.SkipTraining || !options.RequireExecutorFeedback ||
                !options.UseRuntimeTestHarnessExecutor || options.TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer ||
                options.ExecutionSnapshotProfile != "full" || request.OptionId != "executor.till_soil" ||
                request.SourcePlanningOptionId != request.OptionId)
                throw new InvalidDataException("Selected till observation requires its explicit isolated original till execution.");
            request.ActionOriginObservationScopeId = NativeOutputObservationMode.CreateIdentityScope(options, request);
            request.ActionOriginTillingResources = request.ActionOriginSnapshotAnchor = request.ActionOriginToolInputs = true;
            request.ActionOriginInventoryPayload = request.ActionOriginInventoryTransients = true;
            request.ActionOriginInventoryGraph = request.ActionOriginDebrisState = true;
            if (!NativeTillingObservationContract.IsCompleteRequest(request))
                throw new InvalidDataException("Selected till lost its original complete resource observation contract.");
        }
    }
}
