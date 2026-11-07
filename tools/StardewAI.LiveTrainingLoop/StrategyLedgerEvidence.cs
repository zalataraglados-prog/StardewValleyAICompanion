using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed record StrategyLedgerEvidence(string Path, StrategyCommitmentLedger Ledger);

public static class StrategyLedgerEvidenceStore
{
    public static async Task<StrategyLedgerEvidence> ReadAsync(HttpClient http, string backendUrl,
        string evidenceRoot, string stateHash, JsonSerializerOptions options, string label)
    {
        var node = await http.GetFromJsonAsync<JsonObject>(backendUrl +
            "/api/v1/strategy/commitments/latest?stateHash=" + Uri.EscapeDataString(stateHash))
            ?? throw new InvalidDataException(label + " ledger response is empty.");
        return await SaveAsync(node, evidenceRoot, options, label);
    }

    public static async Task<StrategyLedgerEvidence> SaveAsync(JsonObject node, string evidenceRoot,
        JsonSerializerOptions options, string label)
    {
        var ledger = node.Deserialize<StrategyCommitmentLedger>(options)
            ?? throw new InvalidDataException(label + " ledger is invalid.");
        var json = node.ToJsonString(options);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var directory = System.IO.Path.Combine(evidenceRoot, "teacher-ledgers");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, digest + ".json");
        if (File.Exists(path))
        {
            if (await File.ReadAllTextAsync(path) != json)
                throw new InvalidDataException("Immutable Teacher ledger evidence changed.");
        }
        else
            await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));
        return new StrategyLedgerEvidence(path, ledger);
    }
}
