using System.Reflection;
using RimWorld;
using Verse;

// Global mod settings

namespace BedOwnershipTools {
    public class ModSettingsImpl : ModSettings {
        public bool enableCommunalBeds = true;
        public bool communalBedsSupportOrderedMedicalSleep = true;

        public bool enableBedAssignmentPinning = true;
        public bool pawnsMaySelfAssignToUnownedPinnedBeds = true;

        public bool enableBedAssignmentGroups = true;
        public bool useAssignmentGroupsForDeathrestCaskets = true;

        public bool enableAutomaticDeathrest = true;

        public bool showCommunalGUIOverlayInsteadOfBlankUnderBed = true;
        public bool hideGUIOverlayOnNonHumanlikeBeds = true;
        public bool showColonistsAcrossAllMapsInAssignmentDialog = true;
        public bool hideDeathrestAutoControlsOnPawnWhileAwake = true;
        public bool showDeathrestAutoControlsOnCasket = true;

        public bool enableHospitalityModCompatPatches = true;
        public bool enableOneBedToSleepWithAllModCompatPatches = true;
        public bool enableLoftBedModCompatPatches = true;
        public bool enableBunkBedsModCompatPatches = true;
        public bool enableMultiFloorsModCompatPatches = true;
        // TODO bed linking in Dubs Hygiene is not aware of overlay owners

        public bool devEnableDebugInspectStringListings = false;
        public bool devEnableUnaccountedCaseLogging = false;
        public bool devEnableExtraMenusAndGizmos = false;

        public void Reset() {
            this.enableCommunalBeds = true;
            this.communalBedsSupportOrderedMedicalSleep = true;

            this.enableBedAssignmentPinning = true;
            this.pawnsMaySelfAssignToUnownedPinnedBeds = true;

            this.enableBedAssignmentGroups = true;
            this.useAssignmentGroupsForDeathrestCaskets = true;

            this.enableAutomaticDeathrest = true;

            this.showCommunalGUIOverlayInsteadOfBlankUnderBed = true;
            this.hideGUIOverlayOnNonHumanlikeBeds = true;
            this.showColonistsAcrossAllMapsInAssignmentDialog = true;
            this.hideDeathrestAutoControlsOnPawnWhileAwake = true;
            this.showDeathrestAutoControlsOnCasket = true;

            this.enableHospitalityModCompatPatches = true;
            this.enableOneBedToSleepWithAllModCompatPatches = true;
            this.enableLoftBedModCompatPatches = true;
            this.enableBunkBedsModCompatPatches = true;
            this.enableMultiFloorsModCompatPatches = true;
        }

        public void ResetDev() {
            this.devEnableDebugInspectStringListings = false;
            this.devEnableUnaccountedCaseLogging = false;
            this.devEnableExtraMenusAndGizmos = false;
        }

        public override void ExposeData() {
            Scribe_Values.Look(ref this.enableCommunalBeds, "enableCommunalBeds", true);
            Scribe_Values.Look(ref this.communalBedsSupportOrderedMedicalSleep, "communalBedsSupportOrderedMedicalSleep", true);

            Scribe_Values.Look(ref this.enableBedAssignmentPinning, "enableBedAssignmentPinning", true);
            Scribe_Values.Look(ref this.pawnsMaySelfAssignToUnownedPinnedBeds, "pawnsMaySelfAssignToUnownedPinnedBeds", true);

            Scribe_Values.Look(ref this.enableBedAssignmentGroups, "enableBedAssignmentGroups", true);
            Scribe_Values.Look(ref this.useAssignmentGroupsForDeathrestCaskets, "useAssignmentGroupsForDeathrestCaskets", true);

            Scribe_Values.Look(ref this.enableAutomaticDeathrest, "enableAutomaticDeathrest", true);

            Scribe_Values.Look(ref this.showCommunalGUIOverlayInsteadOfBlankUnderBed, "showCommunalGUIOverlayInsteadOfBlankUnderBed", true);
            Scribe_Values.Look(ref this.hideGUIOverlayOnNonHumanlikeBeds, "hideGUIOverlayOnNonHumanlikeBeds", true);
            Scribe_Values.Look(ref this.showColonistsAcrossAllMapsInAssignmentDialog, "showColonistsAcrossAllMapsInAssignmentDialog", true);
            Scribe_Values.Look(ref this.hideDeathrestAutoControlsOnPawnWhileAwake, "hideDeathrestAutoControlsOnPawnWhileAwake", true);
            Scribe_Values.Look(ref this.showDeathrestAutoControlsOnCasket, "showDeathrestAutoControlsOnCasket", true);

            Scribe_Values.Look(ref this.enableHospitalityModCompatPatches, "enableHospitalityModCompatPatches", true);
            Scribe_Values.Look(ref this.enableOneBedToSleepWithAllModCompatPatches, "enableOneBedToSleepWithAllModCompatPatches", true);
            Scribe_Values.Look(ref this.enableLoftBedModCompatPatches, "enableLoftBedModCompatPatches", true);
            Scribe_Values.Look(ref this.enableBunkBedsModCompatPatches, "enableBunkBedsModCompatPatches", true);
            Scribe_Values.Look(ref this.enableMultiFloorsModCompatPatches, "enableMultiFloorsModCompatPatches", true);

            Scribe_Values.Look(ref this.devEnableDebugInspectStringListings, "devEnableDebugInspectStringListings", false);
            Scribe_Values.Look(ref this.devEnableUnaccountedCaseLogging, "devEnableUnaccountedCaseLogging", false);
            Scribe_Values.Look(ref this.devEnableExtraMenusAndGizmos, "devEnableExtraMenusAndGizmos", false);
        }
    }
}
