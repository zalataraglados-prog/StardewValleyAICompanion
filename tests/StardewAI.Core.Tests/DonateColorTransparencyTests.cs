using System.Text.Json;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class DonateColorTransparencyTests
{
    [Fact]
    public void DonateMatcherUsesExactPreservedParentBaseColorTags()
    {
        using var document = JsonDocument.Parse("""
        {
          "runtime_type":"StardewValley.Objects.ColoredObject",
          "context_tags":["artisan_good","color_blue"],
          "donate_color_context":{
            "is_colored_object":true,
            "preserved_parent_item_id":"613",
            "preserved_parent_base_context_tags":["color_red","item_apple"],
            "projection_status":"exact_native_preserved_parent_base_context_tags"
          }
        }
        """);

        Assert.True(QuestContextTagMatcher.MatchesDonateObjective(
            document.RootElement,
            new[] { "color_red/color_dark_red" }));
        Assert.False(QuestContextTagMatcher.MatchesDonateObjective(
            document.RootElement,
            new[] { "color_blue" }));
    }

    [Fact]
    public void DonateMatcherFailsClosedForColoredItemWithoutParentProjection()
    {
        using var document = JsonDocument.Parse("""
        {
          "runtime_type":"StardewValley.Objects.ColoredObject",
          "context_tags":["color_red"]
        }
        """);

        Assert.False(QuestContextTagMatcher.MatchesDonateObjective(
            document.RootElement,
            new[] { "color_red" }));

        using var contradictory = JsonDocument.Parse("""
        {
          "runtime_type":"StardewValley.Objects.ColoredObject",
          "context_tags":["color_red"],
          "donate_color_context":{
            "is_colored_object":false,
            "preserved_parent_item_id":null,
            "preserved_parent_base_context_tags":[],
            "projection_status":"not_applicable_not_colored_object"
          }
        }
        """);
        Assert.False(QuestContextTagMatcher.MatchesDonateObjective(
            contradictory.RootElement,
            new[] { "color_red" }));
    }

    [Fact]
    public void OrdinaryObjectivesStillMatchCurrentItemColorAndNegatedTags()
    {
        using var document = JsonDocument.Parse("""
        {
          "runtime_type":"StardewValley.Object",
          "context_tags":["color_red","category_fruit"]
        }
        """);

        Assert.True(QuestContextTagMatcher.Matches(
            document.RootElement,
            new[] { "color_red,!category_vegetable" }));
        Assert.True(QuestContextTagMatcher.MatchesDonateObjective(
            document.RootElement,
            new[] { "color_red" }));

        using var coloredWithoutParent = JsonDocument.Parse("""
        {
          "runtime_type":"StardewValley.Objects.ColoredObject",
          "context_tags":["color_red"],
          "donate_color_context":{
            "is_colored_object":true,
            "preserved_parent_item_id":null,
            "preserved_parent_base_context_tags":[],
            "projection_status":"not_applicable_no_preserved_parent"
          }
        }
        """);
        Assert.True(QuestContextTagMatcher.MatchesDonateObjective(
            coloredWithoutParent.RootElement,
            new[] { "color_red" }));
    }

    [Fact]
    public void InventoryProjectionReadsNativePreservedParentBaseTagsWithoutMergingThem()
    {
        var adapter = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.cs"));
        var colorProjection = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.InventoryColor.cs"));

        Assert.Contains("donate_color_context = ReadDonateColorContext(item)", adapter, StringComparison.Ordinal);
        Assert.Contains("coloredObject?.preservedParentSheetIndex.Value", colorProjection, StringComparison.Ordinal);
        Assert.Contains("ItemContextTagManager.GetBaseContextTags(parentItemId)", colorProjection, StringComparison.Ordinal);
        Assert.Contains("exact_native_preserved_parent_base_context_tags", colorProjection, StringComparison.Ordinal);
        Assert.DoesNotContain("context_tags = ReadDonateColorContext", adapter, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeQuestMatrixUsesNativeQiColorOrderAndNativeValidityProbe()
    {
        var fixture = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.QuestTerminalFixture.cs"));
        var smoke = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeQuestTerminalDailyPlanSmoke.ps1"));

        Assert.Contains("SpecialOrder.GetSpecialOrder(questKey, 0)", fixture, StringComparison.Ordinal);
        Assert.Contains("ItemContextTagManager.GetBaseContextTags(parentItemId)", fixture, StringComparison.Ordinal);
        Assert.Contains("donate.IsValidItem(coloredObject)", fixture, StringComparison.Ordinal);
        Assert.Contains("location.checkEventPrecondition(pair.Key)", fixture, StringComparison.Ordinal);
        Assert.Contains("QuestKey = \"QiChallenge12\"", smoke, StringComparison.Ordinal);
        Assert.Contains("RequiredTagPrefix = \"color_red\"", smoke, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException("Repository file not found: " + Path.Combine(parts));
    }
}
