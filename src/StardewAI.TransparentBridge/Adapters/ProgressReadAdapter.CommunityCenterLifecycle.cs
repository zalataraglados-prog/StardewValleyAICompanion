using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private const string CommunityCenterInitialUnlockEventId = "611439";
    private const string CommunityCenterInitialUnlockEventKey =
        "611439/j 4/t 800 1300/w sunny/a 0 54/H";
    private const string CommunityCenterInitialUnlockScriptSha256 =
        "630a011f708dd6751dd98ab5468c11fcd8cd5ad2fe5910234d6a907b56e692a1";
    private const string CommunityCenterJunimoTextEventId = "112";
    private const string CommunityCenterJunimoTextEventKey =
        "112/n seenJunimoNote";
    private const string CommunityCenterJunimoTextScriptSha256 =
        "a5890ddeb05228be92acd2b895ee8313eaeb46202f4b03d0b429c7f391fd0dc9";
    private const string CommunityCenterFinalCeremonyEventId = "191393";
    private const string CommunityCenterFinalCeremonyEventKey =
        "191393/Hn ccFishTank/Hn ccBulletin/Hn ccPantry/Hn ccVault/Hn ccBoilerRoom/Hn ccCraftsRoom/Hl jojaFishTank/Hl jojaPantry/Hl jojaVault/Hl jojaBoilerRoom/Hl jojaCraftsRoom/Hl JojaMember/w sunny/H";
    private const string CommunityCenterFinalCeremonyScriptSha256 =
        "ed18314c06e19b54f07e3e6fddfb1a1953dcd84d8166232d0b99ea5760083ff8";

    private static CommunityCenterLifecycleRef ReadCommunityCenterLifecycle(
        Farmer master,
        Farmer actor,
        CommunityCenter communityCenter,
        IReadOnlyCollection<string> areaMailIds)
    {
        var initialUnlock = ReadCommunityCenterEventAsset(
            "Data\\Events\\Town",
            CommunityCenterInitialUnlockEventId,
            CommunityCenterInitialUnlockEventKey,
            CommunityCenterInitialUnlockScriptSha256,
            actor);
        var junimoText = ReadCommunityCenterEventAsset(
            "Data\\Events\\WizardHouse",
            CommunityCenterJunimoTextEventId,
            CommunityCenterJunimoTextEventKey,
            CommunityCenterJunimoTextScriptSha256,
            actor);
        var finalCeremony = ReadCommunityCenterEventAsset(
            "Data\\Events\\Town",
            CommunityCenterFinalCeremonyEventId,
            CommunityCenterFinalCeremonyEventKey,
            CommunityCenterFinalCeremonyScriptSha256,
            actor);
        var allAssetsLocked = initialUnlock.AssetLocked &&
            junimoText.AssetLocked &&
            finalCeremony.AssetLocked;
        var allAreasComplete = communityCenter.areAllAreasComplete();
        var allAreaMailsReceived = areaMailIds.All(master.mailReceived.Contains);
        var ccFlagReceived = master.mailReceived.Contains("ccIsComplete");
        var ccFlagPending = HasPendingMail(master, "ccIsComplete") && !ccFlagReceived;
        var unclaimedRewards = communityCenter.bundleRewards.Pairs
            .Where(pair => pair.Value)
            .Select(pair => pair.Key)
            .OrderBy(id => id)
            .ToArray();
        var areaByBundle = Game1.netWorldState.Value.BundleData.Keys
            .Select(key => key.Split('/'))
            .Where(parts => parts.Length >= 2 && int.TryParse(parts[1], out _))
            .GroupBy(parts => parts[1], StringComparer.Ordinal)
            .Select(group => group.First())
            .ToDictionary(
                parts => int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                parts => CommunityCenter.getAreaNumberFromName(parts[0]));
        var missedRewardBundles = unclaimedRewards
            .Where(id => areaByBundle.TryGetValue(id, out var areaId) &&
                areaId >= 0 &&
                areaId < communityCenter.areasComplete.Count &&
                communityCenter.areasComplete[areaId])
            .ToArray();
        var jojaRoomFlags = new[]
        {
            "jojaFishTank",
            "jojaPantry",
            "jojaVault",
            "jojaBoilerRoom",
            "jojaCraftsRoom",
            "JojaMember"
        };
        var town = Game1.getLocationFromName("Town");
        var finalCeremonyReady = finalCeremony.AssetLocked &&
            Game1.IsMasterGame &&
            allAreaMailsReceived &&
            jojaRoomFlags.All(flag => !master.mailReceived.Contains(flag)) &&
            town is not null &&
            !town.IsRainingHere() &&
            !finalCeremony.EventSeen;
        var locationAccessible = Game1.isLocationAccessible("CommunityCenter");
        var completionAdmitted = master.hasCompletedCommunityCenter() &&
            finalCeremony.EventSeen &&
            locationAccessible;
        var canReadReceived = actor.mailReceived.Contains("canReadJunimoText");
        var canReadPending = HasPendingMail(actor, "canReadJunimoText") &&
            !canReadReceived;
        var doorUnlockReceived = actor.mailReceived.Contains("ccDoorUnlock");
        var doorUnlockPending = HasPendingMail(actor, "ccDoorUnlock") &&
            !doorUnlockReceived;
        var wizardLetterReceived = actor.mailReceived.Contains("wizardJunimoNote");
        var wizardLetterPending = HasPendingMail(actor, "wizardJunimoNote") &&
            !wizardLetterReceived;

        return new CommunityCenterLifecycleRef
        {
            ProjectionStatus = allAssetsLocked
                ? "complete_locked_base_1.6.15"
                : "blocked_base_event_asset_missing_or_modified",
            NativeContract =
                "Town.611439->ccDoorUnlock;JunimoNoteMenu->seenJunimoNote+wizardJunimoNote;WizardHouse.112->canReadJunimoText;CommunityCenter.areaCompleteReward->ccRoomMail;Junimo.returnToJunimoHutToFetchStar->ccIsComplete;Farmer.hasCompletedCommunityCenter;Town.191393->location_accessible",
            Stage = completionAdmitted
                ? "completion_admitted"
                : finalCeremony.EventActive
                    ? "final_ceremony_active"
                    : finalCeremonyReady
                        ? "final_ceremony_ready"
                        : allAreaMailsReceived
                            ? "native_completion_settled"
                            : allAreasComplete
                                ? "area_mail_settlement_pending"
                                : canReadReceived || canReadPending
                                    ? "bundle_donation_active"
                                    : wizardLetterReceived || wizardLetterPending
                                        ? "junimo_text_event_pending"
                                        : actor.mailReceived.Contains("seenJunimoNote")
                                            ? "wizard_letter_pending"
                                            : doorUnlockReceived || doorUnlockPending
                                                ? "first_junimo_note_pending"
                                                : initialUnlock.EventActive
                                                    ? "initial_unlock_event_active"
                                                    : "initial_unlock_pending",
            InitialUnlockEvent = initialUnlock,
            DoorUnlockReceived = doorUnlockReceived,
            DoorUnlockPending = doorUnlockPending,
            FirstJunimoNoteSeen = actor.mailReceived.Contains("seenJunimoNote"),
            WizardLetterReceived = wizardLetterReceived,
            WizardLetterPending = wizardLetterPending,
            JunimoTextEvent = junimoText,
            CanReadJunimoTextReceived = canReadReceived,
            CanReadJunimoTextPending = canReadPending,
            AllAreasComplete = allAreasComplete,
            UnclaimedBundleRewardIds = unclaimedRewards,
            MissedRewardBundleIds = missedRewardBundles,
            MissedRewardsChestVisible = communityCenter.missedRewardsChestVisible.Value,
            CommunityCenterCompleteFlagReceived = ccFlagReceived,
            CommunityCenterCompleteFlagPending = ccFlagPending,
            AllAreaCompletionMailsReceived = allAreaMailsReceived,
            FinalCeremonyEvent = finalCeremony,
            FinalCeremonyReady = finalCeremonyReady,
            CompletionAdmitted = completionAdmitted
        };
    }

    private static CommunityCenterEventAssetRef ReadCommunityCenterEventAsset(
        string assetName,
        string eventId,
        string expectedKey,
        string expectedScriptSha256,
        Farmer actor)
    {
        try
        {
            var localized = Game1.content.Load<Dictionary<string, string>>(assetName);
            var baseEnglish = Game1.content.Load<Dictionary<string, string>>(
                assetName,
                LocalizedContentManager.LanguageCode.en);
            var localizedEntry = FindCommunityCenterEvent(localized, eventId);
            var baseEnglishEntry = FindCommunityCenterEvent(baseEnglish, eventId);
            var localizedHash = CommunityCenterLifecycleSha256(
                localizedEntry.Value ?? string.Empty);
            var baseEnglishHash = CommunityCenterLifecycleSha256(
                baseEnglishEntry.Value ?? string.Empty);
            var assetLocked = localizedEntry.Key == expectedKey &&
                baseEnglishEntry.Key == expectedKey &&
                baseEnglishHash == expectedScriptSha256;
            return new CommunityCenterEventAssetRef
            {
                EventId = eventId,
                SourceAsset = assetName.Replace('\\', '/'),
                ExpectedEventKey = expectedKey,
                EventKey = localizedEntry.Key ?? string.Empty,
                EventKeyMatchesLockedBase = localizedEntry.Key == expectedKey,
                EventScriptSha256 = localizedHash,
                EventScriptLanguage = LocalizedContentManager.CurrentLanguageCode.ToString(),
                EventScriptIsLocalized = localizedHash != baseEnglishHash,
                BaseEnglishEventKey = baseEnglishEntry.Key ?? string.Empty,
                BaseEnglishEventScriptSha256 = baseEnglishHash,
                BaseEnglishEventScriptMatchesLockedBase =
                    baseEnglishHash == expectedScriptSha256,
                AssetLocked = assetLocked,
                EventSeen = actor.eventsSeen.Contains(eventId),
                EventActive = string.Equals(
                    Game1.CurrentEvent?.id,
                    eventId,
                    StringComparison.Ordinal)
            };
        }
        catch
        {
            return new CommunityCenterEventAssetRef
            {
                EventId = eventId,
                SourceAsset = assetName.Replace('\\', '/'),
                ExpectedEventKey = expectedKey,
                EventSeen = actor.eventsSeen.Contains(eventId),
                EventActive = string.Equals(
                    Game1.CurrentEvent?.id,
                    eventId,
                    StringComparison.Ordinal)
            };
        }
    }

    private static KeyValuePair<string, string> FindCommunityCenterEvent(
        IReadOnlyDictionary<string, string> events,
        string eventId) => events
        .Where(entry => Event.SplitPreconditions(entry.Key).FirstOrDefault() == eventId)
        .OrderBy(entry => entry.Key, StringComparer.Ordinal)
        .FirstOrDefault();

    private static string CommunityCenterLifecycleSha256(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }
}
