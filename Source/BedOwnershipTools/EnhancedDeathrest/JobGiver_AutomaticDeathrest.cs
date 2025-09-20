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
                    building_Bed = RestUtility.FindBedFor(pawn);
                    if (building_Bed != null) {
                        return JobMaker.MakeJob(JobDefOf.Deathrest, building_Bed);
                    }
                }
            }
            return null;
        }

        public Building_Bed FindDeathrestCasketFor(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return null;
            }
            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                    if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                        if (pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some)) {
                            pawn.ownership.ClaimDeathrestCasket(bed);
                            return bed;
                        }
                    }
                }
            } else {
                if (pawn.ownership.AssignedDeathrestCasket != null) {
                    if (pawn.CanReach(pawn.ownership.AssignedDeathrestCasket, PathEndMode.OnCell, Danger.Some)) {
                        return pawn.ownership.AssignedDeathrestCasket;
                    }
                }
            }
            // TODO if I'm bound to a deathrest casket, then use that deathrest casket
            return null;
        }

        public override ThinkNode DeepCopy(bool resolve = true) {
            JobGiver_GetDeathrest obj = (JobGiver_GetDeathrest)base.DeepCopy(resolve);
            return obj;
        }
    }
}
