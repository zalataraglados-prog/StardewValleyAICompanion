namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionQuantityMath
{
    public static int DivideRoundUp(int quantity, int unitsPerOperation)
    {
        if (quantity <= 0 || unitsPerOperation <= 0)
        {
            throw new InvalidDataException(
                "Quantity and units per operation must be positive.");
        }
        return checked((quantity + unitsPerOperation - 1) /
            unitsPerOperation);
    }

    public static int Multiply(int unitsPerOperation, int operationCount)
    {
        if (unitsPerOperation < 0 || operationCount <= 0)
        {
            throw new InvalidDataException(
                "Units per operation must be non-negative and operation count must be positive.");
        }
        return checked(unitsPerOperation * operationCount);
    }
}
