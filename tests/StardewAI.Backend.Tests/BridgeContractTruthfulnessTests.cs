using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed class BridgeContractTruthfulnessTests
{
    [Fact]
    public void ChangeEventBindsOnlyToObservedSnapshot()
    {
        var store = StoreWithSnapshot("snapshot-a");
        var gameEvent = new GameEvent
        {
            EventId = "event-a",
            EventType = "InventoryChanged",
            GameTick = 10,
            ObservedSnapshotHash = "snapshot-a",
            SnapshotRelation = "observed_after_snapshot",
            ChangedFields = new[] { "player.inventory" }
        };

        var errors = EventValidator.ValidateRaw(
            JsonSerializer.Serialize(gameEvent, JsonOptions),
            store,
            out var parsed);

        Assert.Empty(errors);
        Assert.NotNull(parsed);
        Assert.Null(parsed!.PublishedSnapshotHash);
    }

    [Fact]
    public void ChangeEventCannotClaimPublishedSnapshot()
    {
        var store = StoreWithSnapshot("snapshot-a");
        var gameEvent = new GameEvent
        {
            EventId = "event-a",
            EventType = "InventoryChanged",
            GameTick = 10,
            ObservedSnapshotHash = "snapshot-a",
            PublishedSnapshotHash = "snapshot-a",
            SnapshotRelation = "observed_after_snapshot",
            ChangedFields = new[] { "player.inventory" }
        };

        var errors = EventValidator.ValidateRaw(
            JsonSerializer.Serialize(gameEvent, JsonOptions),
            store,
            out _);

        Assert.Contains("change events cannot claim a published_snapshot_hash", errors);
    }

    [Fact]
    public void SnapshotPublishedCarriesObservedAndPublishedIdentities()
    {
        var store = StoreWithSnapshot("snapshot-b");
        var gameEvent = new GameEvent
        {
            EventId = "event-b",
            EventType = "SnapshotPublished",
            GameTick = 11,
            ObservedSnapshotHash = "unavailable",
            PublishedSnapshotHash = "snapshot-b",
            SnapshotRelation = "snapshot_published"
        };

        var errors = EventValidator.ValidateRaw(
            JsonSerializer.Serialize(gameEvent, JsonOptions),
            store,
            out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void LegacyEventV1IsRejected()
    {
        var errors = EventValidator.ValidateRaw(
            """{"schema_version":"event.v1","event_id":"old","event_type":"TimeChanged"}""",
            new StateStore(),
            out _);

        Assert.Contains("schema_version must be event.v2", errors);
    }

    [Fact]
    public void CapabilityIdentityMustBeHashClosedBeforeItCanBeObserved()
    {
        var manifest = ObserverManifest();
        Assert.Empty(CapabilityValidator.Validate(manifest));

        manifest.GameBinaryIdentity.Sha256 = string.Empty;
        Assert.Contains(
            "game_binary_identity hash_observed identity is incomplete",
            CapabilityValidator.Validate(manifest));
    }

    [Fact]
    public void TransparentBridgeSourceHasNoOptionWritePathAndGeneratesAdapterCapabilities()
    {
        var root = FindRepositoryRoot();
        var bridgeSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));
        var configSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Models.cs"));

        Assert.DoesNotContain("ApplyAiControlSettings", bridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.options.autoRun =", bridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.options.pauseWhenOutOfFocus =", bridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyAiControlSettings", configSource, StringComparison.Ordinal);
        Assert.Contains("stateCollector?.Adapters", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("identity_observed_unverified", bridgeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void EventHashSourceUsesObservedSnapshotSemantics()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));

        Assert.Contains("[\"observed_snapshot_hash\"]", source, StringComparison.Ordinal);
        Assert.Contains("[\"published_snapshot_hash\"]", source, StringComparison.Ordinal);
        Assert.Contains("[\"snapshot_relation\"]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"state_hash_before\"]", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"state_hash_after\"]", source, StringComparison.Ordinal);
    }

    private static StateStore StoreWithSnapshot(string stateHash)
    {
        var store = new StateStore();
        store.StoreSnapshot(new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = 1
        });
        return store;
    }

    private static CapabilityManifest ObserverManifest()
    {
        return new CapabilityManifest
        {
            CompatibilityStatus = "identity_observed_unverified",
            GameBinaryIdentity = Identity("Stardew Valley"),
            SmapiBinaryIdentity = Identity("StardewModdingAPI"),
            Capabilities = new[]
            {
                new Capability
                {
                    CapabilityId = "execute.command",
                    AccessMode = "execute",
                    Status = "blocked"
                }
            }
        };
    }

    private static BinaryIdentity Identity(string name)
    {
        return new BinaryIdentity
        {
            AssemblyName = name,
            AssemblyVersion = "1.0.0.0",
            Mvid = "f2c38a8a-8847-4bdc-98ad-58faf9310bed",
            ByteLength = 1,
            Sha256 = new string('a', 64),
            IdentityStatus = "hash_observed"
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
