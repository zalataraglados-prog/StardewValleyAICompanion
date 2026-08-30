using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Contracts.Strategy;

public sealed class CalicoJackRandomCursor
{
    private readonly Func<Random> factory;
    private readonly List<RandomOperation> operations;
    private readonly Random random;

    public CalicoJackRandomCursor(Func<Random> factory)
        : this(factory, Array.Empty<RandomOperation>())
    {
    }

    private CalicoJackRandomCursor(Func<Random> factory, IReadOnlyList<RandomOperation> operations)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        random = factory();
        this.operations = new List<RandomOperation>(operations.Count);
        foreach (var operation in operations)
        {
            Replay(operation);
            this.operations.Add(operation);
        }
    }

    public int OperationCount => operations.Count;

    public int Next(int minimumInclusive, int maximumExclusive)
    {
        var value = random.Next(minimumInclusive, maximumExclusive);
        operations.Add(new RandomOperation("next", minimumInclusive, maximumExclusive));
        return value;
    }

    public double NextDouble()
    {
        var value = random.NextDouble();
        operations.Add(new RandomOperation("double", 0, 0));
        return value;
    }

    public CalicoJackRandomCursor Clone() => new(factory, operations);

    private void Replay(RandomOperation operation)
    {
        if (operation.Kind == "next")
            random.Next(operation.MinimumInclusive, operation.MaximumExclusive);
        else
            random.NextDouble();
    }

    private sealed class RandomOperation
    {
        public RandomOperation(string kind, int minimumInclusive, int maximumExclusive)
        {
            Kind = kind;
            MinimumInclusive = minimumInclusive;
            MaximumExclusive = maximumExclusive;
        }

        public string Kind { get; }
        public int MinimumInclusive { get; }
        public int MaximumExclusive { get; }
    }
}

public sealed class CalicoJackDecisionProjection
{
    public CalicoJackDecisionProjection(
        string recommendedAction,
        int playerTotal,
        int dealerTotal,
        int standCoinDelta,
        int hitCoinDelta,
        int projectedNextHitCard,
        string standOutcome,
        string hitOutcome,
        int searchDepth)
    {
        RecommendedAction = recommendedAction;
        PlayerTotal = playerTotal;
        DealerTotal = dealerTotal;
        StandCoinDelta = standCoinDelta;
        HitCoinDelta = hitCoinDelta;
        ProjectedNextHitCard = projectedNextHitCard;
        StandOutcome = standOutcome;
        HitOutcome = hitOutcome;
        SearchDepth = searchDepth;
    }

    public string RecommendedAction { get; }
    public int PlayerTotal { get; }
    public int DealerTotal { get; }
    public int StandCoinDelta { get; }
    public int HitCoinDelta { get; }
    public int ProjectedNextHitCard { get; }
    public string StandOutcome { get; }
    public string HitOutcome { get; }
    public int SearchDepth { get; }
}

public static class CalicoJackDecisionModel
{
    public const int PlayingTo = 21;
    public const int DealerPassNumber = 18;

    public static int DrawPlayerCard(CalicoJackRandomCursor random, int playerTotal)
    {
        var card = random.Next(1, 10);
        var distanceToTwentyOne = PlayingTo - playerTotal;
        if (distanceToTwentyOne > 1 && distanceToTwentyOne < 6 &&
            random.NextDouble() < 1d / distanceToTwentyOne)
        {
            card = random.NextDouble() < 0.5d
                ? distanceToTwentyOne
                : distanceToTwentyOne - 1;
        }
        return card;
    }

    public static CalicoJackDecisionProjection Recommend(
        CalicoJackRandomCursor random,
        IReadOnlyList<int> playerCards,
        IReadOnlyList<int> dealerCards,
        int currentBet,
        double dailyLuck,
        int luckLevel)
    {
        if (playerCards is null || playerCards.Count < 2)
            throw new ArgumentException("At least two player cards are required.", nameof(playerCards));
        if (dealerCards is null || dealerCards.Count < 2)
            throw new ArgumentException("At least two dealer cards are required.", nameof(dealerCards));
        if (currentBet <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentBet));

