using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeInventoryTransientBoundaryTests
{
    [Fact]
    public void SingleCodeUnitsAndValidPairsAgreeWithLosslessJsonRoundTrip()
    {
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var text = new string((char)code, 1);
            var actual = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(text));
            Assert.Equal(text == actual, NativeObservationText.IsWellFormedUtf16(text));
        }
        foreach (var text in new[] { "", "\0\r\n", "\u4e2d\u6587", "\ud83d\ude00", "a\ud83d\ude00b" })
        {
            Assert.True(NativeObservationText.IsWellFormedUtf16(text));
            Assert.Equal(text, JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(text)));
        }
    }

    [Theory]
    [InlineData("flag")][InlineData("key")][InlineData("value")][InlineData("tag")]
    [InlineData("description")][InlineData("display")][InlineData("qualified-cache")]
    public void RejectsMalformedRawTextRatherThanNormalizingTheNativeWitness(string field)
    {
        var (request, receipt) = Fixture();
        var item = receipt.InventoryPayloadBefore!.Slots![0].TransientState!;
        var tool = receipt.InventoryPayloadBefore.Slots[1].TransientState!.Tool!;
        var malformed = new string((char)0xD800, 1);
        switch (field)
        {
            case "flag": item.SetFlagOnPickup = Text(malformed); break;
            case "key": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = malformed, Kind = "null" }]; break;
            case "value": item.TempDataPresent = true; item.TempDataEntries = [new() { Key = "x", Kind = "string", StringValue = malformed }]; break;
            case "tag": item.Cache!.ContextTagsPresent = true; item.Cache.ContextTags = [malformed]; break;
            case "description": tool.Description = Text(malformed); break;
            case "display": tool.DisplayName = Text(malformed); break;
            case "qualified-cache": item.Cache!.QualifiedItemId = Text(malformed); break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }
}
