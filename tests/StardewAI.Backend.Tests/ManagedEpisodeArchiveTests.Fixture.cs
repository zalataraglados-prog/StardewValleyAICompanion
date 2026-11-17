using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class ManagedEpisodeArchiveTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // Packaging fixtures only: fresh failed witnesses never claim native progress or a full schedule.
    private sealed class Fixture
    {
        internal LiveTrainingOptions Options { get; }
        internal string Root => Options.SnapshotDir;
        internal string EpisodePath => Path.Combine(Root, "plan-execution-episode-0001.json");
        internal string BeforePath { get; }
        internal PlanExecutionEpisodeEnvelope Episode { get; }
        internal string OriginalEpisode { get; private set; } = string.Empty;
        internal string OriginalBefore => ContentAddressedJsonArtifactReader.ReadAllText(BeforePath);

        internal Fixture(bool nested = true, string mode = "plain")
        {
            var root = Path.Combine(Path.GetTempPath(), "managed-archive-" + Guid.NewGuid().ToString("N"));
            Options = new LiveTrainingOptions { Root = root, ManifestPath = Path.Combine(root, "run.json"),
                RunId = "managed-archive-fixture", UseManagedCropTeacher = true, CaptureGoldenWalnutFreshSaveEvidence = true,
                SnapshotArtifactMode = mode };
            Directory.CreateDirectory(Root);
            BeforePath = nested ? Path.Combine(Root, "planning-handoff-0002", "source.json") : Path.Combine(Root, "before.json");
            Directory.CreateDirectory(Path.GetDirectoryName(BeforePath)!);
            var before = Snapshot();
            ContentAddressedJsonArtifactStore.WriteAsync(BeforePath, JsonSerializer.Serialize(before, JsonOptions), mode).GetAwaiter().GetResult();
            var afterPath = Path.Combine(Root, "after.json");
            before.GameTick++;
            File.WriteAllText(afterPath, JsonSerializer.Serialize(before, JsonOptions));
            var receiptPath = Path.Combine(Root, "execution.json");
            File.WriteAllText(receiptPath, "{\"status\":\"blocked\",\"primitive_verification_status\":\"unverified\"}");
            Episode = new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = "managed-archive-episode", RunId = Options.RunId, SourceStateHash = before.StateHash,
                AfterStateHash = before.StateHash, BeforeGameTick = 1, AfterGameTick = 2, AfterSnapshotFresh = true,
                BeforeSnapshotPath = BeforePath, AfterSnapshotPath = afterPath, ExecutionResultPath = receiptPath,
                Status = "blocked", Success = false, PrimitiveVerificationStatus = "unverified",
                ChangedFacts = JsonSerializer.SerializeToElement(Array.Empty<object>())
            };
            SaveEpisode();
        }

        internal void SaveEpisode()
        {
            OriginalEpisode = JsonSerializer.Serialize(Episode, JsonOptions);
            File.WriteAllText(EpisodePath, OriginalEpisode);
        }

        internal GoldenWalnutFreshSaveEpisodeArchiveResult Capture() =>
            (GoldenWalnutFreshSaveEpisodeArchiveResult)typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
                .GetMethod("CaptureGoldenWalnutPlanExecutionEpisode", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [Options, EpisodePath, 1])!;
        internal string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        internal string AliasPath => Path.Combine(Root, "golden-walnut-managed-before-" +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OriginalBefore))).ToLowerInvariant() + ".json");
    }

    private static SnapshotEnvelope Snapshot() => FreshSaveBoundarySnapshotFixture.Create();
}
