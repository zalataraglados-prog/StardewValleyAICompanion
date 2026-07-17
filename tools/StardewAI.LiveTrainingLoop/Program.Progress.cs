using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static void AppendProgress(LiveTrainingOptions options, string stage, int iteration, string stateHash, string queueId, string detail)
    {
        var line = string.Join(" ", new[]
        {
            DateTimeOffset.Now.ToString("O"),
            "stage=" + stage,
            "iteration=" + iteration,
            "run_id=" + options.RunId,
            "state_hash=" + stateHash,
            "queue_id=" + queueId,
            detail
        });
        File.AppendAllText(options.ProgressLogPath, line + Environment.NewLine);
    }
}
