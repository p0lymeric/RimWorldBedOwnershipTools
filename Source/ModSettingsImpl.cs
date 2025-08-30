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

        public bool showCommunalGUIOverlayInsteadOfBlankUnderBed = true;
        public bool hideGUIOverlayOnNonHumanlikeBeds = true;
        public bool showColonistsAcrossAllMapsInAssignmentDialog = true;

        public bool enableHospitalityModCompatPatches = true;
        // TODO bed linking in Dubs Hygiene is not aware of overlay owners

        public bool devEnableDebugInspectStringListings = false;
        public bool devEnableUnaccountedCaseLogging = false;

        public override void ExposeData() {
            Scribe_Values.Look(ref this.enableCommunalBeds, "enableCommunalBeds", true);
            Scribe_Values.Look(ref this.communalBedsSupportOrderedMedicalSleep, "communalBedsSupportOrderedMedicalSleep", true);

            Scribe_Values.Look(ref this.enableBedAssignmentPinning, "enableBedAssignmentPinning", true);
            Scribe_Values.Look(ref this.pawnsMaySelfAssignToUnownedPinnedBeds, "pawnsMaySelfAssignToUnownedPinnedBeds", true);

            Scribe_Values.Look(ref this.showCommunalGUIOverlayInsteadOfBlankUnderBed, "showCommunalGUIOverlayInsteadOfBlankUnderBed", true);
            Scribe_Values.Look(ref this.hideGUIOverlayOnNonHumanlikeBeds, "hideGUIOverlayOnNonHumanlikeBeds", true);
            Scribe_Values.Look(ref this.showColonistsAcrossAllMapsInAssignmentDialog, "showColonistsAcrossAllMapsInAssignmentDialog", true);

            Scribe_Values.Look(ref this.enableBedAssignmentGroups, "enableBedAssignmentGroups", true);

            Scribe_Values.Look(ref this.enableHospitalityModCompatPatches, "enableHospitalityModCompatPatches", true);

            Scribe_Values.Look(ref this.devEnableDebugInspectStringListings, "devEnableDebugInspectStringListings", false);
            Scribe_Values.Look(ref this.devEnableUnaccountedCaseLogging, "devEnableUnaccountedCaseLogging", false);
        }
    }
}