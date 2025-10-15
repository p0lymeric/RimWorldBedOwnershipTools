using System;
using System.Text;
using RimWorld;
using Verse;

// New state attached to Pawns

namespace BedOwnershipTools {
    public class CompPawnXAttrs : ThingComp {
        public Pawn parentPawn;
        public PawnXAttrs_AssignmentGroupTracker assignmentGroupTracker;
        public PawnXAttrs_AutomaticDeathrestTracker automaticDeathrestTracker;
        public PawnXAttrs_RelationshipAwareTracker relationshipAwareTracker;

        public CompPawnXAttrs() : base() {
            this.assignmentGroupTracker = new(this);
            this.automaticDeathrestTracker = new(this);
            this.relationshipAwareTracker = new(this);
        }

        public override void Initialize(CompProperties props) {
            if (this.parent is not Pawn) {
                Log.Error($"[BOT] Tried to create CompPawnXAttrs under a non-Pawn ({this.parent.GetType().Name}) parent ThingWithComps ({this.parent.GetUniqueLoadID()}).");
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
            relationshipAwareTracker.ShallowExposeData();
        }

        public string FormatTickAsDateTimeString(int offsetAbsTick) {
            return $"{GenDate.DayOfQuadrum(offsetAbsTick, 0) + 1} {GenDate.Quadrum(offsetAbsTick, 0).Label()} {GenDate.Year(offsetAbsTick, 0)} {GenDate.HourFloat(offsetAbsTick, 0):F1}h";
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
                if (pawn.CurJob != null && GameComponent_AssignmentGroupManager.Singleton.agmCompartment_JobDriverTargetBedLUT.GetJobTargetedBedFromPawn(pawn, insertCache: false, warn: false) is Thing thing) {
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
                stringBuilder.AppendInNewLine($"ScheduleTest: {(automaticDeathrestTracker.ScheduleTest() ? "Armed" : "Disarmed")}");
                float myLongitude = automaticDeathrestTracker.LongitudeForLocalDateCalc();
                int tickCompletedLastDeathrest = GenDate.TickGameToAbs(this.automaticDeathrestTracker.tickCompletedLastDeathrest) + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude);
                stringBuilder.AppendInNewLine($"tickCompletedLastDeathrest: {FormatTickAsDateTimeString(tickCompletedLastDeathrest)} LOC");
                int localTwelfthAbs = (Find.TickManager.TicksAbs + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude)) / GenDate.TicksPerTwelfth;
                int tickStartOfLocalTwelfthAnywhereOnPlanet = localTwelfthAbs * GenDate.TicksPerTwelfth - GenDate.TicksPerHour * 12 + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude);
                stringBuilder.AppendInNewLine($"tickStartOfLocalTwelfthAnywhereOnPlanet: {FormatTickAsDateTimeString(tickStartOfLocalTwelfthAnywhereOnPlanet)} LOC");
                int projectedExhaustionTick = Find.TickManager.TicksAbs + (int)Math.Round(automaticDeathrestTracker.TicksToDeathrestExhaustion()) + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude);
                stringBuilder.AppendInNewLine($"TicksToDeathrestExhaustion: {FormatTickAsDateTimeString(projectedExhaustionTick)} LOC");

                if (pawn.ownership.AssignedDeathrestCasket != null) {
                    stringBuilder.AppendInNewLine("INTERNALDC: " + pawn.ownership.AssignedDeathrestCasket.GetUniqueLoadID());
                }

                foreach(var (assignmentGroup, deathrestCasket) in this.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap) {
                    stringBuilder.AppendInNewLine(assignmentGroup.name + "DC: " + deathrestCasket.GetUniqueLoadID());
                }

                stringBuilder.AppendInNewLine("MyActiveOwnedBedIsOnThisMap: " + this.relationshipAwareTracker.MyActiveOwnedBedIsOnThisMap());
                stringBuilder.AppendInNewLine("IRecentlySleptInACommunalBed: " + this.relationshipAwareTracker.IRecentlySleptInACommunalBed());
                stringBuilder.AppendInNewLine($"tickGameLastSleptInCommunalBed: {FormatTickAsDateTimeString(GenDate.TickGameToAbs(this.relationshipAwareTracker.tickGameLastSleptInCommunalBed) + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude))}");
                stringBuilder.AppendInNewLine($"tickGameLastSleptWithPartner: {FormatTickAsDateTimeString(GenDate.TickGameToAbs(this.relationshipAwareTracker.tickGameLastSleptWithPartner) + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude))}");
                stringBuilder.AppendInNewLine($"tickGameLastSleptWithNonPartner: {FormatTickAsDateTimeString(GenDate.TickGameToAbs(this.relationshipAwareTracker.tickGameLastSleptWithNonPartner) + (int)GenDate.LocalTicksOffsetFromLongitude(myLongitude))}");
                stringBuilder.AppendInNewLine("sharedBedMoodOffset: " + this.relationshipAwareTracker.sharedBedMoodOffset);
                stringBuilder.AppendInNewLine("ThinkWantToSleepWithSpouseOrLover: " + this.relationshipAwareTracker.ThinkWantToSleepWithSpouseOrLover());
                stringBuilder.AppendInNewLine("ThinkSharedBed: " + this.relationshipAwareTracker.ThinkSharedBed());
            }
            return stringBuilder.ToString();
        }
    }
}
