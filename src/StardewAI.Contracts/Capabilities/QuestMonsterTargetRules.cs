using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Contracts.Capabilities
{
    public static class QuestMonsterTargetRules
    {
        public static bool Matches(
            string monsterName,
            IEnumerable<string> targetNameFragments,
            bool matchAnySlimeName = false)
        {
            if (string.IsNullOrWhiteSpace(monsterName))
            {
                return false;
            }

            if (matchAnySlimeName && IsSlimeName(monsterName))
            {
                return true;
            }

            return targetNameFragments
                .Where(target => !string.IsNullOrWhiteSpace(target))
                .Any(target => monsterName.Contains(target, StringComparison.Ordinal));
        }

        public static bool IsSlimeName(string monsterName)
        {
            return monsterName.Contains("Slime", StringComparison.Ordinal) ||
                monsterName.Contains("Jelly", StringComparison.Ordinal) ||
                monsterName.Contains("Sludge", StringComparison.Ordinal);
        }
    }
}
