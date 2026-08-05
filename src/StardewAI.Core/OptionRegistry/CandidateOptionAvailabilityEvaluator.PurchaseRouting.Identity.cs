using System;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static bool PurchaseIdentityMatches(
            EconomicCandidate candidate,
            SmallModelActionParameter[] boundParameters)
        {
            var shopId = ReadParameter(boundParameters, "continuation.shop_id");
            var qualifiedItemId = ReadParameter(
                boundParameters,
                "continuation.qualified_item_id");
            var maxUnitPrice = ReadParameterInt(
                boundParameters,
                "continuation.max_unit_price");
            return (string.IsNullOrWhiteSpace(shopId) ||
                    string.Equals(
                        candidate.ShopId,
                        shopId,
                        StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    string.Equals(
                        candidate.QualifiedItemId,
                        qualifiedItemId,
                        StringComparison.Ordinal)) &&
                (!maxUnitPrice.HasValue || candidate.UnitPrice <= maxUnitPrice.Value);
        }

        private static SmallModelActionParameter[] PurchaseContinuationParameters(
            EconomicCandidate candidate,
            string targetLocation)
        {
            return new[]
            {
                Parameter("continuation.option_id", "economy.buy_supplies"),
                Parameter("continuation.shop_id", candidate.ShopId),
                Parameter("continuation.target_location", targetLocation),
                Parameter("continuation.item_id", candidate.ItemId),
                Parameter(
                    "continuation.qualified_item_id",
                    candidate.QualifiedItemId),
                Parameter(
                    "continuation.max_unit_price",
                    candidate.UnitPrice.ToString(CultureInfo.InvariantCulture)),
                Parameter("continuation.quantity", "1")
            };
        }

        private static SmallModelActionParameter[] PurchaseContinuationParameters(
            SmallModelActionParameter[] boundParameters)
        {
            return boundParameters
                .Where(parameter => parameter.Name.StartsWith(
                    "continuation.",
                    StringComparison.Ordinal))
                .ToArray();
        }

        private static bool IsPurchaseContinuationCandidate(
            OptionAvailabilityCandidate candidate)
        {
            return string.Equals(
                    candidate.OptionId,
                    "economy.buy_supplies",
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(ReadParameter(
                    candidate.Parameters,
                    "continuation.shop_id")) &&
                !string.IsNullOrWhiteSpace(ReadParameter(
                    candidate.Parameters,
                    "continuation.qualified_item_id"));
        }

        private static EventCandidate BlockedPurchaseStageCandidate(
            EconomicCandidate preview,
            string reason,
            string locationId = "",
            int? tileX = null,
            int? tileY = null,
            SmallModelActionParameter[]? continuation = null)
        {
            return new EventCandidate
            {
                CandidateId = PurchaseCandidateId(
                    preview,
                    "blocked",
                    locationId,
                    tileX,
                    tileY),
                Kind = "purchase_stage_blocked",
                Available = false,
                LocationId = locationId,
                TileX = tileX,
                TileY = tileY,
                ItemId = preview.ItemId,
                QualifiedItemId = preview.QualifiedItemId,
                DisplayName = preview.DisplayName,
                Quantity = 1,
                ShopId = preview.ShopId,
                UnitPrice = preview.UnitPrice,
                TotalValue = preview.UnitPrice,
                AvailabilityClass = "purchase_stage_blocked",
                AllowedNow = false,
                AllowedToday = false,
                BlockReasons = preview.BlockReasons
                    .Concat(new[] { reason })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = continuation ?? Array.Empty<SmallModelActionParameter>()
            };
        }

        private static string PurchaseCandidateId(
            EconomicCandidate candidate,
            string stage,
            string locationId,
            int? tileX,
            int? tileY)
        {
            return "purchase:" + candidate.ShopId + ":" +
                candidate.QualifiedItemId + ":" + stage + ":" +
                locationId + ":" + (tileX?.ToString(CultureInfo.InvariantCulture) ?? "none") +
                "," + (tileY?.ToString(CultureInfo.InvariantCulture) ?? "none");
        }
    }
}
