using System;

namespace StardewAI.Core.Infrastructure;

public static class StochasticRetryPolicy
{
    public const double TargetSuccessProbability = 0.95d;
    public const int MaximumIndependentAttemptCount = 100_000;

    public static int? RequiredIndependentAttemptCount(
        int requiredSuccessCount,
        double singleAttemptSuccessProbability,
        double targetSuccessProbability = TargetSuccessProbability)
    {
        if (requiredSuccessCount <= 0 ||
            double.IsNaN(singleAttemptSuccessProbability) ||
            double.IsInfinity(singleAttemptSuccessProbability) ||
            singleAttemptSuccessProbability < 0d ||
            singleAttemptSuccessProbability > 1d ||
            double.IsNaN(targetSuccessProbability) ||
            double.IsInfinity(targetSuccessProbability) ||
            targetSuccessProbability <= 0d ||
            targetSuccessProbability > 1d)
        {
            throw new ArgumentOutOfRangeException();
        }
        if (singleAttemptSuccessProbability == 0d)
        {
            return null;
        }
        if (requiredSuccessCount > MaximumIndependentAttemptCount)
        {
            return null;
        }
        if (targetSuccessProbability == 1d &&
            singleAttemptSuccessProbability < 1d)
        {
            return null;
        }
        if (singleAttemptSuccessProbability == 1d)
        {
            return requiredSuccessCount;
        }

        if (BinomialSuccessTail(
                MaximumIndependentAttemptCount,
                requiredSuccessCount,
                singleAttemptSuccessProbability) < targetSuccessProbability)
        {
            return null;
        }

        var low = requiredSuccessCount;
        var high = MaximumIndependentAttemptCount;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (BinomialSuccessTail(
                    middle,
                    requiredSuccessCount,
                    singleAttemptSuccessProbability) >= targetSuccessProbability)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }
        return low;
    }

    private static double BinomialSuccessTail(
        int attempts,
        int requiredSuccessCount,
        double successProbability)
    {
        if (attempts < requiredSuccessCount)
        {
            return 0d;
        }

        var failureProbability = 1d - successProbability;
        var logRatio = Math.Log(successProbability) -
                       Math.Log(failureProbability);
        var logTerm = attempts * Math.Log(failureProbability);
        var maximumLogTerm = logTerm;
        var scaledSum = 1d;
        for (var successes = 1;
             successes < requiredSuccessCount;
             successes++)
        {
            logTerm += Math.Log(attempts - successes + 1d) -
                       Math.Log(successes) +
                       logRatio;
            if (logTerm <= maximumLogTerm)
            {
                scaledSum += Math.Exp(logTerm - maximumLogTerm);
            }
            else
            {
                scaledSum = scaledSum *
                            Math.Exp(maximumLogTerm - logTerm) + 1d;
                maximumLogTerm = logTerm;
            }
        }

        var lowerTail = Math.Exp(maximumLogTerm) * scaledSum;
        return lowerTail >= 1d ? 0d :
            lowerTail <= 0d ? 1d : 1d - lowerTail;
    }
}
