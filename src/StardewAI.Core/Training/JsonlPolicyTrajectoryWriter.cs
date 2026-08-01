using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class JsonlPolicyTrajectoryWriter
{
    public PolicyTrajectoryAppendResult Append(
        string datasetPath,
        PolicyDecisionTrajectoryEnvelope trajectory)
    {
        var fullPath = Path.GetFullPath(datasetPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.AppendAllText(
            fullPath,
            JsonSerializer.Serialize(trajectory, JsonOptions) + Environment.NewLine);
        return new PolicyTrajectoryAppendResult
        {
            DatasetPath = fullPath,
            TrajectoryId = trajectory.TrajectoryId,
            RowCount = File.ReadLines(fullPath).Count(line => !string.IsNullOrWhiteSpace(line))
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
