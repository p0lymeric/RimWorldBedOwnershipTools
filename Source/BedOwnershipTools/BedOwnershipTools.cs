using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace BedOwnershipTools {
    public class BedOwnershipTools : Mod {
        public static BedOwnershipTools Singleton = null;
        public ModSettingsImpl settings = null;
        public RuntimeHandleProvider runtimeHandles = null;
        public Harmony harmony = null;

        public BedOwnershipTools(ModContentPack content) : base(content) {
            if (BedOwnershipTools.Singleton is not null) {
                Log.Error("[BOT] Game tried to initialize mod multiple times!");
            }
            BedOwnershipTools.Singleton = this;

            this.settings = GetSettings<ModSettingsImpl>();

            this.runtimeHandles = new RuntimeHandleProvider(settings);

            harmony = new("polymeric.bedownershiptools");
            HarmonyPatches.ApplyHarmonyPatches(this);
        }

        public override void DoSettingsWindowContents(Rect inRect) {
            const float MIDDLE_MARGIN = 8f;
            Rect rectLeftCol = new(inRect.x, inRect.y, inRect.width / 2 - MIDDLE_MARGIN, inRect.height);
            Rect rectRightCol = new(inRect.x + inRect.width / 2 + MIDDLE_MARGIN, inRect.y, inRect.width / 2 - MIDDLE_MARGIN, inRect.height);

            Listing_Standard listingStandard = new();

            listingStandard.Begin(rectLeftCol);
            listingStandard.Label("BedOwnershipTools.CommunalBedsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableCommunalBeds".Translate(),
                ref settings.enableCommunalBeds,
                "BedOwnershipTools.EnableCommunalBeds_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.CommunalBedsSupportOrderedMedicalSleep".Translate(),
                ref settings.communalBedsSupportOrderedMedicalSleep,
                "BedOwnershipTools.CommunalBedsSupportOrderedMedicalSleep_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AssignmentPinningHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableBedAssignmentPinning".Translate(),
                ref settings.enableBedAssignmentPinning,
                "BedOwnershipTools.EnableBedAssignmentPinning_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.PawnsMaySelfAssignToUnownedPinnedBeds".Translate(),
                ref settings.pawnsMaySelfAssignToUnownedPinnedBeds,
                "BedOwnershipTools.PawnsMaySelfAssignToUnownedPinnedBeds_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AssignmentGroupsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableBedAssignmentGroups".Translate(),
                ref settings.enableBedAssignmentGroups,
                "BedOwnershipTools.EnableBedAssignmentGroups_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.UseAssignmentGroupsForDeathrestCaskets".Translate(),
                ref settings.useAssignmentGroupsForDeathrestCaskets,
                "BedOwnershipTools.UseAssignmentGroupsForDeathrestCaskets_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.DeathrestBindingsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableSpareDeathrestBindings".Translate(),
                ref settings.enableSpareDeathrestBindings,
                "BedOwnershipTools.EnableSpareDeathrestBindings_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.DeathrestBindingsArePermanent".Translate(),
                ref settings.deathrestBindingsArePermanent,
                "BedOwnershipTools.DeathrestBindingsArePermanent_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AutomaticDeathrestHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableAutomaticDeathrest".Translate(),
                ref settings.enableAutomaticDeathrest,
                "BedOwnershipTools.EnableAutomaticDeathrest_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.IgnoreBedsForAutomaticDeathrest".Translate(),
                ref settings.ignoreBedsForAutomaticDeathrest,
                "BedOwnershipTools.IgnoreBedsForAutomaticDeathrest_Tooltip".Translate()
            );
            listingStandard.End();

            listingStandard = new();
            listingStandard.Begin(rectRightCol);
            listingStandard.Label("BedOwnershipTools.UICustomizationsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowCommunalGUIOverlayInsteadOfBlankUnderBed".Translate(),
                ref settings.showCommunalGUIOverlayInsteadOfBlankUnderBed,
                "BedOwnershipTools.ShowCommunalGUIOverlayInsteadOfBlankUnderBed_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.HideGUIOverlayOnNonHumanlikeBeds".Translate(),
                ref settings.hideGUIOverlayOnNonHumanlikeBeds,
                "BedOwnershipTools.HideGUIOverlayOnNonHumanlikeBeds_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowColonistsAcrossAllMapsInAssignmentDialog".Translate(),
                ref settings.showColonistsAcrossAllMapsInAssignmentDialog,
                "BedOwnershipTools.ShowColonistsAcrossAllMapsInAssignmentDialog_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.HideDeathrestAutoControlsOnPawnWhileAwake".Translate(),
                ref settings.hideDeathrestAutoControlsOnPawnWhileAwake,
                "BedOwnershipTools.HideDeathrestAutoControlsOnPawnWhileAwake_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowDeathrestAutoControlsOnCasket".Translate(),
                ref settings.showDeathrestAutoControlsOnCasket,
                "BedOwnershipTools.ShowDeathrestAutoControlsOnCasket_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.ModCompatibilityHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Hospitality"),
                ref settings.enableHospitalityModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("One bed to sleep with all - Polycule Edition"),
                ref settings.enableOneBedToSleepWithAllModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Loft Bed"),
                ref settings.enableLoftBedModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Bunk Beds"),
                ref settings.enableBunkBedsModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("MultiFloors"),
                ref settings.enableMultiFloorsModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );

            listingStandard.GapLine();
            if (listingStandard.ButtonText("BedOwnershipTools.ResetSettingsButton".Translate())) {
                settings.Reset();
            }

            if (Prefs.DevMode) {
                listingStandard.GapLine();
                listingStandard.Label("BedOwnershipTools.DeveloperSettingsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableDebugInspectStringListings".Translate(),
                    ref settings.devEnableDebugInspectStringListings,
                    "BedOwnershipTools.DevEnableDebugInspectStringListings_Tooltip".Translate()
                );
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableUnaccountedCaseLogging".Translate(),
                    ref settings.devEnableUnaccountedCaseLogging,
                    "BedOwnershipTools.DevEnableUnaccountedCaseLogging_Tooltip".Translate()
                );
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableExtraMenusAndGizmos".Translate(),
                    ref settings.devEnableExtraMenusAndGizmos,
                    "BedOwnershipTools.DevEnableExtraMenusAndGizmos_Tooltip".Translate()
                );
                if (listingStandard.ButtonText("BedOwnershipTools.ResetDeveloperSettingsButton".Translate())) {
                    settings.ResetDev();
                }
            }
            listingStandard.End();
        }

        public override string SettingsCategory() {
            return "Bed Ownership Tools";
        }

        public override void WriteSettings() {
            base.WriteSettings();
            // TODO rerun Harmony patches, but need to first check if repatching will cause any issues wrt patch ordering
            if (Current.ProgramState == ProgramState.Playing && GameComponent_AssignmentGroupManager.Singleton != null) {
                GameComponent_AssignmentGroupManager.Singleton.Notify_WriteSettings();
            }
        }
    }
}
