using System.Text.Json;
using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class ModdedStateReadAdapterTests
{
    [Fact]
    public void BuildResultReportsInstalledAndContentPackMetadataOnly()
    {
        var result = ModdedStateReadAdapter.BuildResult(new[]
        {
            new ModdedStateModMetadata("Pathoschild.ContentPatcher", "Content Patcher", "2.7.0", "Pathoschild", false),
            new ModdedStateModMetadata("Example.Pack", "Example Pack", "1.0.0", "Example Author", true)
        }, 123);

        var json = JsonSerializer.SerializeToElement(result.Fields, JsonOptions);
        var installed = json.GetProperty("installed").GetProperty("value");
        var contentPacks = json.GetProperty("content_packs").GetProperty("value");

        Assert.Equal("modded_state", result.SectionName);
        Assert.Equal(2, json.GetProperty("installed_count").GetProperty("value").GetInt32());
        Assert.Equal(1, json.GetProperty("content_pack_count").GetProperty("value").GetInt32());
        Assert.Equal("Pathoschild.ContentPatcher", installed[0].GetProperty("mod_id").GetString());
        Assert.False(installed[0].GetProperty("is_content_pack").GetBoolean());
        Assert.Equal("Example.Pack", contentPacks[0].GetProperty("mod_id").GetString());
        Assert.True(contentPacks[0].GetProperty("is_content_pack").GetBoolean());
    }

    [Fact]
    public void BuildResultMarksArbitraryPrivateModStateUnavailable()
    {
        var result = ModdedStateReadAdapter.BuildResult(Array.Empty<ModdedStateModMetadata>(), 456);

        var json = JsonSerializer.SerializeToElement(result.Fields, JsonOptions);
        var privateState = json.GetProperty("private_mod_state");

        Assert.Contains("modded_state.private_mod_state", result.UnavailableFields);
        Assert.Contains("modded_state.arbitrary_mod_private_save_data", result.UnavailableFields);
        Assert.Equal("unavailable", privateState.GetProperty("status").GetString());
        Assert.Equal("transparent_unavailable", privateState.GetProperty("adapter").GetString());
        Assert.Equal("arbitrary_mod_private_state_unavailable_without_mod_specific_read_only_api", privateState.GetProperty("reason").GetString());
        Assert.Equal("unavailable", privateState.GetProperty("source").GetProperty("kind").GetString());
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
