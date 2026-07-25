using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.PreviewCompiler;
using Xunit;

namespace StardewAI.Core.Tests
{
    public sealed class PlanningPreviewCompilerTests
    {
        [Fact]
        public void Compile_UsesRegisteredOptionAndSeparatesExecutionPermission()
        {
            var snapshot = Snapshot(new[]
            {
                Fields("player", ("location_id", "\"Farm\""), ("energy", "270")),
                Section("farm")
            });
            var compiler = new PlanningPreviewCompiler();

            var preview = compiler.Compile(snapshot, "water crops today", "efficiency");

            Assert.Equal("farm.maintain_crops", preview.Goal.Intent);
            Assert.Equal("farm.maintain_crops", preview.SelectedOption.OptionId);
            Assert.Equal("unknown", preview.Feasibility);
            Assert.True(preview.PreviewOnly);
            Assert.Equal("disabled", preview.ExecutionPermission);
            Assert.False(preview.WouldBeExecutable);
            Assert.Contains("farm.crops", preview.MissingStateFactors);
        }

        [Fact]
        public void Compile_FeasiblePlanStillHasDisabledExecutionPermission()
        {
            var snapshot = Snapshot(new[]
            {
                Fields("time", ("season", "\"spring\""), ("weather", "\"sun\"")),
                Fields("player", ("location_id", "\"Farm\""), ("energy", "270")),
                Field("farm", "crops", "[]")
            });
            var compiler = new PlanningPreviewCompiler();

            var preview = compiler.Compile(snapshot, "water crops today", "efficiency");

            Assert.Equal("feasible", preview.Feasibility);
            Assert.True(preview.WouldBeReadEligible);
            Assert.True(preview.WouldBind);
            Assert.False(preview.WouldCompile);
            Assert.False(preview.WouldBeExecutable);
            Assert.Equal("disabled", preview.ExecutionPermission);
            Assert.True(preview.PreviewOnly);
        }

        private static SnapshotEnvelope Snapshot(IEnumerable<string> sections)
        {
            var stateJson = "{" + string.Join(",", sections) + @",
                ""locations"": {},
                ""npcs"": {},
                ""quests"": {},
                ""world_progress"": {},
                ""menus"": {},
                ""mods"": {},
                ""modded_state"": {}
            }";
            return new SnapshotEnvelope
            {
                BridgeVersion = "test",
                GameTick = 100,
                RealTimestamp = "2026-07-04T00:00:00Z",
                StateHash = "hash-100",
                Completeness = "partial",
                State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!
            };
        }

        private static string Field(string section, string name, string valueJson)
        {
            return $@"""{section}"": {{ ""{name}"": {{
                ""value"": {valueJson},
                ""status"": ""available"",
                ""source"": {{ ""kind"": ""game_object"", ""path"": ""{section}.{name}"" }},
                ""adapter"": ""test"",
                ""read_at_tick"": 100,
                ""confidence"": 1.0
            }} }}";
        }

        private static string Fields(string section, params (string Name, string ValueJson)[] fields)
        {
            var fieldJson = string.Join(",", fields.Select(field => $@"""{field.Name}"": {{
                ""value"": {field.ValueJson},
                ""status"": ""available"",
                ""source"": {{ ""kind"": ""game_object"", ""path"": ""{section}.{field.Name}"" }},
                ""adapter"": ""test"",
                ""read_at_tick"": 100,
                ""confidence"": 1.0
            }}"));
            return $@"""{section}"": {{ {fieldJson} }}";
        }

        private static string Section(string section)
        {
            return $@"""{section}"": {{}}";
        }
    }
}
