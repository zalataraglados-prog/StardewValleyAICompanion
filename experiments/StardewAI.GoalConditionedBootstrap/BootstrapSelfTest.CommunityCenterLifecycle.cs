using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCommunityCenterDonationReceiptEvidence()
    {
        var credit = new CurrentCollectionRequirementCredit(
            "community_center_standard",
            "community_center:bundle:Pantry/5",
            0,
            "(O)24",
            "native_community_center_donation_completion",
            1,
            0,
            "native_donation_consumes_exact_projected_inventory");
        var queueItem = new ActionQueueItem
        {
            OptionId = "executor.donate_community_center_item",
            NormalizedCommand = new NormalizedCommand
            {
                Parameters = new[]
                {
                    CommunityCenterParameter("bundle_data_key", "Pantry/5"),
                    CommunityCenterParameter("bundle_id", "5"),
                    CommunityCenterParameter("bundle_area_id", "0"),
                    CommunityCenterParameter("bundle_ingredient_index", "0"),
                    CommunityCenterParameter("qualified_item_id", "(O)24"),
                    CommunityCenterParameter("required_stack", "1"),
                    CommunityCenterParameter("inventory_item_total_before", "3"),
                    CommunityCenterParameter("inventory_item_total_after", "2"),
                    CommunityCenterParameter("expected_bundle_completed_count_after", "1"),
                    CommunityCenterParameter("expected_bundle_complete_after", "true"),
                    CommunityCenterParameter("expected_bundle_reward_available_after", "true"),
                    CommunityCenterParameter("expected_complete_bundle_count_after", "1"),
                    CommunityCenterParameter("expected_area_complete_after", "true"),
                    CommunityCenterParameter("expected_area_completion_mail_pending_after", "true"),
                    CommunityCenterParameter("expected_bulletin_thank_you_pending_after", "false"),
                    CommunityCenterParameter("expected_all_areas_complete_after", "false"),
                    CommunityCenterParameter("newly_appearing_note_area_ids_json", "[2]")
                }
            }
        };
        var before = CommunityCenterDonationSnapshot(
            "cc-before",
            3,
            ingredientComplete: false,
            bundleComplete: false,
            rewardAvailable: false,
            completeBundleCount: 0,
            areaComplete: false,
            areaMailPending: false,
            newNoteAppears: false);
        var after = CommunityCenterDonationSnapshot(
            "cc-after",
            2,
            ingredientComplete: true,
            bundleComplete: true,
            rewardAvailable: true,
            completeBundleCount: 1,
            areaComplete: true,
            areaMailPending: true,
            newNoteAppears: true);
        var evidence = ExactCommunityCenterDonationReceiptVerifier.Verify(
            credit,
            queueItem,
            before,
            after);
        Require(evidence.Verified,
            "Exact Community Center donation side effects were not admitted: " +
            string.Join(",", evidence.BlockingReasons));

        var tampered = CommunityCenterDonationSnapshot(
            "cc-after-tampered",
            2,
            ingredientComplete: true,
            bundleComplete: true,
            rewardAvailable: true,
            completeBundleCount: 1,
            areaComplete: true,
            areaMailPending: false,
            newNoteAppears: true);
        var rejected = ExactCommunityCenterDonationReceiptVerifier.Verify(
            credit,
            queueItem,
            before,
            tampered);
        Require(!rejected.Verified && rejected.BlockingReasons.Contains(
                "community_center_donation_area_mail_mismatch"),
            "A Community Center receipt with a missing native room mail side effect was admitted.");

        var beforeFirstNote = CommunityCenterFirstNoteSnapshot(
            "cc-first-note-before",
            firstNoteSeen: false,
            wizardPending: false);
        var afterFirstNote = CommunityCenterFirstNoteSnapshot(
            "cc-first-note-after",
            firstNoteSeen: true,
            wizardPending: true);
        var firstNote = CommunityCenterLifecycleTransitionVerifier.Verify(
            CommunityCenterLifecycleTransitionVerifier.FirstJunimoNote,
            beforeFirstNote,
            afterFirstNote);
        Require(firstNote.Verified,
            "Fresh native first Junimo note transition was not admitted: " +
            string.Join(",", firstNote.BlockingReasons));
        var missingWizardLetter = CommunityCenterFirstNoteSnapshot(
            "cc-first-note-tampered",
            firstNoteSeen: true,
            wizardPending: false);
        var firstNoteRejected = CommunityCenterLifecycleTransitionVerifier.Verify(
            CommunityCenterLifecycleTransitionVerifier.FirstJunimoNote,
            beforeFirstNote,
            missingWizardLetter);
        Require(!firstNoteRejected.Verified &&
                firstNoteRejected.BlockingReasons.Contains(
                    "community_center_wizard_letter_schedule_transition_missing"),
            "A first Junimo note transition without the native wizard letter schedule was accepted.");

        var beforeCeremony = CommunityCenterCeremonySnapshot(
            "cc-ceremony-before",
            eventSeen: false,
            locationAccessible: false,
            completionAdmitted: false);
        var afterCeremony = CommunityCenterCeremonySnapshot(
            "cc-ceremony-after",
            eventSeen: true,
            locationAccessible: true,
            completionAdmitted: true);
        var ceremony = CommunityCenterLifecycleTransitionVerifier.Verify(
            CommunityCenterLifecycleTransitionVerifier.FinalCeremony,
            beforeCeremony,
            afterCeremony);
        Require(ceremony.Verified,
            "Fresh native Community Center ceremony transition was not admitted: " +
            string.Join(",", ceremony.BlockingReasons));
        var tamperedCeremony = CommunityCenterCeremonySnapshot(
            "cc-ceremony-tampered",
            eventSeen: true,
            locationAccessible: true,
            completionAdmitted: false);
        var ceremonyRejected = CommunityCenterLifecycleTransitionVerifier.Verify(
            CommunityCenterLifecycleTransitionVerifier.FinalCeremony,
            beforeCeremony,
            tamperedCeremony);
        Require(!ceremonyRejected.Verified &&
                ceremonyRejected.BlockingReasons.Contains(
                    "community_center_final_completion_admission_missing"),
            "A final ceremony without native terminal admission was accepted.");
    }

    private static SnapshotEnvelope CommunityCenterFirstNoteSnapshot(
        string stateHash,
        bool firstNoteSeen,
        bool wizardPending)
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            world_progress = new
            {
                community_center = Available(new
                {
                    lifecycle = new
                    {
                        projection_status = "complete_locked_base_1.6.15",
                        stage = firstNoteSeen
                            ? "wizard_letter_pending"
                            : "first_junimo_note_pending",
                        first_junimo_note_seen = firstNoteSeen,
                        wizard_letter_pending = wizardPending,
                        wizard_letter_received = false
                    }
                })
            }
        });
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = firstNoteSeen ? 2 : 1,
            State = json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal)
        };
    }

    private static SnapshotEnvelope CommunityCenterCeremonySnapshot(
        string stateHash,
        bool eventSeen,
        bool locationAccessible,
        bool completionAdmitted)
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            world_progress = new
            {
                community_center = Available(new
                {
                    location_accessible = locationAccessible,
                    community_center_complete_native = true,
                    completed_area_mail_flags = new[]
                    {
                        "ccBoilerRoom",
                        "ccCraftsRoom",
                        "ccPantry",
                        "ccFishTank",
                        "ccVault",
                        "ccBulletin"
                    },
                    pending_area_mail_flags = Array.Empty<string>(),
                    lifecycle = new
                    {
                        projection_status = "complete_locked_base_1.6.15",
                        stage = completionAdmitted
                            ? "completion_admitted"
                            : "final_ceremony_ready",
                        all_areas_complete = true,
                        all_area_completion_mails_received = true,
                        completion_admitted = completionAdmitted,
                        final_ceremony_event = new
                        {
                            event_id = "191393",
                            asset_locked = true,
                            event_seen = eventSeen,
                            event_active = false
                        }
                    }
                })
            }
        });
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = eventSeen ? 2 : 1,
            State = json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal)
        };
    }

    private static SnapshotEnvelope CommunityCenterDonationSnapshot(
        string stateHash,
        int inventoryStack,
        bool ingredientComplete,
        bool bundleComplete,
        bool rewardAvailable,
        int completeBundleCount,
        bool areaComplete,
        bool areaMailPending,
        bool newNoteAppears)
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            player = new
            {
                inventory = Available(new[]
                {
                    new
                    {
                        slot_index = 0,
                        qualified_item_id = "(O)24",
                        stack = inventoryStack,
                        quality = 0
                    }
                })
            },
            world_progress = new
            {
                community_center = Available(new
                {
                    lifecycle = new
                    {
                        all_areas_complete = false,
                        community_center_complete_flag_received = false
                    },
                    complete_bundle_count = completeBundleCount,
                    areas_complete = new[]
                    {
                        areaComplete,
                        false,
                        false,
                        false,
                        false,
                        false
                    },
                    bundle_rows = new object[]
                    {
                        new
                        {
                            projection_status = "exact",
                            bundle_data_key = "Pantry/5",
                            bundle_id = 5,
                            area_id = 0,
                            completed_ingredient_count = ingredientComplete ? 1 : 0,
                            complete = bundleComplete,
                            reward_available = rewardAvailable,
                            area_complete = areaComplete,
                            area_completion_mail_pending = areaMailPending,
                            bulletin_thank_you_pending = false,
                            note_appears = true,
                            ingredients = new[]
                            {
                                new
                                {
                                    ingredient_index = 0,
                                    item_id_or_category = "24",
                                    completed = ingredientComplete
                                }
                            }
                        },
                        new
                        {
                            projection_status = "exact",
                            bundle_data_key = "FishTank/6",
                            bundle_id = 6,
                            area_id = 2,
                            complete = false,
                            reward_available = false,
                            area_complete = false,
                            area_completion_mail_pending = false,
                            bulletin_thank_you_pending = false,
                            note_appears = newNoteAppears,
                            ingredients = Array.Empty<object>()
                        }
                    }
                })
            }
        });
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = stateHash == "cc-before" ? 1 : 2,
            State = json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal)
        };
    }

    private static SmallModelActionParameter CommunityCenterParameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static object Available(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "self_test", path = "community_center" },
        adapter = "self_test",
        read_at_tick = 1,
        confidence = 1
    };
}
