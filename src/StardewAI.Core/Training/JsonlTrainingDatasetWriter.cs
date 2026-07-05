using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class JsonlTrainingDatasetWriter
    {
        public TrainingDatasetAppendResult Append(string datasetPath, TrainingFeatureRowEnvelope row)
        {
            var fullPath = Path.GetFullPath(datasetPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(row, JsonOptions);
            var payload = line + Environment.NewLine;
            File.AppendAllText(fullPath, payload, Encoding.UTF8);

            return new TrainingDatasetAppendResult
            {
                DatasetPath = fullPath,
                RowId = row.RowId,
                EpisodeId = row.EpisodeId,
                BytesWritten = Encoding.UTF8.GetByteCount(payload),
                RowCount = File.ReadLines(fullPath).Count(lineItem => !string.IsNullOrWhiteSpace(lineItem))
            };
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
