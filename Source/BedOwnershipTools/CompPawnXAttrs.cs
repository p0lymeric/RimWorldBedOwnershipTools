using System.Collections.Generic;
using RimWorld;
using Verse;

// New state attached to Pawns

namespace BedOwnershipTools {
    public class CompPawnXAttrs : ThingComp {
        // Augments Pawn_Ownership.intOwnedBed
        // A pawn's internal tracker of all beds that it owns
        public Dictionary<AssignmentGroup, Building_Bed> assignmentGroupToOwnedBedMap = new Dictionary<AssignmentGroup, Building_Bed>();
        private List<AssignmentGroup> assignmentGroupTmpListForScribing = new List<AssignmentGroup>();
        private List<Building_Bed> ownedBedTmpListForScribing = new List<Building_Bed>();

        public override void Initialize(CompProperties props) {
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Remove(this);
        }

        public override void PostExposeData() {
		    base.PostExposeData();
            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                Scribe_Collections.Look(
                    ref this.assignmentGroupToOwnedBedMap,
                    "BedOwnershipTools_assignmentGroupToOwnedBedMap",
                    LookMode.Reference, LookMode.Reference,
                    ref assignmentGroupTmpListForScribing, ref ownedBedTmpListForScribing
                );
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit) {
                if (assignmentGroupToOwnedBedMap == null) {
                    assignmentGroupToOwnedBedMap = new Dictionary<AssignmentGroup, Building_Bed>();
                }
                // rest of fixup including null handling is done in GameComponent_AssignmentGroupManager
		    }
	    }
    }
}
