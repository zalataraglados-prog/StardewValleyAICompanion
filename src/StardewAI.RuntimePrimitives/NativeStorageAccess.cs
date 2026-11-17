using System.Collections.Generic;
using System.Linq;

namespace StardewAI.RuntimePrimitives
{
    public static class NativeStorageAccess
    {
        public static bool CanUseUnownedWoodenFarmChest(
            long ownerId, long actorId, string qualifiedItemId,
            bool isStandaloneMainPlayer, bool isOrdinaryFarmChest,
            IEnumerable<long> savePlayerIds) =>
            ownerId == 0 && actorId != 0 &&
            qualifiedItemId == "(BC)130" &&
            isStandaloneMainPlayer && isOrdinaryFarmChest &&
            savePlayerIds.SequenceEqual(new[] { actorId });
    }
}
