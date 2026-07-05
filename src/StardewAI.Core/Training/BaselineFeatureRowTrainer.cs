using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class BaselineFeatureRowTrainer
    {
        public BaselineTrainingReport Train(string datasetPath)
        {
            var fullPath = Path.GetFullPath(datasetPath);
            if (!File.Exists(fullPath))
            {
                return new BaselineTrainingReport
                {
                    DatasetPath = fullPath
                };
            }

            var rows = File.ReadLines(fullPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize<TrainingFeatureRowEnvelope>(line, JsonOptions))
                .Where(row => row is not null)
                .Select(row => row!)
                .ToArray();
            var groups = new Dictionary<string, List<TrainingFeatureRowEnvelope>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var optionId = row.ActionFeatures.OptionIds.FirstOrDefault() ?? "none";
                if (!groups.TryGetValue(optionId, out var group))
                {
                    group = new List<TrainingFeatureRowEnvelope>();
                    groups[optionId] = group;
                }

                group.Add(row);
            }

            return new BaselineTrainingReport
            {
                DatasetPath = fullPath,
                RowCount = rows.Length,
                OptionScores = groups
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => Score(pair.Key, pair.Value))
                    .ToArray()
            };
        }

        private static BaselineOptionScore Score(string optionId, IReadOnlyCollection<TrainingFeatureRowEnvelope> rows)
        {
            return new BaselineOptionScore
            {
                OptionId = optionId,
                ExampleCount = rows.Count,
                AverageGoalProgressDelta = Math.Round(rows.Average(row => row.Labels.GoalProgressDelta), 4),
                AverageTotalReward = Math.Round(rows.Average(row => row.Labels.TotalReward), 4),
                HardBlockRate = Math.Round(rows.Count(row => row.Labels.HardBlocked) / (double)rows.Count, 4)
            };
        }

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    }
}
