using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SavedResourceReadback_DeclaredVolatileResetMatchesResourcesAndReplaysOriginalRebindOnly()
    {
        var f = new SavedResourceFixture();
        Assert.NotEqual(f.Before.GameTick, f.After.GameTick);
        Assert.NotEqual(f.Before.StateHash, f.After.StateHash);
        var leaf = f.Open();
        Assert.IsAssignableFrom<ICropTeacherSupportingTransition>(leaf);
        Assert.True(leaf.SnapshotResourceContentMatches);
        Assert.False(leaf.NativeReloadVerified);
        Assert.False(leaf.SemanticSavedContentVerified);
        Assert.False(leaf.FormalProductTrainingAuthorized);
        Assert.Equal(f.AfterPath, leaf.AfterSnapshotPath);
        Assert.Equal(3, leaf.InputHashes.Count);
        var request = leaf.RebindRequest;
        Assert.Equal(f.After.StateHash, request.StateHash);
        Assert.Equal(f.Ledger.Revision, request.ExpectedLedgerRevision);
        Assert.Equal(SavedResourceFixture.Step, request.PortfolioId);
        Assert.Equal(SavedResourceFixture.Step, request.SourceDecisionId);
        Assert.Empty(request.MaterialClaims); Assert.Empty(request.CurrencyClaims);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, f.After, request, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var rebound = leaf.VerifyRebindReply(reply);
        Assert.Equal(f.After.StateHash, rebound.SourceStateHash);
        Assert.Equal(f.Ledger.Revision + 1, rebound.Revision);
        AssertOtherRootsUnchanged(f.Ledger, rebound);
        foreach (var row in leaf.InputHashes) Assert.Equal(row.Value, SavedResourceFixture.Hash(row.Key));
        leaf.ValidateInputsUnchanged();
    }

    [Theory]
    [InlineData("inventory")]
    [InlineData("currency")]
    [InlineData("crops")]
    [InlineData("calendar")]
    [InlineData("unknown")]
    public void SavedResourceReadback_RejectsTargetedPersistentDrift(string kind)
    {
        var variants = kind switch
        {
            "inventory" => new[] { "stack", "quality" },
            "currency" => new[] { "money", "club_coins", "total_money_earned", "shop_currency_balances" },
            "calendar" => new[] { "total_days", "year", "season", "day" },
            _ => new[] { kind }
        };
        foreach (var field in variants)
        {
            var f = new SavedResourceFixture();
            string expected;
            if (kind == "inventory")
            {
                ChangeState(f.After, "player", player =>
                {
                    var row = player["inventory"]!["value"]!.AsArray().OfType<JsonObject>().First();
                    row[field] = field == "quality" ? 4 : row[field]!.GetValue<int>() + 1;
                });
                expected = "player.inventory";
            }
            else if (kind == "currency")
            {
                ChangeState(f.After, "player", player =>
                {
                    if (field == "shop_currency_balances") player[field]!["value"]!["rows"]![0]!["balance"] = 9999;
                    else player[field]!["value"] = player[field]!["value"]!.GetValue<int>() + 1;
                });
                expected = "player." + field;
            }
            else if (kind == "crops")
            {
                ChangeState(f.After, "farm", farm => farm["crops"]!["value"]!.AsArray().Add(new JsonObject { ["declared_persistent_drift"] = true }));
                expected = "farm.crops";
            }
            else if (kind == "calendar")
            {
                ChangeState(f.After, "time", time =>
                {
                    if (field == "season") time[field]!["value"] = time[field]!["value"]!.GetValue<string>() == "winter" ? "spring" : "winter";
                    else time[field]!["value"] = time[field]!["value"]!.GetValue<int>() + 1;
                });
                expected = "time." + field;
            }
            else
            {
                ChangeState(f.After, "player", player => player["unknown_persisted_resource"] = new JsonObject { ["status"] = "available", ["value"] = 1 });
                expected = "player.unknown_persisted_resource";
            }
            f.Save();
            var error = Assert.Throws<InvalidDataException>(() => f.Open());
            Assert.Contains("persistent resources differ", error.Message);
            Assert.Contains(expected, error.Message);
        }
    }

    [Fact]
    public void SavedResourceReadback_RequiresReadableTypedCoreFieldsEvenWhenBothSnapshotsMatch()
    {
        var fields = new[] { ("player", "inventory"), ("farm", "crops"), ("time", "total_days"), ("time", "year"),
            ("time", "season"), ("time", "day"), ("player", "money"), ("player", "club_coins"),
            ("player", "total_money_earned"), ("player", "shop_currency_balances") };
        foreach (var (domain, field) in fields)
        {
            foreach (var drift in new[] { "missing", "unavailable", "null" })
            {
                var f = new SavedResourceFixture();
                foreach (var snapshot in new[] { f.Before, f.After })
                    ChangeState(snapshot, domain, section =>
                    {
                        if (drift == "missing") section.AsObject().Remove(field);
                        else if (drift == "null") section[field]!["value"] = null;
                        else section[field]!["status"] = "unavailable";
                    });
                f.Save();
                var error = Assert.Throws<InvalidDataException>(() => f.Open());
                Assert.Contains("required readable field " + domain + "." + field, error.Message);
            }
        }
        foreach (var (domain, field) in new[] { ("player", "inventory"), ("farm", "crops"), ("time", "year"), ("time", "season") })
        {
            var f = new SavedResourceFixture();
            foreach (var snapshot in new[] { f.Before, f.After })
                ChangeState(snapshot, domain, section => section[field]!["value"] = new JsonObject());
            f.Save();
            Assert.Contains("required", Assert.Throws<InvalidDataException>(() => f.Open()).Message);
        }
    }

    [Fact]
    public void SavedResourceReadback_RejectsActorHashLedgerPathAndMarkerIdentityDrift()
    {
        foreach (var fault in new[] { "actor", "game", "snapshot_hash", "ledger_source", "ledger_actor", "step_empty", "step_reused" })
        {
            var f = new SavedResourceFixture();
            var reason = fault switch
            {
                "actor" => "snapshot actor/game identity differs",
                "game" or "snapshot_hash" => "snapshot is invalid",
                "ledger_source" => "ledger source must equal",
                "ledger_actor" => "ledger metadata or identity is invalid",
                _ => "marker identity is not new/distinct"
            };
            if (fault == "actor") f.After.PlayerId.Value = "456";
            if (fault == "game") f.After.GameVersion = "foreign";
            if (fault == "snapshot_hash") f.After.StateHash = new string('0', 64);
            if (fault == "ledger_source") f.Ledger.SourceStateHash = f.After.StateHash;
            if (fault == "ledger_actor") f.Ledger.SaveId = "foreign";
            f.Save(hashSnapshots: fault != "snapshot_hash");
            var step = fault == "step_empty" ? " " : fault == "step_reused" ? f.Ledger.History.First().SourceDecisionId : SavedResourceFixture.Step;
            Assert.Contains(reason, Assert.Throws<InvalidDataException>(() => f.Open(step)).Message);
        }
        var paths = new SavedResourceFixture();
        Assert.Contains("distinct", Assert.Throws<InvalidDataException>(() => CropTeacherSavedResourceReadback.Open(
            paths.BeforePath, paths.BeforePath, paths.LedgerPath, SavedResourceFixture.Step)).Message);
        Assert.Contains("absolute", Assert.Throws<InvalidDataException>(() => CropTeacherSavedResourceReadback.Open(
            "relative-before.json", paths.AfterPath, paths.LedgerPath, SavedResourceFixture.Step)).Message);
    }

    [Fact]
    public void SavedResourceReadback_OriginalReplyValidationRejectsCrossRootMutationAndReturnsDefensiveCopies()
    {
        var f = new SavedResourceFixture(); var leaf = f.Open();
        var request = leaf.RebindRequest;
        request.PortfolioId = "caller-mutated";
        Assert.Equal(SavedResourceFixture.Step, leaf.RebindRequest.PortfolioId);
        var baseLedger = leaf.BaseLedger;
        baseLedger.SourceStateHash = "caller-mutated";
        Assert.Equal(f.Before.StateHash, leaf.BaseLedger.SourceStateHash);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, f.After, leaf.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        reply.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
        Assert.Contains("rebind does not replay the original marker", Assert.Throws<InvalidDataException>(() => leaf.VerifyRebindReply(reply)).Message);
    }

    [Fact]
    public void SavedResourceReadback_RetainedInputTamperingInvalidatesReadRequestAndReply()
    {
        foreach (var input in new[] { "before", "after", "ledger" })
        {
            var f = new SavedResourceFixture(); var leaf = f.Open();
            var reply = new ReservationPortfolioLedgerService().Commit(f.Ledger, f.After, leaf.RebindRequest, RecordedAt);
            Assert.True(reply.Accepted, string.Join(",", reply.Errors));
            var path = input == "before" ? f.BeforePath : input == "after" ? f.AfterPath : f.LedgerPath;
            File.AppendAllText(path, " ");
            Assert.Contains("retained input changed", Assert.Throws<InvalidDataException>(leaf.ValidateInputsUnchanged).Message);
            Assert.Contains("retained input changed", Assert.Throws<InvalidDataException>(() => _ = leaf.RebindRequest).Message);
            Assert.Contains("retained input changed", Assert.Throws<InvalidDataException>(() => leaf.VerifyRebindReply(reply)).Message);
        }
    }
}
