using System.Xml.Linq;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private interface IVerifiedPickupToolMetadata
    {
        XElement ProjectExpected(NativeObservedInventoryPayloadSlot slot, XElement original);
    }
}
