using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;

// New state attached to Pawns

namespace BedOwnershipTools {
    public class CompPawnXAttrs : ThingComp {
        public Pawn parentPawn;
        public PawnXAttrs_AssignmentGroupTracker assignmentGroupTracker;
        public PawnXAttrs_AutomaticDeathrestTracker automaticDeathrestTracker;

        public CompPawnXAttrs() : base() {
            this.assignmentGroupTracker = new(this);
            this.automaticDeathrestTracker = new(this);
        }

        public override void Initialize(CompProperties props) {
            if (this.parent is not Pawn) {
                Log.Error("[BOT] Tried to create CompPawnXAttrs under a non-Pawn parent ThingWithComps.");
            }
            parentPawn = (Pawn)this.parent;
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compPawnXAttrsRegistry.Remove(this);
        }

        public override void PostExposeData() {
		    base.PostExposeData();

            assignmentGroupTracker.ShallowExposeData();
            automaticDeathrestTracker.ShallowExposeData();
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

                Gene_Deathrest gene_Deathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                if (gene_Deathrest != null) {
                    for (int i = 0; i < gene_Deathrest.BoundBuildings.Count; i++) {
                        stringBuilder.AppendInNewLine($"BoundBuildings[{i}]: {gene_Deathrest.BoundBuildings[i].GetUniqueLoadID()}");
                    }
                }

                stringBuilder.AppendInNewLine("automaticDeathrestMode: " + this.automaticDeathrestTracker.automaticDeathrestMode.ToString());
                stringBuilder.AppendInNewLine("tickCompletedLastDeathrest: " + this.automaticDeathrestTracker.tickCompletedLastDeathrest.ToString());

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
