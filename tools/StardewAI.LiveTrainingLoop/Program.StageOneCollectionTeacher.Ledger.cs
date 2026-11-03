using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static async Task<(string? Path, StrategyCommitmentLedger? Ledger)> ReadStageOneTeacherLedgerAsync(
        HttpClient http, LiveTrainingOptions options, string stateHash, AvailabilityAwarePolicyPredictionEnvelope ranking)
    {
        if (!ranking.RankedEventCandidates.Any(candidate => candidate.OptionId == "museum.donate_items"))
            return (null, null);
        var node = await http.GetFromJsonAsync<JsonObject>(options.BackendUrl +
            "/api/v1/strategy/commitments/latest?stateHash=" + Uri.EscapeDataString(stateHash))
            ?? throw new InvalidDataException("Stage-one donation ledger response is empty.");
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(node.ToJsonString(JsonOptions), JsonOptions)
            ?? throw new InvalidDataException("Stage-one donation ledger is invalid.");
        var json = node.ToJsonString(JsonOptions);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var directory = Path.Combine(options.SnapshotDir, "teacher-ledgers");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, digest + ".json");
        if (File.Exists(path))
        {
            if (await File.ReadAllTextAsync(path) != json)
                throw new InvalidDataException("Immutable Teacher ledger evidence changed.");
        }
        else
            await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));
        return (path, ledger);
    }
}
