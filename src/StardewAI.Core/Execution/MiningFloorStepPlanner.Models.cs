using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private sealed class ObjectSourceMatch
        {
            public JsonElement Object { get; set; }

            public Candidate? Candidate { get; set; }

            public int MatchRank { get; set; }

            public string MatchStatus { get; set; } = string.Empty;

            public string[] MatchedDropIds { get; set; } = Array.Empty<string>();
        }

        private sealed class MonsterDropMatch
        {
            public string[] MatchedIds { get; set; } = Array.Empty<string>();

            public string TargetId { get; set; } = string.Empty;

            public bool IsGuaranteed { get; set; }

            public double? Chance { get; set; }

            public bool ChanceKnown => Chance.HasValue;

            public double? ExpectedQuantityPerKill { get; set; }

            public string ProbabilityStatus { get; set; } = string.Empty;

            public double Efficiency(int distance, double? combatDurationMs = null, double? movementTileDurationMs = null)
            {
                if (!Chance.HasValue)
                {
                    return -1d;
                }
                return combatDurationMs.HasValue && movementTileDurationMs.HasValue
                    ? Chance.Value / Math.Max(1d, combatDurationMs.Value + Math.Max(0, distance) * movementTileDurationMs.Value)
                    : Chance.Value / (Math.Max(0, distance) + 1d);
            }
        }

        private sealed class MonsterCombatProjectionInfo
        {
            public MonsterCombatProjectionInfo(
                string method,
                int? slotIndex,
                double? expectedAttacks,
                double? durationMs,
                string ammoQualifiedItemId,
                int ammoStack,
                double? selectionCostMs = null,
                string terminalEffect = "defeat",
                bool explosiveAreaSafe = false,
                bool explosiveAreaHasAdditionalValue = false,
                int explosiveAreaUsefulObjectHits = 0,
                int explosiveAreaAdditionalMonsterHits = 0)
            {
                Method = method;
                SlotIndex = slotIndex;
                ExpectedAttacks = expectedAttacks;
                DurationMs = durationMs;
                AmmoQualifiedItemId = ammoQualifiedItemId;
                AmmoStack = ammoStack;
                SelectionCostMs = selectionCostMs ?? durationMs ?? double.MaxValue;
                TerminalEffect = terminalEffect;
                ExplosiveAreaSafe = explosiveAreaSafe;
                ExplosiveAreaHasAdditionalValue = explosiveAreaHasAdditionalValue;
                ExplosiveAreaUsefulObjectHits = explosiveAreaUsefulObjectHits;
                ExplosiveAreaAdditionalMonsterHits = explosiveAreaAdditionalMonsterHits;
            }

            public string Method { get; }

            public int? SlotIndex { get; }

            public double? ExpectedAttacks { get; }

            public double? DurationMs { get; }

            public string AmmoQualifiedItemId { get; }

            public int AmmoStack { get; }

            public double SelectionCostMs { get; }

            public string TerminalEffect { get; }

            public bool ExplosiveAreaSafe { get; }

            public bool ExplosiveAreaHasAdditionalValue { get; }

            public int ExplosiveAreaUsefulObjectHits { get; }

            public int ExplosiveAreaAdditionalMonsterHits { get; }

            public double ExplosiveAreaValueMultiplier => Math.Min(
                3d,
                1d + ExplosiveAreaAdditionalMonsterHits + ExplosiveAreaUsefulObjectHits * 0.25d);

            public MonsterCombatProjectionInfo WithSelectionCost(double? selectionCostMs)
            {
                return new MonsterCombatProjectionInfo(
                    Method,
                    SlotIndex,
                    ExpectedAttacks,
                    DurationMs,
                    AmmoQualifiedItemId,
                    AmmoStack,
                    selectionCostMs,
                    TerminalEffect,
                    ExplosiveAreaSafe,
                    ExplosiveAreaHasAdditionalValue,
                    ExplosiveAreaUsefulObjectHits,
                    ExplosiveAreaAdditionalMonsterHits);
            }
        }

        private sealed class BombCandidate
        {
            public BombCandidate(Candidate candidate, int slotIndex, string qualifiedItemId, int radius, int escapeX, int escapeY, int objectHits, int monsterHits, int score)
            {
                Candidate = candidate;
                SlotIndex = slotIndex;
                QualifiedItemId = qualifiedItemId;
                Radius = radius;
                EscapeX = escapeX;
                EscapeY = escapeY;
                ObjectHits = objectHits;
                MonsterHits = monsterHits;
                Score = score;
            }

            public Candidate Candidate { get; }
            public int SlotIndex { get; }
            public string QualifiedItemId { get; }
            public int Radius { get; }
            public int EscapeX { get; }
            public int EscapeY { get; }
            public int ObjectHits { get; }
            public int MonsterHits { get; }
            public int Score { get; }
        }

        private sealed class MummyBombFinisherCandidate
        {
            public MummyBombFinisherCandidate(JsonElement monster, JsonElement bomb, Candidate placement, int escapeX, int escapeY, int escapeDistance)
            {
                Monster = monster;
                Bomb = bomb;
                Placement = placement;
                EscapeX = escapeX;
                EscapeY = escapeY;
                EscapeDistance = escapeDistance;
            }

            public JsonElement Monster { get; }
            public JsonElement Bomb { get; }
            public Candidate Placement { get; }
            public int Radius => ReadInt(Bomb, "radius_tiles") ?? 0;
            public int EscapeX { get; }
            public int EscapeY { get; }
            public int EscapeDistance { get; }
            public int TotalDistance => Placement.Distance + EscapeDistance;
        }

        private sealed class MonsterDropCatalogInfo
        {
            public MonsterDropCatalogInfo(string[] ids, IReadOnlyDictionary<string, MonsterDropCatalogEntryInfo> selectionEntries)
            {
                Ids = ids;
                SelectionEntries = selectionEntries;
            }

            public string[] Ids { get; }

            public IReadOnlyDictionary<string, MonsterDropCatalogEntryInfo> SelectionEntries { get; }
        }

        private sealed class MonsterDropCatalogEntryInfo
        {
            public MonsterDropCatalogEntryInfo(double conditionalSelectionChance, double conditionalExpectedQuantity)
            {
                ConditionalSelectionChance = conditionalSelectionChance;
                ConditionalExpectedQuantity = conditionalExpectedQuantity;
            }

            public double ConditionalSelectionChance { get; }

            public double ConditionalExpectedQuantity { get; }
        }

        private sealed class TargetProbabilityRule
        {
            public TargetProbabilityRule(double chance, double? expectedQuantity)
            {
                Chance = chance;
                ExpectedQuantity = expectedQuantity;
            }

            public double Chance { get; }

            public double? ExpectedQuantity { get; }
        }

        private sealed class SearchResult
        {
            private readonly (int X, int Y) start;
            private readonly Dictionary<string, string> previous;

            public SearchResult((int X, int Y) start, Dictionary<string, int> distance, Dictionary<string, string> previous)
            {
                this.start = start;
                Distance = distance;
                this.previous = previous;
            }

            public Dictionary<string, int> Distance { get; }

            public (int X, int Y) Start => start;

            public MiningPathTile[] PathTo(int x, int y)
            {
                var path = new List<MiningPathTile>();
                var key = Key(x, y);
                while (true)
                {
                    var split = key.Split(',');
                    path.Add(new MiningPathTile { X = int.Parse(split[0]), Y = int.Parse(split[1]) });
                    if (key == Key(start.X, start.Y))
                    {
                        break;
                    }

                    if (!previous.TryGetValue(key, out key))
                    {
                        return Array.Empty<MiningPathTile>();
                    }
                }

                path.Reverse();
                return path.ToArray();
            }
        }

        private sealed class Candidate
        {
            public Candidate(int targetX, int targetY, int standX, int standY, int distance, int swings, bool deterministicLadder, MiningPathTile[] path)
            {
                TargetX = targetX;
                TargetY = targetY;
                StandX = standX;
                StandY = standY;
                Distance = distance;
                Swings = swings;
                DeterministicLadder = deterministicLadder;
                Path = path;
            }

            public int TargetX { get; }
            public int TargetY { get; }
            public int StandX { get; }
            public int StandY { get; }
            public int Distance { get; }
            public int Swings { get; }
            public bool DeterministicLadder { get; }
            public MiningPathTile[] Path { get; }
        }    }
}
