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
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
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
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableBedAssignmentGroups".Translate(),
                ref settings.enableBedAssignmentGroups,
                "BedOwnershipTools.EnableBedAssignmentGroups_Tooltip".Translate()
            );

            listingStandard.GapLine();
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

            listingStandard.GapLine();
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableHospitalityModCompatPatches".Translate(),
                ref settings.enableHospitalityModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );

            if (Prefs.DevMode) {
                listingStandard.GapLine();
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
