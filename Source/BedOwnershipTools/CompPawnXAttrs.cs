using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

// New state attached to Pawns

namespace BedOwnershipTools {
    public class CompPawnXAttrs : ThingComp {
        public PawnXAttrs_AssignmentGroupTracker assignmentGroupTracker;

        public CompPawnXAttrs() : base() {
            this.assignmentGroupTracker = new(this);
        }

        public override void Initialize(CompProperties props) {
            if (this.parent is not Pawn) {
                Log.Error("[BOT] Tried to create CompPawnXAttrs under a non-Pawn parent ThingWithComps.");
            }
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Remove(this);
        }

        public override void PostExposeData() {
		    base.PostExposeData();

            assignmentGroupTracker.ShallowExposeData();
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

                stringBuilder.AppendInNewLine("CurrentJobDriver: ");
                if (pawn.jobs.curDriver != null) {
                    stringBuilder.Append($"{pawn.jobs.curDriver}");
                } else {
                    stringBuilder.Append("null");
                }

                stringBuilder.AppendInNewLine("TargetBed: ");
                if (pawn.CurJob != null && HarmonyPatches.ModCompatPatches_LoftBedBunkBeds.GetJobTargetedBedFromPawn(pawn, false, false) is Thing thing) {
                    stringBuilder.Append($"{thing.GetUniqueLoadID()}");
                } else {
                    stringBuilder.Append("null");
                }

                if (pawn.ownership.OwnedBed != null) {
                    stringBuilder.AppendInNewLine("INTERNAL: " + pawn.ownership.OwnedBed.GetUniqueLoadID());
                }

                foreach(var (assignmentGroup, bed3) in this.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    stringBuilder.AppendInNewLine(assignmentGroup.name + ": " + bed3.GetUniqueLoadID());
                }

                if (pawn.ownership.AssignedDeathrestCasket != null) {
                    stringBuilder.AppendInNewLine("INTERNALDC: " + pawn.ownership.AssignedDeathrestCasket.GetUniqueLoadID());
                }

                foreach(var (assignmentGroup, deathrestCasket) in this.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap) {
                    stringBuilder.AppendInNewLine(assignmentGroup.name + "DC: " + deathrestCasket.GetUniqueLoadID());
                }
            }
            return stringBuilder.ToString();
        }
    }
}
