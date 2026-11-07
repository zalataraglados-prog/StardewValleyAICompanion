using System.Text;
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
        var raw = JsonSerializer.Serialize(request, JsonOptions);
        var hash = PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(raw));
        var stem = Path.Combine(Path.GetFullPath(options.PortfolioJournalDirectory), "portfolio-mutation-" + hash);
        var requestPath = stem + "-rpc-request.json";
        var journalPath = stem + "-rpc-journal.json";
        var expected = new JsonObject
        {
            ["schema_version"] = "stardewai.portfolio_rpc_journal.v1", ["backend_url"] = options.BackendUrl,
            ["operation"] = operation, ["request_file"] = Path.GetFileName(requestPath), ["request_sha256"] = hash
        };
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
