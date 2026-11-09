using System;
using System.Collections.Generic;

namespace StardewAI.Contracts.Training;

public static class NativeDebrisObservationDomain
{
    public const int MaximumChunks = 4096;
    public const int MaximumEventOccurrences = 4096;
    public const int MaximumQueueEntries = 256;
    public const int MaximumHandlers = 256;
    public const string NativeModuleId = "aa1f513a-94f0-4ef6-a35f-2d5b4a3721bf";
    public const string PauseEventKind = "native.pause_event.bool";

    public static string? DebrisTypeName(int value) => value switch
    {
        0 => "CHUNKS",
        1 => "LETTERS",
        3 => "ARCHAEOLOGY",
        4 => "OBJECT",
        5 => "SPRITECHUNKS",
        6 => "RESOURCE",
        7 => "NUMBERS",
        _ => null
    };
}

// Exact nonabstract native Character descendants from the pinned image, metadata only.
public static class NativeObservedCharacterTypes
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "StardewValley.Character",
        "StardewValley.Characters.Cat",
        "StardewValley.Characters.Child",
        "StardewValley.Characters.Dog",
        "StardewValley.Characters.Horse",
        "StardewValley.Characters.Junimo",
        "StardewValley.Characters.JunimoHarvester",
        "StardewValley.Characters.Pet",
        "StardewValley.Characters.Raccoon",
        "StardewValley.Characters.TrashBear",
        "StardewValley.FarmAnimal",
        "StardewValley.Farmer",
        "StardewValley.Monsters.AngryRoger",
        "StardewValley.Monsters.Bat",
        "StardewValley.Monsters.BigSlime",
        "StardewValley.Monsters.BlueSquid",
        "StardewValley.Monsters.Bug",
        "StardewValley.Monsters.DinoMonster",
        "StardewValley.Monsters.Duggy",
        "StardewValley.Monsters.DustSpirit",
        "StardewValley.Monsters.DwarvishSentry",
        "StardewValley.Monsters.Fly",
        "StardewValley.Monsters.Ghost",
        "StardewValley.Monsters.GreenSlime",
        "StardewValley.Monsters.Grub",
        "StardewValley.Monsters.HotHead",
        "StardewValley.Monsters.LavaLurk",
        "StardewValley.Monsters.Leaper",
        "StardewValley.Monsters.MetalHead",
        "StardewValley.Monsters.Monster",
        "StardewValley.Monsters.Mummy",
        "StardewValley.Monsters.RockCrab",
        "StardewValley.Monsters.RockGolem",
        "StardewValley.Monsters.Serpent",
        "StardewValley.Monsters.ShadowBrute",
        "StardewValley.Monsters.ShadowGirl",
        "StardewValley.Monsters.ShadowGuy",
        "StardewValley.Monsters.ShadowShaman",
        "StardewValley.Monsters.Shooter",
        "StardewValley.Monsters.Skeleton",
        "StardewValley.Monsters.Spiker",
        "StardewValley.Monsters.SquidKid",
        "StardewValley.NPC",
    };
    public static bool IsSupported(string? value) => value is not null && Names.Contains(value);
}
