using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class PawnXAttrs_AssignmentGroupTracker {
        public readonly CompPawnXAttrs parent;

        // Augments Pawn_Ownership.intOwnedBed
        // A pawn's tracker of all beds that it owns under the multiple bed assignment system
        public Dictionary<AssignmentGroup, Building_Bed> assignmentGroupToOwnedBedMap = new();
        private List<AssignmentGroup> assignmentGroupToOwnedBedMapKListForScribing = new();
        private List<Building_Bed>assignmentGroupToOwnedBedMapVListForScribing = new();

        // Augments Pawn_Ownership.AssignedDeathrestCasket
        // A pawn's tracker of all deathrest caskets that it owns under the multiple bed assignment system
        public Dictionary<AssignmentGroup, Building_Bed> assignmentGroupToAssignedDeathrestCasketMap = new();
        private List<AssignmentGroup> assignmentGroupToAssignedDeathrestCasketMapKListForScribing = new();
        private List<Building_Bed> assignmentGroupToAssignedDeathrestCasketMapVListForScribing = new();

        public PawnXAttrs_AssignmentGroupTracker(CompPawnXAttrs parent) {
            this.parent = parent;
        }

        public void ShallowExposeData() {
            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                Scribe_Collections.Look(
                    ref this.assignmentGroupToOwnedBedMap,
                    "BedOwnershipTools_assignmentGroupToOwnedBedMap",
                    LookMode.Reference, LookMode.Reference,
                    ref assignmentGroupToOwnedBedMapKListForScribing, ref assignmentGroupToOwnedBedMapVListForScribing
                );
                Scribe_Collections.Look(
                    ref this.assignmentGroupToAssignedDeathrestCasketMap,
                    "BedOwnershipTools_assignmentGroupToAssignedDeathrestCasketMap",
                    LookMode.Reference, LookMode.Reference,
                    ref assignmentGroupToAssignedDeathrestCasketMapKListForScribing, ref assignmentGroupToAssignedDeathrestCasketMapVListForScribing
                );
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit) {
                if (assignmentGroupToOwnedBedMap == null) {
                    assignmentGroupToOwnedBedMap = new();
                }
                if (assignmentGroupToAssignedDeathrestCasketMap == null) {
                    assignmentGroupToAssignedDeathrestCasketMap = new();
                }
                // rest of fixup including null handling is done in AGMCompartment_AssignmentGroups
		    }
	    }
    }
}
