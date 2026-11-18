using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

public sealed partial class LiveTrainingOptions
{
    public bool UseManagedWateringResources { get; set; }
    internal bool ObserveSelectedWateringResources { get; private set; }

    internal LiveTrainingOptions WithSelectedWateringResources()
    {
        var copy = (LiveTrainingOptions)MemberwiseClone();
        copy.ObserveSelectedWateringResources = true;
        copy.ExecutionSnapshotProfile = "full";
        return copy;
    }
}

namespace StardewAI.LiveTrainingLoop
{
    internal static class NativeWateringExecutionObservation
    {
        internal static void Apply(LiveTrainingOptions options, TrainingExecutionRequest request)
        {
            if (!options.ObserveSelectedWateringResources || options.ObserveSelectedTillingResources || !options.SkipTraining ||
                !options.RequireExecutorFeedback || !options.UseRuntimeTestHarnessExecutor ||
                options.TargetExecutionMode != ExecutionTargetProfiles.TrainingSingleplayer ||
                options.ExecutionSnapshotProfile != "full" || request.OptionId != "executor.water_crop" ||
                request.SourcePlanningOptionId != request.OptionId || request.ActionOriginTillingResources)
                throw new InvalidDataException("Selected watering observation requires its explicit isolated original watering execution.");
            request.ActionOriginObservationScopeId = NativeOutputObservationMode.CreateIdentityScope(options, request);
            request.ActionOriginWateringResources = request.ActionOriginSnapshotAnchor = request.ActionOriginToolInputs = true;
            request.ActionOriginInventoryPayload = request.ActionOriginInventoryTransients = true;
            request.ActionOriginInventoryGraph = request.ActionOriginDebrisState = true;
            if (!NativeWateringObservationContract.IsCompleteRequest(request))
                throw new InvalidDataException("Selected watering lost its complete resource observation contract.");
        }
    }
}
