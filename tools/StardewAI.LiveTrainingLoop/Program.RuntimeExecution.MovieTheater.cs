using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyMovieTheaterRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId != "executor.watch_movie")
            return;
        request.MovieStage = ReadQueueParameterString(item, "movie_stage");
        request.MovieProjectionFingerprint = ReadQueueParameterString(item, "movie_projection_fingerprint");
        request.MovieId = ReadQueueParameterString(item, "continuation.movie_id");
        request.MovieGuestName = ReadQueueParameterString(item, "continuation.movie_guest_name");
        request.MovieConcessionId = ReadQueueParameterString(item, "continuation.movie_concession_id");
        request.MovieObjectiveKey = ReadQueueParameterString(item, "continuation.movie_objective_key");
        request.MovieFriendshipEffective = ReadQueueParameterInt(item, "continuation.movie_friendship_effective");
        request.MovieConcessionFriendshipEffective = ReadQueueParameterInt(item, "continuation.movie_concession_friendship_effective");
        request.MovieTicketSlotIndex = ReadQueueParameterInt(item, "movie_ticket_slot_index");
        request.MovieTicketStackBefore = ReadQueueParameterInt(item, "movie_ticket_stack_before");
        request.MovieActionRaw = ReadQueueParameterString(item, "movie_action_raw");
        request.MovieActionToken = ReadQueueParameterString(item, "movie_action_token");
    }
}
