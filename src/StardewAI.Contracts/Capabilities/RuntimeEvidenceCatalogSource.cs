using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Capabilities
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RuntimeEvidenceFreshnessStatus
    {
        LegacyUnbound,
        Current,
        StaleOrMissing
    }

    public sealed class RuntimePathIdentity
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; internal set; } = RuntimeEvidenceCatalogSource.SchemaVersion;

        [JsonPropertyName("runtime_path_revision")]
        public string RuntimePathRevision { get; internal set; } = string.Empty;

        [JsonPropertyName("runtime_source_sha256")]
        public string RuntimeSourceSha256 { get; internal set; } = string.Empty;

        [JsonPropertyName("runtime_source_paths")]
        public string[] RuntimeSourcePaths { get; internal set; } = Array.Empty<string>();
    }

    public sealed class RuntimeEvidenceBinding
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; internal set; } = RuntimeEvidenceCatalogSource.SchemaVersion;

        [JsonPropertyName("evidence_id")]
        public string EvidenceId { get; internal set; } = string.Empty;

        [JsonPropertyName("artifact_id")]
        public string ArtifactId { get; internal set; } = string.Empty;

        [JsonPropertyName("source_commit")]
        public string SourceCommit { get; internal set; } = string.Empty;

        [JsonPropertyName("build_identity")]
        public string BuildIdentity { get; internal set; } = string.Empty;

        [JsonPropertyName("runtime_path_revision")]
        public string RuntimePathRevision { get; internal set; } = string.Empty;

        [JsonPropertyName("runtime_source_sha256")]
        public string RuntimeSourceSha256 { get; internal set; } = string.Empty;

        [JsonPropertyName("runtime_harness_dll_sha256")]
        public string RuntimeHarnessDllSha256 { get; internal set; } = string.Empty;

        [JsonPropertyName("contracts_dll_sha256")]
        public string ContractsDllSha256 { get; internal set; } = string.Empty;

        [JsonPropertyName("transparent_bridge_dll_sha256")]
        public string TransparentBridgeDllSha256 { get; internal set; } = string.Empty;
    }

    public static class RuntimeEvidenceCatalogSource
    {
        public const string SchemaVersion = "runtime_evidence_catalog.v1";
        public const string NativeObjectRuntimePathRevision = "native_object_execution.v2";
        public const string NativeObjectRuntimeSourceSha256 =
            "d3072293f13befcc2d926a862493cc6a365c012b611a9e87c3bf969232f2228e";

        private const string NativeObjectSourceCommit =
            "f79db3eb0e4800d7de39768c802b313eb01317d3";
        private static readonly string[] NativeObjectRuntimeSourcePaths =
        {
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.NativeObjectPayload.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.HousePlant.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.SingingStone.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.SlimeBall.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.FeedHopper.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.AutoGrabber.cs",
            "src/StardewAI.Contracts/Training/TrainingExecutionRequest.MiniObelisk.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.NativeObjectMechanics.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.HousePlant.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.SingingStone.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.SlimeBall.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.FeedHopper.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.AutoGrabber.cs",
            "src/StardewAI.Core/Execution/ActionQueueCompiler.MiniObelisk.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.NativeObjectMechanics.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.HousePlant.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.SingingStone.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.SlimeBall.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.FeedHopper.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.AutoGrabber.cs",
            "src/StardewAI.Core/OptionRegistry/CandidateOptionAvailabilityEvaluator.MiniObelisk.cs",
            "tools/StardewAI.LiveTrainingLoop/Program.RuntimeExecution.NativeObjects.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.NativeObjectInteractionDomain.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.NativeObjectInteractionMovement.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.NativeObjectPlacement.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.NativeObjectPayload.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.HousePlant.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.SingingStone.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.SlimeBall.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.FeedHopper.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.AutoGrabber.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.MiniObelisk.cs"
        };

        private static readonly IReadOnlyList<RuntimePathIdentity> RuntimePaths =
            new ReadOnlyCollection<RuntimePathIdentity>(new[]
            {
                new RuntimePathIdentity
                {
                    RuntimePathRevision = NativeObjectRuntimePathRevision,
                    RuntimeSourceSha256 = NativeObjectRuntimeSourceSha256,
                    RuntimeSourcePaths = NativeObjectRuntimeSourcePaths.ToArray()
                }
            });

        private static readonly IReadOnlyList<RuntimeEvidenceBinding> Bindings =
            new ReadOnlyCollection<RuntimeEvidenceBinding>(new[]
            {
                NativeObjectBinding(
                    "EVD-271", "runtime-house-plant-20260829-001431",
                    "fa311e605b86de0083dc9271ed01dce0500b29917500c9ace9df6d5abdc05b74",
                    "13aea46e7c4aa8a89a58203ad37913d75c3fc9e072f1febee18d91228982cd84",
                    "759b9b691c20a656d3a5916bd9c7e384e6f67901f01df6271460db0f006a8bc3"),
                NativeObjectBinding(
                    "EVD-274", "runtime-singing-stone-20260829-001624",
                    "fa311e605b86de0083dc9271ed01dce0500b29917500c9ace9df6d5abdc05b74",
                    "13aea46e7c4aa8a89a58203ad37913d75c3fc9e072f1febee18d91228982cd84",
                    "759b9b691c20a656d3a5916bd9c7e384e6f67901f01df6271460db0f006a8bc3"),
                NativeObjectBinding(
                    "EVD-272", "runtime-slime-ball-20260829-001854",
                    "fa311e605b86de0083dc9271ed01dce0500b29917500c9ace9df6d5abdc05b74",
                    "13aea46e7c4aa8a89a58203ad37913d75c3fc9e072f1febee18d91228982cd84",
                    "759b9b691c20a656d3a5916bd9c7e384e6f67901f01df6271460db0f006a8bc3"),
                NativeObjectBinding(
                    "EVD-276", "runtime-feed-hopper-20260829-001943",
                    "fa311e605b86de0083dc9271ed01dce0500b29917500c9ace9df6d5abdc05b74",
                    "13aea46e7c4aa8a89a58203ad37913d75c3fc9e072f1febee18d91228982cd84",
                    "759b9b691c20a656d3a5916bd9c7e384e6f67901f01df6271460db0f006a8bc3"),
                NativeObjectBinding(
                    "EVD-278", "runtime-auto-grabber-20260829-002033",
                    "fa311e605b86de0083dc9271ed01dce0500b29917500c9ace9df6d5abdc05b74",
                    "13aea46e7c4aa8a89a58203ad37913d75c3fc9e072f1febee18d91228982cd84",
                    "759b9b691c20a656d3a5916bd9c7e384e6f67901f01df6271460db0f006a8bc3"),
                NativeObjectBinding(
                    "EVD-279", "runtime-mini-obelisk-20260829-011139",
                    "9f08fcd8acce8d6a9ca41cb3dcdee2aa9630aa1490e2947add231697192d26df",
                    "87548f0de326cef947d1ede232097aad9426f81e0de5edb4a28dc9c55a5f76e8",
                    "895976c21cad1ef7df956cabb9d77a2221a5a6f6969220e2ee400164121e38bd")
            });

        private static readonly IReadOnlyDictionary<string, RuntimeEvidenceBinding> ByEvidenceId =
            new ReadOnlyDictionary<string, RuntimeEvidenceBinding>(
                Bindings.ToDictionary(row => row.EvidenceId, StringComparer.Ordinal));

        private static readonly IReadOnlyDictionary<string, RuntimePathIdentity> ByRuntimePathRevision =
            new ReadOnlyDictionary<string, RuntimePathIdentity>(
                RuntimePaths.ToDictionary(row => row.RuntimePathRevision, StringComparer.Ordinal));

        public static IReadOnlyList<RuntimeEvidenceBinding> AllBindings => Bindings;
        public static IReadOnlyList<RuntimePathIdentity> AllRuntimePaths => RuntimePaths;

        public static RuntimeEvidenceFreshnessStatus EvaluateFreshness(
            IEnumerable<string> evidenceIds,
            string expectedRuntimePathRevision,
            out RuntimeEvidenceBinding[] resolvedBindings,
            out RuntimePathIdentity? runtimePathIdentity)
        {
            var ids = evidenceIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            resolvedBindings = ids
                .Where(ByEvidenceId.ContainsKey)
                .Select(id => ByEvidenceId[id])
                .ToArray();

            if (string.IsNullOrWhiteSpace(expectedRuntimePathRevision))
            {
                runtimePathIdentity = null;
                return RuntimeEvidenceFreshnessStatus.LegacyUnbound;
            }

            if (!ByRuntimePathRevision.TryGetValue(
                    expectedRuntimePathRevision,
                    out runtimePathIdentity) ||
                ids.Length == 0 ||
                resolvedBindings.Length != ids.Length)
            {
                return RuntimeEvidenceFreshnessStatus.StaleOrMissing;
            }

            var resolvedPath = runtimePathIdentity;
            var current = resolvedBindings.All(binding =>
                string.Equals(
                    binding.RuntimePathRevision,
                    expectedRuntimePathRevision,
                    StringComparison.Ordinal) &&
                string.Equals(
                    binding.RuntimeSourceSha256,
                    resolvedPath.RuntimeSourceSha256,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(binding.ArtifactId) &&
                !string.IsNullOrWhiteSpace(binding.SourceCommit) &&
                !string.IsNullOrWhiteSpace(binding.BuildIdentity) &&
                IsSha256(binding.RuntimeHarnessDllSha256) &&
                IsSha256(binding.ContractsDllSha256) &&
                IsSha256(binding.TransparentBridgeDllSha256));
            return current
                ? RuntimeEvidenceFreshnessStatus.Current
                : RuntimeEvidenceFreshnessStatus.StaleOrMissing;
        }

        public static string ComputeRuntimeSourceSha256(
            string repositoryRoot,
            string runtimePathRevision)
        {
            if (!ByRuntimePathRevision.TryGetValue(runtimePathRevision, out var identity))
                throw new KeyNotFoundException($"Unknown runtime path revision '{runtimePathRevision}'.");

            var canonical = new StringBuilder();
            foreach (var relativePath in identity.RuntimeSourcePaths.OrderBy(
                         value => value,
                         StringComparer.Ordinal))
            {
                var fullPath = Path.Combine(
                    repositoryRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Runtime evidence source file is missing.", fullPath);

                var source = File.ReadAllText(fullPath)
                    .Replace("\r\n", "\n")
                    .Replace("\r", "\n");
                canonical.Append(relativePath).Append('\n');
                canonical.Append(source).Append('\n');
            }

            using var sha256 = SHA256.Create();
            var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return BitConverter.ToString(digest)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static RuntimeEvidenceBinding NativeObjectBinding(
            string evidenceId,
            string artifactId,
            string runtimeHarnessDllSha256,
            string contractsDllSha256,
            string transparentBridgeDllSha256)
        {
            return new RuntimeEvidenceBinding
            {
                EvidenceId = evidenceId,
                ArtifactId = artifactId,
                SourceCommit = NativeObjectSourceCommit,
                BuildIdentity = "release-net6.0@" + runtimeHarnessDllSha256,
                RuntimePathRevision = NativeObjectRuntimePathRevision,
                RuntimeSourceSha256 = NativeObjectRuntimeSourceSha256,
                RuntimeHarnessDllSha256 = runtimeHarnessDllSha256,
                ContractsDllSha256 = contractsDllSha256,
                TransparentBridgeDllSha256 = transparentBridgeDllSha256
            };
        }

        private static bool IsSha256(string value)
        {
            return value.Length == 64 && value.All(character =>
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f'));
        }
    }
}
