using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private static readonly JsonSerializerOptions ReplyJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    [Theory]
    [InlineData("commit")]
    [InlineData("settle-completed-route")]
    [InlineData("settle-supporting-transition")]
    public void PortfolioReplySurvivesRestartWithoutWritingOrLoadingCache(string operation)
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var (request, result) = ApplyPortfolioReplyCase(repository, operation);
        var path = Assert.Single(Directory.GetFiles(root));
        var bytes = File.ReadAllBytes(path);
        var written = File.GetLastWriteTimeUtc(path);
        var document = JsonNode.Parse(bytes)!.AsObject();
        var storedReply = document["_last_portfolio_reply"]!["reply"]!.AsObject();
        Assert.False(storedReply.ContainsKey("ledger"));
        var restarted = new FileStrategyCommitmentRepository(root);

        var lookup = restarted.LookupPortfolioMutationReply(Snapshot(), operation, ReplyElement(request));

        Assert.Equal("found", lookup["status"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(result, ReplyJson), lookup["reply"]));
        Assert.False(lookup["execution_performed"]!.GetValue<bool>());
        Assert.False(lookup["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.False(lookup["native_outcome_verified"]!.GetValue<bool>());
        Assert.False(lookup["formal_product_training"]!.GetValue<bool>());
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        Assert.Single(Directory.GetFiles(root));
        var cache = (IDictionary)typeof(FileStrategyCommitmentRepository)
            .GetField("cache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(restarted)!;
        Assert.Empty(cache);
        Assert.False(JsonSerializer.SerializeToNode(restarted.Get(Snapshot()), ReplyJson)!
            .AsObject().ContainsKey("_last_portfolio_reply"));
    }

    [Fact]
    public void RejectedMutationDoesNotOverwriteSuccessfulReply()
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var (request, _) = ApplyPortfolioReplyCase(repository, "commit");
        var path = Assert.Single(Directory.GetFiles(root));
        var bytes = File.ReadAllBytes(path);
        var rejected = repository.CommitReservationPortfolio(Snapshot(), (ReservationPortfolioCommitRequest)request);
        Assert.False(rejected.Accepted);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal("found", repository.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["status"]!.GetValue<string>());
    }

    [Fact]
    public void LaterNonPortfolioMutationKeepsButSupersedesLastReply()
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var (request, _) = ApplyPortfolioReplyCase(repository, "commit");
        var cancelled = repository.CancelMaterial(Snapshot(), "route-a-material", new StrategyCommitmentCancelRequest
        {
            StateHash = Snapshot().StateHash, ExpectedLedgerRevision = 1, Reason = "test cancellation"
        });
        Assert.True(cancelled.Accepted, string.Join(";", cancelled.Errors));
        var restarted = new FileStrategyCommitmentRepository(root);
        Assert.Equal("superseded", restarted.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["status"]!.GetValue<string>());
        Assert.Null(restarted.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["reply"]);
    }

    [Fact]
    public void LaterPortfolioCommitDoesNotReturnEarlierRequestAsCurrent()
    {
        var repository = new FileStrategyCommitmentRepository(NewPortfolioReplyRoot());
        var (request, _) = ApplyPortfolioReplyCase(repository, "commit");
        var next = new ReservationPortfolioCommitRequest
        {
            StateHash = Snapshot().StateHash, ExpectedLedgerRevision = 1,
            PortfolioId = "other-portfolio", GoalId = "goal.grandpa_21", SourceDecisionId = "other-decision"
        };
        Assert.True(repository.CommitReservationPortfolio(Snapshot(), next).Accepted);
        Assert.Equal("conflict", repository.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("request", "conflict")]
    [InlineData("operation", "conflict")]
    [InlineData("snapshot", "rejected")]
    [InlineData("actor", "not_found")]
    [InlineData("unknown-operation", "rejected")]
    [InlineData("null-request", "rejected")]
    public void PortfolioLookupRejectsDifferentIdentity(string change, string expected)
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var (request, _) = ApplyPortfolioReplyCase(repository, "commit");
        var snapshot = Snapshot();
        var operation = "commit";
        var element = ReplyElement(request);
        switch (change)
        {
            case "request": ((ReservationPortfolioCommitRequest)request).MaterialClaims[0].Quantity++; element = ReplyElement(request); break;
            case "operation": operation = "settle-completed-route"; break;
            case "snapshot": snapshot.StateHash = "different-state"; break;
            case "actor": snapshot.PlayerId.Value = "other-player"; break;
            case "unknown-operation": operation = "erase-ledger"; break;
            case "null-request": element = default; break;
        }
        var bytes = File.ReadAllBytes(Assert.Single(Directory.GetFiles(root)));
        var lookup = repository.LookupPortfolioMutationReply(snapshot, operation, element);
        Assert.Equal(expected, lookup["status"]!.GetValue<string>());
        Assert.Null(lookup["reply"]);
        Assert.Equal(bytes, File.ReadAllBytes(Assert.Single(Directory.GetFiles(root))));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("missing-schema")]
    [InlineData("missing-ledger-schema")]
    [InlineData("ledger")]
    [InlineData("reply")]
    [InlineData("identity")]
    [InlineData("json")]
    [InlineData("null-root")]
    [InlineData("array-root")]
    public void PortfolioLookupRejectsCorruptionWithoutRepairingFile(string change)
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var (request, _) = ApplyPortfolioReplyCase(repository, "commit");
        var path = Assert.Single(Directory.GetFiles(root));
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        switch (change)
        {
            case "schema": document["_last_portfolio_reply"]!["schema_version"] = "future"; break;
            case "missing-schema": document["_last_portfolio_reply"]!.AsObject().Remove("schema_version"); break;
            case "missing-ledger-schema": document.Remove("schema_version"); break;
            case "ledger": document["updated_at"] = "tampered"; break;
            case "reply": document["_last_portfolio_reply"]!["reply"]!["material_claim_count"] = 999; break;
            case "identity": document["save_id"] = "other-save"; break;
        }
        File.WriteAllText(path, change switch
        {
            "json" => "{", "null-root" => "null", "array-root" => "[]", _ => document.ToJsonString(ReplyJson)
        });
        var bytes = File.ReadAllBytes(path);
        var lookup = repository.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request));
        Assert.Equal("corrupt", lookup["status"]!.GetValue<string>());
        Assert.Null(lookup["reply"]);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void MissingOrLegacyReceiptIsNotInventedFromLedgerHistory()
    {
        var root = NewPortfolioReplyRoot();
        var repository = new FileStrategyCommitmentRepository(root);
        var request = Request(Snapshot(), 1, 300);
        Assert.Equal("not_found", repository.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["status"]!.GetValue<string>());
        Assert.False(Directory.Exists(root));
        repository.CommitReservationPortfolio(Snapshot(), request);
        var path = Assert.Single(Directory.GetFiles(root));
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document.Remove("_last_portfolio_reply");
        File.WriteAllText(path, document.ToJsonString(ReplyJson));
        Assert.Equal("not_found", repository.LookupPortfolioMutationReply(Snapshot(), "commit", ReplyElement(request))["status"]!.GetValue<string>());
    }

    private static (object Request, object Result) ApplyPortfolioReplyCase(
        FileStrategyCommitmentRepository repository, string operation)
    {
        var snapshot = Snapshot();
        var commit = Request(snapshot, 1, 300);
        var committed = repository.CommitReservationPortfolio(snapshot, commit);
        Assert.True(committed.Accepted, string.Join(";", committed.Errors));
        if (operation == "commit") return (commit, committed);
        if (operation == "settle-completed-route")
        {
            var request = SettlementRequest(snapshot, 1);
            var result = repository.SettleReservationPortfolioRoute(snapshot, request);
            Assert.True(result.Accepted, string.Join(";", result.Errors));
            return (request, result);
        }
        var support = repository.CommitReservationPortfolio(snapshot, new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = 1,
            PortfolioId = "support:route-a", GoalId = "goal.grandpa_21", SourceDecisionId = "support:route-a"
        });
        Assert.True(support.Accepted, string.Join(";", support.Errors));
        var supportingRequest = SupportingSettlementRequest(snapshot, 2);
        var supportingResult = repository.SettleReservationPortfolioSupportingTransition(snapshot, supportingRequest);
        Assert.True(supportingResult.Accepted, string.Join(";", supportingResult.Errors));
        return (supportingRequest, supportingResult);
    }

    private static string NewPortfolioReplyRoot() => Path.Combine(Path.GetTempPath(), "stardewai-portfolio-reply", Guid.NewGuid().ToString("N"));
    private static JsonElement ReplyElement(object request) => JsonSerializer.SerializeToElement(request, request.GetType(), ReplyJson);
}
