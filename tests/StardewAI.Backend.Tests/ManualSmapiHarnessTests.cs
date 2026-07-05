using System.Text.RegularExpressions;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class ManualSmapiHarnessTests
    {
        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

        [Fact]
        public void ManualAcceptanceScriptUsesReadOnlyBridgeEndpoints()
        {
            var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "Invoke-SmapiRuntimeAcceptance.ps1"));

            Assert.Contains("/api/v1/snapshot", script);
            Assert.Contains("/api/v1/capabilities", script);
            Assert.Contains("/api/v1/events", script);
            Assert.DoesNotContain("InputSimulator", script);
            Assert.DoesNotContain("performClick", script);
            Assert.DoesNotContain("DoFunction", script);
            Assert.DoesNotContain("SaveGame", script);
            Assert.DoesNotContain("Start-Process", script);
            Assert.Contains("Invoke-JsonGet \"$BridgeBaseUrl/api/v1/snapshot\"", script);
            Assert.DoesNotMatch(new Regex(@"Invoke-RestMethod\s+-Method\s+(Put|Patch|Delete)", RegexOptions.IgnoreCase), script);
            Assert.DoesNotMatch(new Regex(@"\$BridgeBaseUrl[^\r\n]+Invoke-JsonPost", RegexOptions.IgnoreCase), script);
        }

        [Fact]
        public void ManualAcceptanceScriptChecksTransparentRuntimeShapes()
        {
            var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "Invoke-SmapiRuntimeAcceptance.ps1"));

            foreach (var envelopeProperty in new[] { "value", "status", "source", "adapter", "read_at_tick", "confidence" })
            {
                Assert.Contains(envelopeProperty, script);
            }

            Assert.Contains("can_write_game_state must be false", script);
            Assert.Contains("can_execute_commands must be false", script);
            Assert.Contains("changed_fields is required", script);
            Assert.Contains("event_stream.v1", script);
            Assert.Contains("event_hash", script);
            Assert.Contains("previous_event_hash", script);
            Assert.Contains("latest_snapshot_hash", script);
            Assert.Contains("hash_match_after_ingest", script);
        }

        [Fact]
        public void ManualAcceptanceScriptSupportsBackgroundRuntimeInputs()
        {
            var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "Invoke-SmapiRuntimeAcceptance.ps1"));
            var doc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "smapi-runtime-acceptance.md"));

            Assert.Contains("IsolatedStardewDirectory", script);
            Assert.Contains("isolated_stardew_directory", script);
            Assert.Contains("BridgeUrl", script);
            Assert.Contains("BackendUrl", script);
            Assert.Contains("ArtifactsDirectory", script);
            Assert.Contains("IsolatedStardewDirectory", doc);
            Assert.Contains("BridgeUrl", doc);
            Assert.Contains("BackendUrl", doc);
            Assert.Contains("ArtifactsDirectory", doc);
            Assert.Contains("does not launch SMAPI", doc);
        }

        [Fact]
        public void ManualAcceptanceDocCarriesLocalDecompileEvidenceAndRuntimeCaveat()
        {
            var doc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "smapi-runtime-acceptance.md"));

            Assert.Contains(@"I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs", doc);
            Assert.Contains(@"I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs", doc);
            Assert.Contains(@"I:\StardewValleyAICompanion-decompile\SMAPI\StardewModdingAPI.Events\InventoryChangedEventArgs.cs", doc);
            Assert.Contains("not_executed", doc);
            Assert.Contains("真实游戏运行时验收尚未执行", doc);
        }
    }
}
