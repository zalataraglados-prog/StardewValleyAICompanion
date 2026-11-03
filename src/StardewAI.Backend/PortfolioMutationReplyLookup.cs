using System.Text.Json;
using System.Text.Json.Nodes;

public interface IPortfolioMutationReplyReader
{
    JsonObject LookupPortfolioMutationReply(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot, string operation, JsonElement request);
}

public sealed class PortfolioMutationReplyLookupRequest
{
    public string Operation { get; set; } = string.Empty;
    public JsonElement Request { get; set; }
}

public static class PortfolioMutationReplyLookup
{
    public static JsonObject Observation(string status, string? detail = null, JsonObject? reply = null) => new()
    {
        ["schema_version"] = "stardewai.portfolio_mutation_lookup.v1",
        ["status"] = status,
        ["detail"] = detail,
        ["reply"] = reply,
        ["execution_performed"] = false,
        ["ledger_mutation_performed"] = false,
        ["native_outcome_verified"] = false,
        ["requires_independent_verification"] = true,
        ["formal_product_training"] = false
    };

    public static void MapPortfolioMutationReplyLookup(this WebApplication app)
    {
        app.MapPost("/api/v1/strategy/commitments/reservation-portfolios/lookup-mutation",
            (PortfolioMutationReplyLookupRequest lookup, StateStore store, IStrategyCommitmentRepository repository) =>
            {
                if (!FileStrategyCommitmentRepository.TryNormalizePortfolioRequest(
                        lookup.Operation, lookup.Request, out _, out var stateHash) ||
                    !store.Snapshots.TryGetValue(stateHash, out var snapshot))
                    return Results.UnprocessableEntity(Observation("rejected", "original request/snapshot unavailable"));
                return Results.Ok(repository is IPortfolioMutationReplyReader reader
                    ? reader.LookupPortfolioMutationReply(snapshot, lookup.Operation, lookup.Request)
                    : Observation("unavailable", "repository does not retain mutation replies"));
            });
    }
}
