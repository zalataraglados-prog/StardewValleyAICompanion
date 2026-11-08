using System;

namespace StardewAI.RuntimePrimitives
{
    public static class ActionOriginInventoryCredit
    {
        public static int LowerBound(int producerInventoryBefore, int inventoryNow, bool preexistingCollectionAmbiguous)
        {
            if (producerInventoryBefore < 0 || inventoryNow < 0)
                throw new ArgumentOutOfRangeException(nameof(producerInventoryBefore), "Action-origin quantities must be non-negative.");
            var increase = Math.Max(0L, (long)inventoryNow - producerInventoryBefore);
            return preexistingCollectionAmbiguous ? 0 : (int)increase;
        }

        public static long RequiredInventoryThreshold(int producerInventoryBefore, int requiredQuantity)
        {
            if (producerInventoryBefore < 0 || requiredQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredQuantity), "A producer baseline and positive requirement are required.");
            return (long)producerInventoryBefore + requiredQuantity;
        }

        public static bool MeetsRequirement(int verifiedQuantity, int requiredQuantity)
        {
            if (verifiedQuantity < 0 || requiredQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredQuantity), "A verified quantity and positive requirement are required.");
            return verifiedQuantity >= requiredQuantity;
        }
    }
}
