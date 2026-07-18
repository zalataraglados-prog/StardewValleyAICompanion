using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class EventCandidateRanker
    {
        private readonly EventCandidateTimelineScheduler timelineScheduler;

        public EventCandidateRanker()
            : this(new EventCandidateTimelineScheduler())
        {
        }

        public EventCandidateRanker(EventCandidateTimelineScheduler timelineScheduler)
        {
            this.timelineScheduler = timelineScheduler;
        }

        public PolicyEventCandidatePrediction[] Rank(BaselineTrainingReport report, OptionAvailabilityEnvelope availability)
        {
            var optionScores = report.OptionScores.ToDictionary(score => score.OptionId, StringComparer.Ordinal);
            var ranked = new List<PolicyEventCandidatePrediction>();
            foreach (var option in availability.Options)
            {
                var seenCandidateIds = new HashSet<string>(StringComparer.Ordinal);
                var legalEventCandidates = option.EventCandidates.Where(CanEnterTimeline).ToArray();
                foreach (var ec in legalEventCandidates)
                {
                    if (!string.IsNullOrEmpty(ec.CandidateId))
                    {
                        seenCandidateIds.Add(ec.CandidateId);
                    }
                }

                var legalSocialCandidates = option.SocialCandidates
                    .Where(CanEnterTimeline)
                    .Where(sc => string.IsNullOrEmpty(sc.CandidateId) || seenCandidateIds.Add(sc.CandidateId));

                var combined = legalEventCandidates.Concat(legalSocialCandidates);

                foreach (var candidate in combined)
                {
                    var baseReward = optionScores.TryGetValue(option.OptionId, out var optionScore)
                        ? optionScore.AverageTotalReward
                        : 0;
                    var urgencySignal = candidate.Kind == "water_crop_tile" ? 0.05 : 0;
                    if (candidate.Kind == "route_connector_tile")
                    {
                        urgencySignal = 0.02;
                    }

                    if (candidate.Kind == "interact_endpoint")
                    {
                        urgencySignal = 0.03;
                    }

                    if (candidate.Kind == "recovery_refresh_plan")
                    {
                        urgencySignal = 0.04;
                    }
                    if (candidate.Kind == "recovery_close_menu")
                    {
                        urgencySignal = 0.25;
                    }
                    if (candidate.Kind == "recovery_return_home")
                    {
                        urgencySignal = 0.5;
                    }
                    if (candidate.Kind == "recovery_sleep_immediately")
                    {
                        urgencySignal = 0.75;
                    }
                    if (candidate.Kind == "plant_seed_tile")
                    {
                        urgencySignal = PlantingTimingSignal(candidate.ExpectedEffect) + PlantingValueSignal(candidate.ExpectedEffect);
                    }
                    if (candidate.Kind == "harvest_crop_tile")
                    {
                        urgencySignal = 0.06;
                    }
                    if (candidate.Kind == "harvest_giant_crop_tile")
                    {
                        urgencySignal = 0.065;
                    }
                    if (candidate.Kind == "clear_obstacle_tile" ||
                        candidate.Kind == "clear_farm_resource_clump" ||
                        candidate.Kind == "clear_green_rain_resource_clump")
                    {
                        urgencySignal = 0.025;
                    }
                    if (candidate.Kind == "collect_machine_output_tile")
                    {
                        urgencySignal = 0.04 + MachineOutputValueSignal(candidate.ExpectedEffect);
                    }
                    if (candidate.Kind == "load_machine_input_tile")
                    {
                        urgencySignal = 0.035 + MachineInputOpportunityCostSignal(candidate.ExpectedEffect);
                    }
                    if (candidate.Kind == "catch_fish")
                    {
                        urgencySignal = 0.03;
                    }
                    if (candidate.Kind == "collect_spawned_object")
                    {
                        urgencySignal = 0.035;
                    }
                    if (candidate.Kind == "harvest_ginger")
                    {
                        urgencySignal = 0.04;
                    }
                    if (candidate.Kind == "harvest_bush")
                    {
                        urgencySignal = candidate.QualifiedItemId == "(O)73" ? 0.07 : 0.04;
                    }
                    if (candidate.Kind == "claim_mine_reward_chest")
                    {
                        urgencySignal = candidate.QualifiedItemId == "(O)434" ? 0.09 : 0.05;
                    }
                    if (candidate.Kind == "collect_crab_pot")
                    {
                        urgencySignal = 0.04;
                    }
                    if (candidate.Kind == "collect_fish_pond_output")
                    {
                        urgencySignal = 0.045;
                    }
                    if (candidate.Kind == "complete_fish_pond_request")
                    {
                        urgencySignal = 0.055;
                    }
                    if (candidate.Kind == "pan_ore_spot")
                    {
                        urgencySignal = 0.04;
                    }
                    if (candidate.Kind == "ship_inventory_item_to_bin")
                    {
                        urgencySignal = 0.025 + ShippingValueSignal(candidate);
                    }
                    var costSignal = candidate.EnergyCost * -0.001 + candidate.EstimatedTicks * -0.0001;
                    ranked.Add(new PolicyEventCandidatePrediction
                    {
                        CandidateId = candidate.CandidateId,
                        OptionId = option.OptionId,
                        Kind = candidate.Kind,
                        Score = Math.Round(baseReward + urgencySignal + costSignal, 4),
                        ExpectedReward = Math.Round(baseReward, 4),
                        Available = candidate.Available,
                        LocationId = candidate.LocationId,
                        TileX = candidate.TileX,
                        TileY = candidate.TileY,
                        ExpectedEffect = candidate.ExpectedEffect,
                        ItemId = candidate.ItemId,
                        QualifiedItemId = candidate.QualifiedItemId,
                        SlotIndex = candidate.SlotIndex,
                        Quantity = candidate.Quantity,
                        ShopId = candidate.ShopId,
                        EstimatedTicks = candidate.EstimatedTicks,
                        EnergyCost = candidate.EnergyCost,
                        AvailabilityClass = candidate.AvailabilityClass,
                        AllowedNow = candidate.AllowedNow,
                        AllowedToday = candidate.AllowedToday,
                        NextOpenTime = candidate.NextOpenTime,
                        EffectiveOpenTime = candidate.EffectiveOpenTime,
                        ClosesAt = candidate.ClosesAt,
                        WaitCost = candidate.WaitCost,
                        GateReasons = candidate.GateReasons,
                        BlockReasons = candidate.BlockReasons,
                        Parameters = candidate.Parameters,
                        FullShipmentKnown = candidate.FullShipmentKnown,
                        FullShipmentEligible = candidate.FullShipmentEligible,
                        FullShipmentCurrentShippedCount = candidate.FullShipmentCurrentShippedCount,
                        FullShipmentAlreadyShipped = candidate.FullShipmentAlreadyShipped,
                        FullShipmentContributes = candidate.FullShipmentContributes
                    });
                }

                foreach (var candidate in option.EconomicCandidates.Where(candidate => candidate.Available))
                {
                    var baseReward = optionScores.TryGetValue(option.OptionId, out var optionScore)
                        ? optionScore.AverageTotalReward
                        : 0;
                    var valueSignal = candidate.Kind == "sell_shop_item"
                        ? candidate.TotalValue * 0.001
                        : -candidate.TotalValue * 0.0005;
                    ranked.Add(new PolicyEventCandidatePrediction
                    {
                        CandidateId = candidate.CandidateId,
                        OptionId = option.OptionId,
                        Kind = candidate.Kind,
                        Score = Math.Round(baseReward + valueSignal, 4),
                        ExpectedReward = Math.Round(baseReward, 4),
                        Available = candidate.Available,
                        ItemId = candidate.ItemId,
                        QualifiedItemId = candidate.QualifiedItemId,
                        DisplayName = candidate.DisplayName,
                        ShopId = candidate.ShopId,
                        SlotIndex = candidate.SlotIndex,
                        Quantity = candidate.Quantity,
                        UnitPrice = candidate.UnitPrice,
                        TotalValue = candidate.TotalValue,
                        CanShip = candidate.CanShip,
                        CanShopSell = candidate.CanShopSell,
                        FullShipmentKnown = candidate.FullShipmentKnown,
                        FullShipmentEligible = candidate.FullShipmentEligible,
                        FullShipmentCurrentShippedCount = candidate.FullShipmentCurrentShippedCount,
                        FullShipmentAlreadyShipped = candidate.FullShipmentAlreadyShipped,
                        FullShipmentContributes = candidate.FullShipmentContributes,
                        BlockReasons = candidate.BlockReasons
                    });
                }
            }

            var currentTime = ReadCurrentTime(availability);
            return timelineScheduler.Schedule(
                ranked
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal),
                currentTime);
        }

        private static bool CanEnterTimeline(EventCandidate candidate)
        {
            return candidate.Available || candidate.AllowedToday == true;
        }

        private static int ReadCurrentTime(OptionAvailabilityEnvelope availability)
        {
            return availability.CurrentTime > 0 ? availability.CurrentTime : 600;
        }

        private static double PlantingTimingSignal(string expectedEffect)
        {
            var adjustedGrowDays = ParseInt(expectedEffect, "adjusted_grow_days=");
            var daysRemaining = ParseInt(expectedEffect, "days_remaining_in_season=");
            if (!adjustedGrowDays.HasValue || !daysRemaining.HasValue)
            {
                return 0.03;
            }

            var maturitySlack = daysRemaining.Value - adjustedGrowDays.Value;
            if (maturitySlack < 0)
            {
                return -0.2;
            }

            var urgency = Math.Max(0, 14 - maturitySlack) * 0.003;
            var quickCrop = Math.Max(0, 12 - adjustedGrowDays.Value) * 0.001;
            return Math.Round(0.02 + urgency + quickCrop, 4);
        }

        private static double PlantingValueSignal(string expectedEffect)
        {
            var estimatedValue = ParseDouble(expectedEffect, "estimated_season_harvest_net_value=") ??
                ParseDouble(expectedEffect, "estimated_first_harvest_net_value=") ??
                ParseDouble(expectedEffect, "estimated_season_harvest_value=") ??
                ParseDouble(expectedEffect, "estimated_first_harvest_value=");
            var conservativeValue = ParseDouble(expectedEffect, "expected_season_harvest_net_value=") ??
                ParseDouble(expectedEffect, "expected_first_harvest_net_value=") ??
                ParseDouble(expectedEffect, "expected_season_harvest_value=") ??
                ParseDouble(expectedEffect, "expected_first_harvest_value=");
            var value = estimatedValue ?? conservativeValue;
            if (!value.HasValue)
            {
                return 0;
            }

            return Math.Round(Math.Clamp(value.Value * 0.0005, -0.05, 0.08), 4);
        }

        private static double MachineOutputValueSignal(string expectedEffect)
        {
            var totalValue = ParseDouble(expectedEffect, "output_total_value=");
            if (!totalValue.HasValue)
            {
                var unitPrice = ParseDouble(expectedEffect, "output_sale_price=");
                var stack = ParseDouble(expectedEffect, "output_stack=");
                if (unitPrice.HasValue && stack.HasValue)
                {
                    totalValue = unitPrice.Value * stack.Value;
                }
            }

            return totalValue.HasValue
                ? Math.Round(Math.Clamp(totalValue.Value * 0.0004, 0, 0.08), 4)
                : 0;
        }

        private static double MachineInputOpportunityCostSignal(string expectedEffect)
        {
            var predictedNetValue = ParseDouble(expectedEffect, "predicted_output_net_value=");
            if (predictedNetValue.HasValue)
            {
                return Math.Round(Math.Clamp(predictedNetValue.Value * 0.0003, -0.04, 0.08), 4);
            }

            var predictedOutputValue = ParseDouble(expectedEffect, "predicted_output_total_value=");
            var opportunityCost = ParseDouble(expectedEffect, "machine_input_opportunity_cost=") ??
                ParseDouble(expectedEffect, "input_sale_price=");
            if (predictedOutputValue.HasValue)
            {
                var netValue = predictedOutputValue.Value - (opportunityCost ?? 0);
                return Math.Round(Math.Clamp(netValue * 0.0003, -0.04, 0.08), 4);
            }

            return opportunityCost.HasValue
                ? Math.Round(Math.Clamp(opportunityCost.Value * -0.0002, -0.04, 0), 4)
                : 0;
        }

        private static double ShippingValueSignal(EventCandidate candidate)
        {
            var totalValue = ParseDouble(candidate.ExpectedEffect, "total_shipping_value=");
            if (!totalValue.HasValue)
            {
                var salePrice = ParameterDouble(candidate, "sale_price");
                var quantity = ParameterDouble(candidate, "quantity");
                if (salePrice.HasValue && quantity.HasValue)
                {
                    totalValue = salePrice.Value * quantity.Value;
                }
            }

            var contributes = candidate.ExpectedEffect.Contains("full_shipment_contributes=true");
            var baseSignal = totalValue.HasValue
                ? Math.Round(Math.Clamp(totalValue.Value * 0.001, 0, 0.1), 4)
                : 0.01;
            var fullShipmentBonus = contributes ? 0.015 : 0;

            return baseSignal + fullShipmentBonus;
        }

        private static int? ParseInt(string source, string prefix)
        {
            var value = ParseValue(source, prefix);
            return int.TryParse(value, out var result) ? result : null;
        }

        private static double? ParseDouble(string source, string prefix)
        {
            var value = ParseValue(source, prefix);
            return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private static double? ParameterDouble(EventCandidate candidate, string name)
        {
            var raw = candidate.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value;
            return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? result
                : null;
        }

        private static string ParseValue(string source, string prefix)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            foreach (var segment in source.Split(';'))
            {
                if (segment.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return segment.Substring(prefix.Length);
                }
            }

            return string.Empty;
        }
    }
}
