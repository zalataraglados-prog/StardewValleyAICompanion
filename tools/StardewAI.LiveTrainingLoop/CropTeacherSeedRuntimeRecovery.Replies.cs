using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

internal interface ICropSeedPortfolioRecoveryOptions
{
    string BackendUrl { get; }
    string PortfolioJournalDirectory { get; }
}

public static partial class CropTeacherSeedRuntimeRecovery
{
    internal static async Task<JsonObject> ReadReplyAsync<T>(ICropSeedPortfolioRecoveryOptions options,
        string origin, string root, string output, Artifacts artifacts, string phase, string operation, T request,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        var (requestPath, journalPath, hash, expected) = PortfolioMutationRequestJournal.Describe(
            options.BackendUrl, options.PortfolioJournalDirectory, operation, request, JsonOptions);
        artifacts.Pin(requestPath);
        artifacts.Pin(journalPath);
        var original = await PortfolioMutationRequestJournal.ReadAsync(journalPath, requestPath, expected, hash);
        var local = Path.Combine(root, phase + "-reply.json");
        if (File.Exists(local)) return artifacts.Read<JsonObject>(local);
        artifacts.Validate();
        var lookup = await post(origin + "/api/v1/strategy/commitments/reservation-portfolios/lookup-mutation",
            new JsonObject { ["operation"] = operation, ["request"] = JsonNode.Parse(original) }.ToJsonString(JsonOptions));
        var observationPath = Path.Combine(output, phase + "-lookup.json");
        await write(observationPath, lookup.ToJsonString(JsonOptions));
        artifacts.Pin(observationPath);
        if (lookup["schema_version"]?.GetValue<string>() != "stardewai.portfolio_mutation_lookup.v1" ||
            lookup["status"]?.GetValue<string>() != "found" || lookup["execution_performed"]?.GetValue<bool>() != false ||
            lookup["ledger_mutation_performed"]?.GetValue<bool>() != false || lookup["native_outcome_verified"]?.GetValue<bool>() != false ||
            lookup["formal_product_training"]?.GetValue<bool>() != false || lookup["requires_independent_verification"]?.GetValue<bool>() != true ||
            lookup["reply"] is not JsonObject reply)
            throw new InvalidDataException("Original crop " + phase + " reply is not recoverable from its original journal.");
        return (JsonObject)reply.DeepClone();
    }
}
