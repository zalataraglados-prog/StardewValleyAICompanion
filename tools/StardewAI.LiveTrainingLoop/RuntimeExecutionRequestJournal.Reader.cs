using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.LiveTrainingLoop;

public static partial class RuntimeExecutionRequestJournal
{
    public static async Task<(RuntimeExecutionRequestRecord Record, string RequestJson)> ReadAsync(string path)
    {
        var verified = await ExecutionRequestJournalReader.ReadAsync(path);
        var record = verified.Metadata.Deserialize<RuntimeExecutionRequestRecord>(JsonOptions)
            ?? throw new InvalidDataException("Execution request journal is missing.");
        return (record with { JournalPath = verified.JournalPath }, verified.RequestJson);
    }
}
