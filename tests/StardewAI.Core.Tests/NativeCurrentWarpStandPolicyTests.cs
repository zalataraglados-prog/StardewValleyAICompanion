using StardewAI.RuntimeTestHarness;

namespace StardewAI.Core.Tests;

public sealed class NativeCurrentWarpStandPolicyTests
{
    [Fact]
    public void ObservedFarmerCanLeaveNorthWithoutEnteringTheSouthernNpcBounds()
    {
        var facts = ObservedFacts();
        Assert.True(new ConnectorStandBounds(64, 64, 64, 64).Intersects(facts.CharacterBounds![0]));
        Assert.False(facts.PlayerBounds.Intersects(facts.CharacterBounds[0]));
        Assert.True(NativeCurrentWarpStandPolicy.CanUse(facts));
    }

    [Theory]
    [InlineData("stand_wall")]
    [InlineData("target_wall")]
    [InlineData("not_current")]
    [InlineData("not_adjacent")]
    [InlineData("outside_map")]
    [InlineData("touch_action")]
    [InlineData("stand_is_warp")]
    [InlineData("unknown_connector")]
    [InlineData("unknown_player_bounds")]
    [InlineData("unknown_characters")]
    [InlineData("unknown_character_bounds")]
    [InlineData("actual_player_collision")]
    [InlineData("target_occupied")]
    public void CurrentStandExceptionFailsClosedOutsideItsNativeProof(string mutation)
    {
        var facts = mutation switch
        {
            "stand_wall" => ObservedFacts() with { StandHardWalkable = false },
            "target_wall" => ObservedFacts() with { WarpHardWalkable = false },
            "not_current" => ObservedFacts() with { CurrentTileX = 2 },
            "not_adjacent" => ObservedFacts() with { WarpTileY = 3 },
            "outside_map" => ObservedFacts() with { StandOnMap = false },
            "touch_action" => ObservedFacts() with { StandHasTouchAction = true },
            "stand_is_warp" => ObservedFacts() with { StandIsWarp = true },
            "unknown_connector" => ObservedFacts() with { ConnectorValidated = false },
            "unknown_player_bounds" => ObservedFacts() with { PlayerBounds = default },
            "unknown_characters" => ObservedFacts() with { CharacterBounds = null },
            "unknown_character_bounds" => ObservedFacts() with { CharacterBounds = [default] },
            "actual_player_collision" => ObservedFacts() with { CharacterBounds = [new(78, 100, 48, 32)] },
            "target_occupied" => ObservedFacts() with { CharacterBounds = [new(78, 16, 48, 32)] },
            _ => throw new InvalidOperationException(mutation)
        };
        Assert.False(NativeCurrentWarpStandPolicy.CanUse(facts));
    }

    [Theory]
    [InlineData("action_warp")]
    [InlineData("locked_door_warp")]
    [InlineData("building_door")]
    [InlineData("touch_action_warp")]
    [InlineData("magic_warp")]
    public void OtherConnectorContractsRetainTheirExistingStandRules(string kind) =>
        Assert.False(NativeCurrentWarpStandPolicy.CanUse(ObservedFacts() with { ConnectorKind = kind }));

    [Fact]
    public void OnePixelRealCollisionRemainsBlocked() =>
        Assert.False(NativeCurrentWarpStandPolicy.CanUse(ObservedFacts() with { CharacterBounds = [new(78, 111, 48, 32)] }));

    [Fact]
    public void EmptyKnownCharacterSetAllowsTheAlreadyOccupiedStand() =>
        Assert.True(NativeCurrentWarpStandPolicy.CanUse(ObservedFacts() with { CharacterBounds = [] }));

    private static CurrentWarpStandFacts ObservedFacts() => new()
    {
        ConnectorKind = "warp", ConnectorValidated = true,
        CurrentTileX = 1, CurrentTileY = 1, StandTileX = 1, StandTileY = 1,
        WarpTileX = 1, WarpTileY = 0, StandOnMap = true, StandHardWalkable = true, WarpHardWalkable = true,
        PlayerBounds = new(72, 80, 48, 32), CharacterBounds = [new(78, 112, 48, 32)]
    };
}
