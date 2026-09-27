using System;
using System.Collections.Generic;
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
            var stockId = ReadParameter(
                boundParameters,
                "continuation.stock_id");
            var outputStack = ReadParameterInt(
                boundParameters,
                "continuation.output_stack_per_purchase");
            var outputQuality = ReadParameterInt(
                boundParameters,
                "continuation.output_quality");
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
                (string.IsNullOrWhiteSpace(stockId) ||
                    string.Equals(
                        candidate.StockId,
                        stockId,
                        StringComparison.Ordinal)) &&
                (!outputStack.HasValue ||
                    candidate.OutputStack == outputStack.Value) &&
                (!outputQuality.HasValue ||
                    candidate.OutputQuality == outputQuality.Value) &&
                (!maxUnitPrice.HasValue || candidate.UnitPrice <= maxUnitPrice.Value);
        }

        private static SmallModelActionParameter[] PurchaseContinuationParameters(
            EconomicCandidate candidate,
            string targetLocation)
        {
            var parameters = new List<SmallModelActionParameter>
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
            if (!string.IsNullOrWhiteSpace(candidate.StockId))
            {
                parameters.Add(Parameter(
                    "continuation.stock_id",
                    candidate.StockId));
            }
            if (candidate.OutputStack > 0)
            {
                parameters.Add(Parameter(
                    "continuation.output_stack_per_purchase",
                    candidate.OutputStack.ToString(CultureInfo.InvariantCulture)));
            }
            if (candidate.OutputQuality >= 0)
            {
                parameters.Add(Parameter(
                    "continuation.output_quality",
                    candidate.OutputQuality.ToString(CultureInfo.InvariantCulture)));
            }
            return parameters.ToArray();
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

        private static EventCandidate BlockedShopObjectiveStageCandidate(
            EconomicCandidate preview,
            string objectivePrefix,
            string reason,
            string locationId = "",
            int? tileX = null,
            int? tileY = null,
            SmallModelActionParameter[]? continuation = null)
        {
            return new EventCandidate
            {
                CandidateId = ShopObjectiveCandidateId(
                    preview,
                    objectivePrefix,
                    "blocked",
                    locationId,
                    tileX,
                    tileY),
                Kind = objectivePrefix + "_stage_blocked",
                Available = false,
                LocationId = locationId,
                TileX = tileX,
                TileY = tileY,
                ItemId = preview.ItemId,
                QualifiedItemId = preview.QualifiedItemId,
                DisplayName = preview.DisplayName,
                SlotIndex = preview.SlotIndex,
                Quantity = preview.Quantity,
                ShopId = preview.ShopId,
                UnitPrice = preview.UnitPrice,
                TotalValue = preview.UnitPrice,
                AvailabilityClass = objectivePrefix + "_stage_blocked",
                AllowedNow = false,
                AllowedToday = false,
                BlockReasons = preview.BlockReasons
                    .Concat(new[] { reason })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                Parameters = continuation ?? Array.Empty<SmallModelActionParameter>()
            };
        }

        private static string ShopObjectiveCandidateId(
            EconomicCandidate candidate,
            string objectivePrefix,
            string stage,
            string locationId,
            int? tileX,
            int? tileY)
        {
            var stockIdentity = string.IsNullOrWhiteSpace(candidate.StockId)
                ? string.Empty
                : candidate.StockId + ":";
            return objectivePrefix + ":" + candidate.ShopId + ":" +
                stockIdentity + candidate.QualifiedItemId + ":" + stage + ":" +
                locationId + ":" + (tileX?.ToString(CultureInfo.InvariantCulture) ?? "none") +
                "," + (tileY?.ToString(CultureInfo.InvariantCulture) ?? "none");
        }
    }
}
