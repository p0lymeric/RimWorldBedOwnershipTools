using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BedOwnershipTools {
    public class JobGiver_AutomaticDeathrest : ThinkNode_JobGiver {
        public override float GetPriority(Pawn pawn) {
            if (!ModsConfig.BiotechActive) {
                return 0f;
            }
            Lord lord = pawn.GetLord();
            if (lord != null && !lord.CurLordToil.AllowSatisfyLongNeeds) {
                return 0f;
            }
            if (pawn.needs == null || pawn.needs.TryGetNeed<Need_Deathrest>() == null) {
                return 0f;
            }
            // TODO review priority
            return 7.75f;
        }

        protected override Job TryGiveJob(Pawn pawn) {
            if (!ModsConfig.BiotechActive) {
                return null;
            }
            if (pawn.needs == null || pawn.needs.TryGetNeed<Need_Deathrest>() == null) {
                return null;
            }
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return null;
            }

            if (!pawnXAttrs.automaticDeathrestTracker.ScheduleTest() && !pawn.Deathresting) {
                return null;
            }

            Lord lord = pawn.GetLord();
            Building_Bed building_Bed = null;
            if ((lord == null || lord.CurLordToil == null || lord.CurLordToil.AllowRestingInBed) && !pawn.IsWildMan() && (!pawn.InMentalState || pawn.MentalState.AllowRestingInBed)) {
                Pawn_RopeTracker roping = pawn.roping;
                if (roping == null || !roping.IsRoped) {
                    building_Bed = FindDeathrestCasketFor(pawn);
                    if (building_Bed != null) {
                        return JobMaker.MakeJob(JobDefOf.Deathrest, building_Bed);
                    }
                    if (!BedOwnershipTools.Singleton.settings.ignoreBedsForAutomaticDeathrest) {
                        building_Bed = RestUtility.FindBedFor(pawn);
                        if (building_Bed != null && !ThingTouchingSunlight(building_Bed)) {
                            return JobMaker.MakeJob(JobDefOf.Deathrest, building_Bed);
                        }
                    }
                }
            }
            return null;
        }

        public bool ThingTouchingSunlight(Thing thing) {
            if (thing != null && thing.Map != null) {
                foreach (IntVec3 coord in thing.OccupiedRect()) {
                    if (coord.InSunlight(thing.Map)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public Building_Bed FindDeathrestCasketFor(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return null;
            }
            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                    if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                        if (pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) && !ThingTouchingSunlight(bed)) {
                            pawn.ownership.ClaimDeathrestCasket(bed);
                            return bed;
                        }
                    }
                }
            } else {
                if (pawn.ownership.AssignedDeathrestCasket != null) {
                    if (pawn.CanReach(pawn.ownership.AssignedDeathrestCasket, PathEndMode.OnCell, Danger.Some) && !ThingTouchingSunlight(pawn.ownership.AssignedDeathrestCasket)) {
                        return pawn.ownership.AssignedDeathrestCasket;
                    }
                }
            }
            // TODO should search through some pawn-specific structure instead of a global registry
            // should also avoid redundant CanReach searches
            // what shall we do when we want things done?
            // FIXME should also account for assignment group order when searching through binds
            foreach (CompDeathrestBindableXAttrs cdbXAttrs in GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry) {
                if (cdbXAttrs.parent is Building_Bed bed) {
                    if (cdbXAttrs.boundPawnOverlay == pawn) {
                        if (pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) && !ThingTouchingSunlight(bed)) {
                            return bed;
                        }
                    }
                    CompDeathrestBindable cdb = cdbXAttrs.parent.GetComp<CompDeathrestBindable>();
                    if (cdb != null && cdb.BoundPawn == pawn) {
                        if (pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) && !ThingTouchingSunlight(bed)) {
                            return bed;
                        }
                    }
                }
            }
            return null;
        }

        public override ThinkNode DeepCopy(bool resolve = true) {
            JobGiver_GetDeathrest obj = (JobGiver_GetDeathrest)base.DeepCopy(resolve);
            return obj;
        }
    }
}
