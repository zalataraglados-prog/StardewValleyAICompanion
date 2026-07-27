using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Infrastructure;

internal sealed record PotentialMachineDemandInput(
    string QualifiedItemId,
    string ItemId,
    int Stack,
    bool UnitSalePriceKnown,
    int UnitSalePrice,
    PredictedMachineDemandOutput[] Outputs);

internal sealed record PredictedMachineDemandOutput(
    string QualifiedItemId,
    string ItemId,
    string PreserveType,
    string PreservedItemId,
    int ProcessMinutes,
    bool SalePriceKnown,
    int UnitSalePrice,
    int Stack,
    int RequiredCount);

internal sealed record MachineDemandEconomicValue(
    string Status,
    int BacklogNetValue,
    int CapacityDeficitNetValue);

internal static class MachineDemandEconomicValueProjection
{
    public static MachineDemandEconomicValue Evaluate(
        IReadOnlyCollection<PotentialMachineDemandInput> inputs,
        int capacityDeficitUnits)
    {
        if (inputs.Count == 0)
        {
            return new(
                "not_applicable_no_current_input_backlog",
                0,
                0);
        }

        var rows = new List<EconomicInputRow>();
        foreach (var input in inputs)
        {
            if (!input.UnitSalePriceKnown)
            {
                return new(
                    "incomplete_input_sale_value",
                    0,
                    0);
            }
            if (input.Outputs.Length == 0 ||
                input.Outputs.Any(output =>
                    !output.SalePriceKnown ||
                    output.Stack <= 0 ||
                    output.RequiredCount <= 0))
            {
                return new(
                    "incomplete_output_sale_value_or_ambiguous_context",
                    0,
                    0);
            }

            var outcomes = input.Outputs
                .Select(output => new
                {
                    OutputTotalValue =
                        (long)output.UnitSalePrice * output.Stack,
                    InputTotalValue =
                        (long)input.UnitSalePrice *
                        output.RequiredCount,
                    output.RequiredCount
                })
                .Distinct()
                .ToArray();
            if (outcomes.Length != 1)
            {
                return new(
                    "incomplete_output_sale_value_or_ambiguous_context",
                    0,
                    0);
            }

            var outcome = outcomes[0];
            rows.Add(new EconomicInputRow(
                Math.Max(
                    0,
                    input.Stack / outcome.RequiredCount),
                outcome.OutputTotalValue -
                outcome.InputTotalValue));
        }

        long backlogNet = 0;
        long totalCycles = 0;
        try
        {
            foreach (var row in rows)
            {
                backlogNet = checked(
                    backlogNet +
                    checked(row.NetValuePerCycle * row.Cycles));
                totalCycles = checked(
                    totalCycles + row.Cycles);
            }
        }
        catch (OverflowException)
        {
            return Overflow();
        }

        var remaining = Math.Min(
            Math.Max(0L, capacityDeficitUnits),
            totalCycles);
        long deficitNet = 0;
        try
        {
            foreach (var row in rows.OrderByDescending(row =>
                         row.NetValuePerCycle))
            {
                var cycles = Math.Min(remaining, row.Cycles);
                deficitNet = checked(
                    deficitNet +
                    checked(cycles * row.NetValuePerCycle));
                remaining -= cycles;
                if (remaining == 0)
                {
                    break;
                }
            }
        }
        catch (OverflowException)
        {
            return Overflow();
        }

        if (backlogNet is > int.MaxValue or < int.MinValue ||
            deficitNet is > int.MaxValue or < int.MinValue)
        {
            return Overflow();
        }

        return new(
            deficitNet > 0
                ? "bounded_current_backlog_positive"
                : "bounded_current_backlog_nonpositive",
            (int)backlogNet,
            (int)deficitNet);
    }

    private static MachineDemandEconomicValue Overflow() => new(
        "blocked_economic_value_overflow",
        0,
        0);

    private sealed record EconomicInputRow(
        int Cycles,
        long NetValuePerCycle);
}
