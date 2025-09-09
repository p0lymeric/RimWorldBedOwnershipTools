using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

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

        public override string CompInspectStringExtra() {
            if (!Prefs.DevMode || !BedOwnershipTools.Singleton.settings.devEnableDebugInspectStringListings) {
                return "";
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (this.parent is Pawn pawn) {
                stringBuilder.AppendInNewLine("CurrentBed: ");
                int? sleepingSlotPos = -1;
                Building_Bed bed = pawn.CurrentBed(out sleepingSlotPos);
                if (bed != null) {
                    stringBuilder.Append($"{bed.GetUniqueLoadID()} {sleepingSlotPos}");
                } else {
                    stringBuilder.Append("null");
                }

                stringBuilder.AppendInNewLine("LayDownTargetA: ");
                if (pawn.CurJob != null && pawn.CurJobDef == JobDefOf.LayDown && pawn.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed2) {
                    stringBuilder.Append($"{bed2.GetUniqueLoadID()}");
                } else {
                    stringBuilder.Append("null");
                }

                if (pawn.ownership.OwnedBed != null) {
                    stringBuilder.AppendInNewLine("INTERNAL " + pawn.ownership.OwnedBed.GetUniqueLoadID());
                }

                foreach(var (assignmentGroup, bed3) in this.assignmentGroupToOwnedBedMap) {
                    stringBuilder.AppendInNewLine(assignmentGroup.name + " " + bed3.GetUniqueLoadID());
                }
            }
            return stringBuilder.ToString();
        }
    }
}
