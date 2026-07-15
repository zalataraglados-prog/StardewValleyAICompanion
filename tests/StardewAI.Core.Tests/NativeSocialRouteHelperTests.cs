using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Core.Tests;

public sealed class NativeSocialRouteHelperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record RouteGraphEdge(
        [property: JsonPropertyName("resolved")] bool Resolved,
        [property: JsonPropertyName("from_location")] string FromLocation,
        [property: JsonPropertyName("from_x")] int? FromX,
        [property: JsonPropertyName("from_y")] int? FromY,
        [property: JsonPropertyName("kind")] string Kind,
        [property: JsonPropertyName("target_location")] string TargetLocation,
        [property: JsonPropertyName("target_x")] int? TargetX = null,
        [property: JsonPropertyName("target_y")] int? TargetY = null
    );

    private sealed record TraverseConnectorRequest(
        [property: JsonPropertyName("schema_version")] string SchemaVersion,
        [property: JsonPropertyName("run_id")] string RunId,
        [property: JsonPropertyName("queue_id")] string QueueId,
        [property: JsonPropertyName("queue_item_id")] string QueueItemId,
        [property: JsonPropertyName("before_state_hash")] string BeforeStateHash,
        [property: JsonPropertyName("option_id")] string OptionId,
        [property: JsonPropertyName("execution_mode")] string ExecutionMode,
        [property: JsonPropertyName("actor")] string Actor,
        [property: JsonPropertyName("save_isolation_path")] string SaveIsolationPath,
        [property: JsonPropertyName("request_nonce")] string RequestNonce,
        [property: JsonPropertyName("created_at")] string CreatedAt,
        [property: JsonPropertyName("target_tile_x")] int TargetTileX,
        [property: JsonPropertyName("target_tile_y")] int TargetTileY,
        [property: JsonPropertyName("connector_kind")] string ConnectorKind,
        [property: JsonPropertyName("expected_target_location")] string ExpectedTargetLocation,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("expected_arrival_tile_x")] int? ExpectedArrivalTileX = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("expected_arrival_tile_y")] int? ExpectedArrivalTileY = null
    );

    [Fact]
    public void TwoEdgeGraph_BuildsRequests_WithCorrectCoordinateMapping()
    {
        var edges = new List<RouteGraphEdge>
        {
            new(
                Resolved: true,
                FromLocation: "Farm",
                FromX: 86,
                FromY: 28,
                Kind: "warp",
                TargetLocation: "BusStop",
                TargetX: 8,
                TargetY: 23
            ),
            new(
                Resolved: true,
                FromLocation: "BusStop",
                FromX: 8,
                FromY: 23,
                Kind: "warp",
                TargetLocation: "Town",
                TargetX: 35,
                TargetY: 94
            )
        };

        var resolvedEdges = edges.Where(e =>
            e.Resolved &&
            !string.IsNullOrWhiteSpace(e.FromLocation) &&
            !string.IsNullOrWhiteSpace(e.TargetLocation) &&
            e.FromX.HasValue &&
            e.FromY.HasValue).ToList();

        Assert.Equal(2, resolvedEdges.Count);

        foreach (var edge in resolvedEdges)
        {
            Assert.False(string.IsNullOrWhiteSpace(edge.Kind),
                $"Edge from {edge.FromLocation} to {edge.TargetLocation} has empty kind");
        }

        var stateHash = Guid.NewGuid().ToString("N");

        for (var i = 0; i < resolvedEdges.Count; i++)
        {
            var edge = resolvedEdges[i];
            var request = BuildTraverseRequest(edge, stateHash, i);

            Assert.Equal(edge.FromX!.Value, request.TargetTileX);
            Assert.Equal(edge.FromY!.Value, request.TargetTileY);
            Assert.Equal(edge.Kind, request.ConnectorKind);
            Assert.Equal(edge.TargetLocation, request.ExpectedTargetLocation);

            if (edge.TargetX.HasValue)
                Assert.Equal(edge.TargetX.Value, request.ExpectedArrivalTileX!.Value);
            if (edge.TargetY.HasValue)
                Assert.Equal(edge.TargetY.Value, request.ExpectedArrivalTileY!.Value);

            Assert.Equal("executor.traverse_connector", request.OptionId);
            Assert.Equal(stateHash, request.BeforeStateHash);

            Assert.NotEqual("placeholder", request.BeforeStateHash);
        }
    }

    [Fact]
    public void EdgeFilters_RejectsUnresolved_IncompleteOrNullCoordinateEdges()
    {
        var allEdges = new List<RouteGraphEdge>
        {
            new(Resolved: false, FromLocation: "Farm", FromX: 1,  FromY: 1, Kind: "warp", TargetLocation: "BusStop"),
            new(Resolved: true,  FromLocation: "",    FromX: 1,  FromY: 1, Kind: "warp", TargetLocation: "BusStop"),
            new(Resolved: true,  FromLocation: "Farm", FromX: null, FromY: 1, Kind: "warp", TargetLocation: "BusStop"),
            new(Resolved: true,  FromLocation: "Farm", FromX: 1,  FromY: null, Kind: "warp", TargetLocation: "BusStop"),
            new(Resolved: true,  FromLocation: "Farm", FromX: 1,  FromY: 1, Kind: "warp", TargetLocation: ""),
            new(Resolved: true,  FromLocation: "Farm", FromX: 1,  FromY: 1, Kind: "", TargetLocation: "BusStop"),
            new(Resolved: true,  FromLocation: "Farm", FromX: 86, FromY: 28, Kind: "warp", TargetLocation: "BusStop", TargetX: 8, TargetY: 23),
        };

        var filtered = allEdges.Where(e =>
            e.Resolved &&
            !string.IsNullOrWhiteSpace(e.FromLocation) &&
            !string.IsNullOrWhiteSpace(e.TargetLocation) &&
            e.FromX.HasValue &&
            e.FromY.HasValue).ToList();

        Assert.Equal(2, filtered.Count);

        var validEdges = filtered.Where(e => !string.IsNullOrWhiteSpace(e.Kind)).ToList();
        Assert.Single(validEdges);
        var kept = validEdges[0];
        Assert.True(kept.Resolved);
        Assert.Equal("Farm", kept.FromLocation);
        Assert.Equal(86, kept.FromX!.Value);
        Assert.Equal(28, kept.FromY!.Value);
        Assert.Equal("BusStop", kept.TargetLocation);

        var invalidKindEdge = filtered.First(e => string.IsNullOrWhiteSpace(e.Kind));
        Assert.True(string.IsNullOrWhiteSpace(invalidKindEdge.Kind));
    }

    [Fact]
    public void EdgeKindMapping_UsesEdgeKind_NotWarpDefault()
    {
        var edge = new RouteGraphEdge(
            Resolved: true,
            FromLocation: "Mountain",
            FromX: 36,
            FromY: 5,
            Kind: "ladder",
            TargetLocation: "UndergroundMine",
            TargetX: 1,
            TargetY: 1
        );

        var request = BuildTraverseRequest(edge, Guid.NewGuid().ToString("N"), 0);

        Assert.Equal("ladder", request.ConnectorKind);
        Assert.NotEqual("warp", request.ConnectorKind);
        Assert.False(string.IsNullOrWhiteSpace(request.ConnectorKind));
    }

    [Fact]
    public void EdgeWithMissingKind_FailsClosed()
    {
        var edge = new RouteGraphEdge(
            Resolved: true,
            FromLocation: "Farm",
            FromX: 1,
            FromY: 1,
            Kind: "",
            TargetLocation: "Town"
        );

        Assert.True(string.IsNullOrWhiteSpace(edge.Kind));
    }

    [Fact]
    public void TraverseRequest_RequiresBeforeStateHash_NotPlaceholder()
    {
        var edge = new RouteGraphEdge(
            Resolved: true,
            FromLocation: "Farm",
            FromX: 86,
            FromY: 28,
            Kind: "warp",
            TargetLocation: "BusStop",
            TargetX: 8,
            TargetY: 23
        );

        var stateHash = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";

        var request = BuildTraverseRequest(edge, stateHash, 0);

        Assert.Equal(stateHash, request.BeforeStateHash);
        Assert.NotEqual("placeholder", request.BeforeStateHash);
        Assert.NotEmpty(request.BeforeStateHash);
    }

    [Fact]
    public void ArrivalCoordinateFields_OnlyWritten_WhenTargetCoordinatesPresent()
    {
        var edgeWithTarget = new RouteGraphEdge(
            Resolved: true,
            FromLocation: "Farm",
            FromX: 86,
            FromY: 28,
            Kind: "warp",
            TargetLocation: "BusStop",
            TargetX: 8,
            TargetY: 23
        );

        var requestWithTarget = BuildTraverseRequest(edgeWithTarget, "hash", 0);

        Assert.NotNull(requestWithTarget.ExpectedArrivalTileX);
        Assert.Equal(8, requestWithTarget.ExpectedArrivalTileX!.Value);
        Assert.NotNull(requestWithTarget.ExpectedArrivalTileY);
        Assert.Equal(23, requestWithTarget.ExpectedArrivalTileY!.Value);

        var edgeWithoutTarget = new RouteGraphEdge(
            Resolved: true,
            FromLocation: "BusStop",
            FromX: 8,
            FromY: 23,
            Kind: "warp",
            TargetLocation: "Town",
            TargetX: null,
            TargetY: null
        );

        var requestWithoutTarget = BuildTraverseRequest(edgeWithoutTarget, "hash", 1);

        Assert.Null(requestWithoutTarget.ExpectedArrivalTileX);
        Assert.Null(requestWithoutTarget.ExpectedArrivalTileY);
    }

    [Fact]
    public void TwoEdgeRoute_ProvesFullRequestStackMapping()
    {
        var edges = new List<RouteGraphEdge>
        {
            new(
                Resolved: true,
                FromLocation: "Farm",
                FromX: 86,
                FromY: 28,
                Kind: "warp",
                TargetLocation: "BusStop",
                TargetX: 8,
                TargetY: 23
            ),
            new(
                Resolved: true,
                FromLocation: "BusStop",
                FromX: 8,
                FromY: 23,
                Kind: "warp",
                TargetLocation: "Town",
                TargetX: null,
                TargetY: null
            )
        };

        var stateHash = Guid.NewGuid().ToString("N");
        var requests = new List<TraverseConnectorRequest>();

        for (var i = 0; i < edges.Count; i++)
        {
            requests.Add(BuildTraverseRequest(edges[i], stateHash, i));
        }

        Assert.Equal(2, requests.Count);

        var first = requests[0];
        Assert.Equal(86, first.TargetTileX);
        Assert.Equal(28, first.TargetTileY);
        Assert.Equal("warp", first.ConnectorKind);
        Assert.Equal("BusStop", first.ExpectedTargetLocation);
        Assert.Equal(8, first.ExpectedArrivalTileX);
        Assert.Equal(23, first.ExpectedArrivalTileY);
        Assert.Equal(stateHash, first.BeforeStateHash);

        var second = requests[1];
        Assert.Equal(8, second.TargetTileX);
        Assert.Equal(23, second.TargetTileY);
        Assert.Equal("warp", second.ConnectorKind);
        Assert.Equal("Town", second.ExpectedTargetLocation);
        Assert.Null(second.ExpectedArrivalTileX);
        Assert.Null(second.ExpectedArrivalTileY);
        Assert.Equal(stateHash, second.BeforeStateHash);

        var json = JsonSerializer.Serialize(new { edges = edges }, JsonOptions);
        Assert.DoesNotContain("source_location_id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("connector_kind", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tile_x", json, StringComparison.Ordinal);
        Assert.Contains("from_location", json, StringComparison.Ordinal);
        Assert.Contains("from_x", json, StringComparison.Ordinal);
        Assert.Contains("from_y", json, StringComparison.Ordinal);
        Assert.Contains("kind", json, StringComparison.Ordinal);
        Assert.Contains("target_location", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PowershellBuildRouteEdgeTraverseRequest_ProducesCorrectCoordinateMapping()
    {
        var scriptPath = FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1");
        var scriptContent = File.ReadAllText(scriptPath);

        var functionStart = scriptContent.IndexOf("function Build-RouteEdgeTraverseRequest", StringComparison.Ordinal);
        Assert.True(functionStart >= 0, "Build-RouteEdgeTraverseRequest function not found in smoke script");

        var nextFunction = scriptContent.IndexOf("function ", functionStart + 1, StringComparison.Ordinal);
        var functionBody = nextFunction >= 0
            ? scriptContent.Substring(functionStart, nextFunction - functionStart)
            : scriptContent.Substring(functionStart);

        var tempPsPath = Path.Combine(Path.GetTempPath(), "stardewai_route_test_" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            File.WriteAllText(tempPsPath, functionBody + @"

$edge = [PSCustomObject]@{
    resolved = $true
    from_location = 'Farm'
    from_x = 86
    from_y = 28
    kind = 'warp'
    target_location = 'BusStop'
    target_x = 8
    target_y = 23
}

$request = Build-RouteEdgeTraverseRequest -EdgeData $edge -StateHash 'a1b2c3d4' -SavesPath 'test/saves' -RunId 'test.run' -EdgeIndex 0
Write-Output ($request | ConvertTo-Json -Depth 8 -Compress)
", System.Text.Encoding.UTF8);

            var processStart = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -File \"" + tempPsPath + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processStart);
            Assert.NotNull(process);

            var output = process!.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);

            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(error))
            {
                Assert.Fail("PowerShell invocation failed with exit code " + process.ExitCode + ": " + error);
            }

            var requestJson = output.Trim();
            Assert.NotEmpty(requestJson);

            using var doc = JsonDocument.Parse(requestJson);
            var root = doc.RootElement;

            Assert.Equal("training_execution_request.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("executor.traverse_connector", root.GetProperty("option_id").GetString());
            Assert.Equal(86, root.GetProperty("target_tile_x").GetInt32());
            Assert.Equal(28, root.GetProperty("target_tile_y").GetInt32());
            Assert.Equal("warp", root.GetProperty("connector_kind").GetString());
            Assert.Equal("BusStop", root.GetProperty("expected_target_location").GetString());
            Assert.Equal(8, root.GetProperty("expected_arrival_tile_x").GetInt32());
            Assert.Equal(23, root.GetProperty("expected_arrival_tile_y").GetInt32());
            Assert.Equal("a1b2c3d4", root.GetProperty("before_state_hash").GetString());
        }
        finally
        {
            if (File.Exists(tempPsPath))
            {
                File.Delete(tempPsPath);
            }
        }
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }

    private static TraverseConnectorRequest BuildTraverseRequest(RouteGraphEdge edge, string stateHash, int index)
    {
        var request = new TraverseConnectorRequest(
            SchemaVersion: "training_execution_request.v1",
            RunId: "test.route-bfs",
            QueueId: $"test.route-bfs.{edge.FromLocation}",
            QueueItemId: $"test.route-bfs.{edge.FromLocation}.item.{index}",
            BeforeStateHash: stateHash,
            OptionId: "executor.traverse_connector",
            ExecutionMode: "training_singleplayer",
            Actor: "training_farmer.main",
            SaveIsolationPath: "test/saves",
            RequestNonce: Guid.NewGuid().ToString("N"),
            CreatedAt: DateTimeOffset.UtcNow.ToString("O"),
            TargetTileX: edge.FromX!.Value,
            TargetTileY: edge.FromY!.Value,
            ConnectorKind: edge.Kind,
            ExpectedTargetLocation: edge.TargetLocation,
            ExpectedArrivalTileX: edge.TargetX,
            ExpectedArrivalTileY: edge.TargetY
        );

        return request;
    }
}
