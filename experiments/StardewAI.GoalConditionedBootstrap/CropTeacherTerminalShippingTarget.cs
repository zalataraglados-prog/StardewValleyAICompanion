namespace StardewAI.GoalConditionedBootstrap;

// Selected once from an already verified retained wave; the owner supplies the accepted cursor.
public sealed record CropTeacherTerminalShippingTarget(
    string RetentionProofPath, string RetentionProofSha256, string SaveId, string PlayerId,
    string GameVersion, int TotalDay, string QualifiedItemId, int SlotIndex, int Quality,
    int UnitPrice, int BinTileX, int BinTileY, int StandTileX, int StandTileY,
    int InitialQuantity, int RequiredQuantity, int AcceptedDepositCount);
