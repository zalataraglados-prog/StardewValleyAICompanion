using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    private static void VerifySelectedProduct(Uri endpoint, VerifiedExecutionRequestJournal journal,
        JsonElement result, TrainingExecutionResult nativeResult, SnapshotEnvelope before, SnapshotEnvelope after,
        bool requireTillingObservation)
    {
        var request = journal.Request;
        bool Flag(string name, bool expected) => result.TryGetProperty(name, out var field) &&
            field.ValueKind is JsonValueKind.True or JsonValueKind.False && field.GetBoolean() == expected;
        bool Tick(string name, out long tick)
        {
            tick = 0;
            return result.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out tick);
        }
        static bool Hash(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
        var receiptId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
        var observed = nativeResult.ActionOriginObservation;
        var productBefore = Text(result, "product_before_state_hash");
        var requestStateHashDrift = productBefore != request.BeforeStateHash;
        Require(endpoint.Scheme is "http" or "https" && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0 &&
            endpoint.Fragment.Length == 0 &&
            (!requireTillingObservation || NativeTillingObservationContract.IsCompleteRequest(request) && observed is not null) &&
            Text(result, "source") == "product_executor" && Text(result, "product_executor_schema_version") == "stardewai.product_executor.v1" &&
            Text(result, "product_authorization_status") == "authorized" && Text(result, "product_receipt_id") == receiptId &&
            Text(result, "product_request_sha256") == Text(journal.Metadata, "product_request_sha256") &&
            Text(result, "product_request_before_state_hash") == request.BeforeStateHash &&
            Hash(productBefore) && Tick("product_before_game_tick", out var firstTick) &&
            firstTick >= before.GameTick && firstTick <= (requireTillingObservation ? observed!.BeforeGameTick : after.GameTick) &&
            Flag("product_request_state_hash_drift", requestStateHashDrift) &&
            Text(result, "product_after_state_hash") == after.StateHash && Tick("product_after_game_tick", out var lastTick) &&
            lastTick == after.GameTick && Text(result, "product_dispatch_guard") == "native_action_preconditions" &&
            // ReadFrame already requires applied/verified; Product then replans only for live-before drift.
            Flag("product_replan_required", requestStateHashDrift) && Flag("product_idempotent_replay", false),
            "Selected Product receipt does not bind its original request and native frame.");
    }
}
