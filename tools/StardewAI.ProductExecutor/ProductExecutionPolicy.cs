using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.ProductExecutor;

public sealed class ProductExecutionPolicy
{
    private readonly ProductExecutorOptions options;

    public ProductExecutionPolicy(ProductExecutorOptions options)
    {
        this.options = options;
    }

    public string[] Authorize(
        TrainingExecutionRequest request,
        DateTimeOffset now,
        bool allowExpiredReplay = false)
    {
        var reasons = new List<string>();
        Require(request.SchemaVersion == "training_execution_request.v1", "unsupported_request_schema", reasons);
        Require(!string.IsNullOrWhiteSpace(request.RunId), "run_id_required", reasons);
        Require(!string.IsNullOrWhiteSpace(request.QueueId), "queue_id_required", reasons);
        Require(!string.IsNullOrWhiteSpace(request.QueueItemId), "queue_item_id_required", reasons);
        Require(IsSha256(request.BeforeStateHash), "before_state_hash_invalid", reasons);
        Require(
            !request.OptionId.StartsWith("debug.", StringComparison.Ordinal) &&
            ProductExecutorCapabilityCatalog.IsSupported(request.OptionId),
            "option_not_product_authorized",
            reasons);
        Require(ExecutionTargetProfiles.IsSupported(request.ExecutionMode), "execution_mode_not_supported", reasons);
        if (ExecutionTargetProfiles.IsSupported(request.ExecutionMode))
        {
            Require(
                request.Actor == ExecutionTargetProfiles.CreateActor(request.ExecutionMode).ActorId,
                "actor_execution_mode_mismatch",
                reasons);
        }
        Require(
            Guid.TryParseExact(request.RequestNonce, "N", out _),
            "request_nonce_invalid",
            reasons);
        Require(
            string.IsNullOrWhiteSpace(options.RequiredRunId) || request.RunId == options.RequiredRunId,
            "run_id_not_authorized",
            reasons);
        Require(PathsEqual(request.SaveIsolationPath, options.AllowedSaveRoot), "save_root_not_authorized", reasons);
        if (!DateTimeOffset.TryParse(request.CreatedAt, out var createdAt))
        {
            reasons.Add("created_at_invalid");
        }
        else
        {
            Require(createdAt <= now.AddSeconds(30), "request_created_in_future", reasons);
            if (!allowExpiredReplay)
            {
                Require(
                    createdAt >= now.AddSeconds(-options.MaxRequestAgeSeconds),
                    "request_expired",
                    reasons);
            }
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool PathsEqual(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));

    private static void Require(bool condition, string reason, List<string> reasons)
    {
        if (!condition)
            reasons.Add(reason);
    }
}
