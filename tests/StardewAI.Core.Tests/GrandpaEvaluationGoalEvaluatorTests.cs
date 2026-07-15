using System.Text.Json;
using StardewAI.Contracts.WorldModel;
using StardewAI.Core.Goals;

namespace StardewAI.Core.Tests;

public sealed class GrandpaEvaluationGoalEvaluatorTests
{
    [Fact]
    public void EvaluateReportsFourCandlesAtTwelvePoints()
    {
        var model = Model(
            player: """
            {
              "total_money_earned": 1000000,
              "has_skull_key": true,
              "has_rusty_key": true,
              "married_or_roommate": false,
              "farmhouse_upgrade_level": 1,
              "level": 25,
              "active_object_qualified_id": "(O)72"
            }
            """,
            worldProgress: """
            {
              "achievements": [5, 26, 34],
              "community_center": { "location_accessible": true, "completed": true },
              "joja_membership": false
            }
            """,
            npcs: """
            {
              "friendships": [
                {"npc_name":"A","points":2000},
                {"npc_name":"B","points":2000},
                {"npc_name":"C","points":2000},
                {"npc_name":"D","points":2000},
                {"npc_name":"E","points":2000}
              ]
            }
            """,
            quests: """
            {
              "mail_received": ["petLoveMessage"]
            }
            """,
            game: """
            {
              "year": 3
            }
            """,
            farm: """
            {
              "grandpa_score": 3
            }
            """);

        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(model);

        Assert.True(report.TargetMet);
        Assert.Equal(4, report.CurrentCandles);
        Assert.True(report.CurrentScore >= 12);
        Assert.Empty(report.MissingFactPaths);
        Assert.True(report.EvaluationContext.ReevaluationAvailable);
        Assert.True(report.EvaluationContext.HoldingReevaluationItem);
        Assert.Contains(report.Factors, factor => factor.Id == "money_1000000" && factor.Points == 2);
    }

    [Fact]
    public void EvaluateReportsMissingFactsWithoutGuessingScore()
    {
        var model = Model(player: "{}", worldProgress: "{}", npcs: "{}", quests: "{}");

        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(model);

        Assert.False(report.TargetMet);
        Assert.Equal(1, report.CurrentCandles);
        Assert.Contains("player.total_money_earned", report.MissingFactPaths);
        Assert.Contains(report.Factors, factor => factor.Id == "money_50000" && !factor.Known && factor.Points == 0);
    }

    private static WorldModelEnvelope Model(string player, string worldProgress, string npcs, string quests, string game = "{}", string farm = "{}")
    {
        return new WorldModelEnvelope
        {
            Facts = new WorldModelFacts
            {
                Game = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(game, JsonOptions)!,
                Player = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(player, JsonOptions)!,
                Farm = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(farm, JsonOptions)!,
                WorldProgress = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(worldProgress, JsonOptions)!,
                Npcs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(npcs, JsonOptions)!,
                Quests = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(quests, JsonOptions)!
            }
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