        var playerTotal = playerCards.Sum();
        var dealerTotal = dealerCards.Sum();
        var stand = EvaluateStand(random.Clone(), playerTotal, dealerCards, currentBet, dailyLuck, luckLevel);
        var hitCursor = random.Clone();
        var nextCard = playerTotal < PlayingTo ? DrawPlayerCard(hitCursor, playerTotal) : 0;
        var hit = playerTotal >= PlayingTo
            ? Terminal(playerTotal == PlayingTo ? currentBet : -currentBet,
                playerTotal == PlayingTo ? "player_calico_jack" : "player_bust", 0)
            : EvaluateAfterHit(hitCursor, playerTotal + nextCard, dealerCards, currentBet, dailyLuck, luckLevel, 1);
        var action = hit.CoinDelta > stand.CoinDelta ? "hit" : "stand";
        return new CalicoJackDecisionProjection(
            action,
            playerTotal,
            dealerTotal,
            stand.CoinDelta,
            hit.CoinDelta,
            nextCard,
            stand.Outcome,
            hit.Outcome,
            hit.Depth);
    }

    public static DealerDrawProjection DrawDealerCard(
        CalicoJackRandomCursor random,
        int dealerTotal,
        int playerTotal,
        int currentBet,
        double dailyLuck,
        int luckLevel)
    {
        var card = random.Next(1, 10);
        var distanceToTwentyOne = PlayingTo - dealerTotal;
        if (playerTotal == 20 && random.NextDouble() < 0.5d)
            card = distanceToTwentyOne + random.Next(1, 4);
        else if (playerTotal == 19 && random.NextDouble() < 0.25d)
            card = distanceToTwentyOne + random.Next(1, 4);
        else if (playerTotal == 18 && random.NextDouble() < 0.1d)
            card = distanceToTwentyOne + random.Next(1, 4);

        var qiFruitChance = Math.Max(0.0005d, 0.001d + dailyLuck / 20d + luckLevel * 0.002d);
        if (random.NextDouble() < qiFruitChance)
        {
            card = 999;
            currentBet = unchecked(currentBet * 3);
        }
        return new DealerDrawProjection(card, currentBet, qiFruitChance);
    }

    private static TerminalProjection EvaluateAfterHit(
        CalicoJackRandomCursor random,
        int playerTotal,
        IReadOnlyList<int> dealerCards,
        int currentBet,
        double dailyLuck,
        int luckLevel,
        int depth)
    {
        if (playerTotal == PlayingTo)
            return Terminal(currentBet, "player_calico_jack", depth);
        if (playerTotal > PlayingTo)
            return Terminal(-currentBet, "player_bust", depth);
        if (depth >= 32)
        {
            var terminal = EvaluateStand(random, playerTotal, dealerCards, currentBet, dailyLuck, luckLevel);
            return new TerminalProjection(terminal.CoinDelta, terminal.Outcome, depth);
        }

        var stand = EvaluateStand(random.Clone(), playerTotal, dealerCards, currentBet, dailyLuck, luckLevel);
        var hitCursor = random.Clone();
        var card = DrawPlayerCard(hitCursor, playerTotal);
        var hit = EvaluateAfterHit(hitCursor, playerTotal + card, dealerCards, currentBet, dailyLuck, luckLevel, depth + 1);
        return hit.CoinDelta > stand.CoinDelta
            ? hit
            : new TerminalProjection(stand.CoinDelta, stand.Outcome, Math.Max(depth, stand.Depth));
    }

    private static TerminalProjection EvaluateStand(
        CalicoJackRandomCursor random,
        int playerTotal,
        IReadOnlyList<int> dealerCards,
        int currentBet,
        double dailyLuck,
        int luckLevel)
    {
        if (playerTotal == PlayingTo)
            return Terminal(currentBet, "player_calico_jack", 0);
        if (playerTotal > PlayingTo)
            return Terminal(-currentBet, "player_bust", 0);

        var dealerTotal = dealerCards.Sum();
        var bet = currentBet;
        var draws = 0;
        while (dealerTotal < DealerPassNumber || (dealerTotal < playerTotal && playerTotal <= PlayingTo))
        {
            var draw = DrawDealerCard(random, dealerTotal, playerTotal, bet, dailyLuck, luckLevel);
            dealerTotal += draw.Card;
            bet = draw.CurrentBet;
            draws++;
            if (dealerTotal > PlayingTo || draws >= 64)
                break;
        }

        if (dealerTotal > PlayingTo)
            return Terminal(bet, "dealer_bust", draws);
        if (playerTotal == dealerTotal)
            return Terminal(0, "draw", draws);
        return playerTotal > dealerTotal
            ? Terminal(bet, "player_higher", draws)
            : Terminal(-bet, "dealer_higher", draws);
    }

    private static TerminalProjection Terminal(int delta, string outcome, int depth) => new(delta, outcome, depth);

    private sealed class TerminalProjection
    {
        public TerminalProjection(int coinDelta, string outcome, int depth)
        {
            CoinDelta = coinDelta;
            Outcome = outcome;
            Depth = depth;
        }

        public int CoinDelta { get; }
        public string Outcome { get; }
        public int Depth { get; }
    }
}

public sealed class DealerDrawProjection
{
    public DealerDrawProjection(int card, int currentBet, double qiFruitChance)
    {
        Card = card;
        CurrentBet = currentBet;
        QiFruitChance = qiFruitChance;
    }

    public int Card { get; }
    public int CurrentBet { get; }
    public double QiFruitChance { get; }
}
