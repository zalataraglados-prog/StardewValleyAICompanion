using System;

namespace StardewAI.Core.OptionRegistry
{
    internal sealed class VanillaBasicQuestEventRoute
    {
        public string QuestId { get; init; } = string.Empty;

        public string RuntimeType { get; init; } = string.Empty;

        public string TargetLocation { get; init; } = string.Empty;

        public string EventId { get; init; } = string.Empty;

        public string RequiredMailId { get; init; } = string.Empty;

        public int StartTimeInclusive { get; init; }

        public int EndTimeInclusive { get; init; }

        public string SourceAsset { get; init; } = string.Empty;

        public string SourceKey { get; init; } = string.Empty;

        public string ScriptSha256 { get; init; } = string.Empty;

        public string Provenance =>
            "locked_vanilla_1_6_15_event_route:" + SourceAsset + ":" +
            SourceKey + ":sha256=" + ScriptSha256;
    }

    internal static class VanillaBasicQuestEventRouteCatalog
    {
        private static readonly VanillaBasicQuestEventRoute[] Rows =
        {
            new()
            {
                QuestId = "13",
                RuntimeType = "Quest",
                TargetLocation = "Beach",
                EventId = "739330",
                RequiredMailId = "spring_2_1",
                StartTimeInclusive = 600,
                EndTimeInclusive = 1710,
                SourceAsset = "Data/Events/Beach",
                SourceKey = "739330/t 600 1710/*n spring_2_1",
                ScriptSha256 =
                    "1cf2e7ceaeccb12e26eda84a484c95fb7750b4bd2f35775309a49930819223a3"
            }
        };

        public static bool TryGet(
            string questId,
            string runtimeType,
            out VanillaBasicQuestEventRoute route)
        {
            foreach (var row in Rows)
            {
                if (string.Equals(
                        row.QuestId,
                        questId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        row.RuntimeType,
                        runtimeType,
                        StringComparison.Ordinal))
                {
                    route = row;
                    return true;
                }
            }

            route = null!;
            return false;
        }
    }
}
