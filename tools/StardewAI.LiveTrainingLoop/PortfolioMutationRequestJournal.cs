using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

internal static class PortfolioMutationRequestJournal
{
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
}
