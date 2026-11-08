using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

internal static class PortfolioMutationRequestJournal
{
    internal static (string RequestPath, string JournalPath, string Hash, JsonObject Expected) Describe<T>(
        string backendUrl, string journalDirectory, string operation, T request, JsonSerializerOptions jsonOptions)
    {
        var hash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, jsonOptions)));
        var stem = Path.Combine(Path.GetFullPath(journalDirectory), "portfolio-mutation-" + hash);
        var path = stem + "-rpc-request.json";
        return (path, stem + "-rpc-journal.json", hash, new JsonObject
        {
            ["schema_version"] = "stardewai.portfolio_rpc_journal.v1", ["backend_url"] = backendUrl,
            ["operation"] = operation, ["request_file"] = Path.GetFileName(path), ["request_sha256"] = hash
        });
    }

    internal static async Task<byte[]> ReadAsync(string journalPath, string requestPath,
        JsonObject expectedJournal, string expectedHash)
    {
        foreach (var path in new[] { journalPath, requestPath })
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Portfolio journal must be a regular file.");
        if (new FileInfo(journalPath).Length > 16 * 1024 ||
            new FileInfo(requestPath).Length > RuntimeExecutionRequestJournal.MaximumRequestBytes ||
            !JsonNode.DeepEquals(expectedJournal, JsonNode.Parse(await File.ReadAllTextAsync(journalPath))))
            throw new InvalidDataException("Portfolio journal identity drifted.");
        var original = await File.ReadAllBytesAsync(requestPath);
        if (Hash(original) != expectedHash)
            throw new InvalidDataException("Portfolio original request hash drifted.");
        return original;
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
