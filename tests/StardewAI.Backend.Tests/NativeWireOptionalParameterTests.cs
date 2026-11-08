using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class NativeWireOptionalParameterTests
{
    [Theory]
    [InlineData("artifact_spots_dug_before")]
    [InlineData("defense_book_mail_before")]
    public void OriginalMapperNullableEmptyPlaceholderIsNotAChangedWireValue(string name)
    {
        var (wire, command) = Fixture(name, "");
        Assert.Null(wire[name]);
        Verify(wire, command);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("zero")]
    [InlineData("nonempty")]
    [InlineData("nonnullable")]
    [InlineData("string-null")]
    [InlineData("conflict")]
    public void EmptyPlaceholderCompatibilityNeverAcceptsMissingOrDifferentValue(string change)
    {
        var (wire, command) = Fixture("artifact_spots_dug_before", "");
        switch (change)
        {
            case "missing": wire.Remove("artifact_spots_dug_before"); break;
            case "zero": wire["artifact_spots_dug_before"] = 0; break;
            case "nonempty": command.Parameters[0].Value = "0"; break;
            case "nonnullable": command.Parameters[0].Name = "max_crops"; wire["max_crops"] = null; break;
            case "string-null": command.Parameters[0].Name = "required_tool_kind"; wire["required_tool_kind"] = null; break;
            case "conflict": command.Parameters = [command.Parameters[0], new() { Name = "artifact_spots_dug_before", Value = "0" }]; break;
        }
        var error = Assert.Throws<TargetInvocationException>(() => Verify(wire, command));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void ExistingExplicitNullableZeroKeepsItsExactWireMeaning()
    {
        var (wire, command) = Fixture("artifact_spots_dug_before", "0");
        Assert.Equal(0, wire["artifact_spots_dug_before"]!.GetValue<int>());
        Verify(wire, command);
    }

    private static (JsonObject Wire, NormalizedCommand Command) Fixture(string name, string value)
    {
        var command = new NormalizedCommand { Parameters = [new() { Name = name, Value = value }] };
        var item = new JsonObject { ["queue_item_id"] = "clear-item", ["option_id"] = "executor.clear_obstacle",
            ["normalized_command"] = JsonSerializer.SerializeToNode(command) };
        var request = (TrainingExecutionRequest)typeof(LiveTrainingOptions).Assembly.GetType("Program")!
            .GetMethod("BuildExecutionRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
                new object[] { new LiveTrainingOptions(), item, new string('a', 64), "original-queue", "executor.clear_obstacle" })!;
        return (JsonSerializer.SerializeToNode(request)!.AsObject(), command);
    }

    private static void Verify(JsonObject wire, NormalizedCommand command) =>
        typeof(CropTeacherRefillApproachNativeTrace).Assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader")!
            .GetMethod("ValidateWireParameters", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { JsonSerializer.SerializeToElement(wire), command });
}
