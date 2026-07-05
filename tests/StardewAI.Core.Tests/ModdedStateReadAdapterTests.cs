using System.Text.Json;
using StardewAI.TransparentBridge.Adapters;

namespace StardewAI.Core.Tests;

public sealed class ModdedStateReadAdapterTests
{
    [Fact]
    public void BuildResultReportsInstalledContentPackAndRawModState()
    {
        var result = ModdedStateReadAdapter.BuildResult(new[]
        {
            new ModdedStateModMetadata("Pathoschild.ContentPatcher", "Content Patcher", "2.7.0", "Pathoschild", false),
            new ModdedStateModMetadata("Example.Pack", "Example Pack", "1.0.0", "Example Author", true)
        }, 123, new object[]
        {
            new
            {
                raw_key = "smapi/mod-data/example.mod/state",
                mod_id = "example.mod",
                data_key = "state",
                raw_json = "{\"enabled\":true}"
            }
        }, new object[]
        {
            new
            {
                owner_kind = "player",
                owner_path = "Game1.player",
                entry_count = 1,
                entries = new Dictionary<string, string> { ["example.mod/value"] = "42" }
            }
        });

        var json = JsonSerializer.SerializeToElement(result.Fields, JsonOptions);
        var installed = json.GetProperty("installed").GetProperty("value");
        var contentPacks = json.GetProperty("content_packs").GetProperty("value");
        var privateState = json.GetProperty("private_mod_state").GetProperty("value");
        var saveData = json.GetProperty("arbitrary_mod_private_save_data").GetProperty("value");

        Assert.Equal("modded_state", result.SectionName);
        Assert.Empty(result.UnavailableFields);
        Assert.Equal(2, json.GetProperty("installed_count").GetProperty("value").GetInt32());
        Assert.Equal(1, json.GetProperty("content_pack_count").GetProperty("value").GetInt32());
        Assert.Equal("Pathoschild.ContentPatcher", installed[0].GetProperty("mod_id").GetString());
        Assert.False(installed[0].GetProperty("is_content_pack").GetBoolean());
        Assert.Equal("Example.Pack", contentPacks[0].GetProperty("mod_id").GetString());
        Assert.True(contentPacks[0].GetProperty("is_content_pack").GetBoolean());
        Assert.Equal("available", json.GetProperty("private_mod_state").GetProperty("status").GetString());
        Assert.Equal(1, privateState.GetProperty("public_mod_data_entry_count").GetInt32());
        Assert.Equal(1, privateState.GetProperty("save_data_entry_count").GetInt32());
        Assert.False(privateState.GetProperty("runtime_private_fields_exposed").GetBoolean());
        Assert.Equal("example.mod", saveData.GetProperty("entries")[0].GetProperty("mod_id").GetString());
        Assert.Equal("{\"enabled\":true}", saveData.GetProperty("entries")[0].GetProperty("raw_json").GetString());
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
