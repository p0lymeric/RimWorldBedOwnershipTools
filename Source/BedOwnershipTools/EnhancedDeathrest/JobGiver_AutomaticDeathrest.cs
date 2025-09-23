using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BedOwnershipTools {
    public class JobGiver_AutomaticDeathrest : ThinkNode_JobGiver {
        public override float GetPriority(Pawn pawn) {
            if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                return 0f;
            }
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

        public static Building_Bed TryFindBedOrDeathrestCasket(Pawn pawn) {
            Building_Bed building_Bed = FindDeathrestCasketFor(pawn);
            if (building_Bed != null) {
                return building_Bed;
            }
            if (!BedOwnershipTools.Singleton.settings.ignoreBedsForAutomaticDeathrest) {
                building_Bed = RestUtility.FindBedFor(pawn);
                if (building_Bed != null && !ThingTouchingSunlight(building_Bed)) {
                    return building_Bed;
                }
            }
            return null;
        }

        protected override Job TryGiveJob(Pawn pawn) {
            if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                return null;
            }
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
            if ((lord == null || lord.CurLordToil == null || lord.CurLordToil.AllowRestingInBed) && !pawn.IsWildMan() && (!pawn.InMentalState || pawn.MentalState.AllowRestingInBed)) {
                Pawn_RopeTracker roping = pawn.roping;
                if (roping == null || !roping.IsRoped) {
                    Building_Bed building_Bed = TryFindBedOrDeathrestCasket(pawn);
                    if (building_Bed != null) {
                        return JobMaker.MakeJob(JobDefOf.Deathrest, building_Bed);
                    }
                }
            }
            return null;
        }

        public static bool ThingTouchingSunlight(Thing thing) {
            if (thing != null && thing.Map != null) {
                foreach (IntVec3 coord in thing.OccupiedRect()) {
                    if (coord.InSunlight(thing.Map)) {
                        return true;
                    }
                }
            }
            return false;
        }

        public static Building_Bed FindDeathrestCasketFor(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return null;
            }
            // assignee wins over bindee for determining canonical owner
            // (this is only relevant if permanent deathrest bindings are disabled)

            // this list should be precalculated per deathrest gene
            // would be problematic if a world has a large number of deathrest buildings (large search space)
            // and a large population of deathresters who can't find a place to automatically deathrest (high frequency of calls)
            IEnumerable<Building_Bed> deathrestCasketsAssociatedWithPawn = GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry
                .Select(x => {
                    if (x.parent is Building_Bed bed) {
                        Pawn assignee = null;
                        Pawn bindee = null;
                        if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                            CompBuilding_BedXAttrs bedXAttrs = x.parent.GetComp<CompBuilding_BedXAttrs>();
                            assignee = bedXAttrs?.assignedPawnsOverlay.FirstOrDefault();
                        } else {
                            assignee = bed.OwnersForReading.FirstOrDefault();
                        }
                        if (BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                            bindee = x.boundPawnOverlay;
                        } else {
                            CompDeathrestBindable cdb = x.parent.GetComp<CompDeathrestBindable>();
                            bindee = cdb?.BoundPawn;
                        }
                        if (assignee == pawn) {
                            return bed;
                        } else if (assignee == null && bindee == pawn) {
                            return bed;
                        }
                    }
                    return null;
                })
                .Where(x => x != null);

            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                deathrestCasketsAssociatedWithPawn = deathrestCasketsAssociatedWithPawn.OrderBy(x => {
                    CompBuilding_BedXAttrs bedXAttrs = x.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs != null) {
                        return bedXAttrs.MyAssignmentGroup.Priority();
                    } else {
                        return int.MaxValue;
                    }
                });
            }

            foreach (Building_Bed bed in deathrestCasketsAssociatedWithPawn) {
                if (pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some) && !ThingTouchingSunlight(bed)) {
                    pawn.ownership.ClaimDeathrestCasket(bed);
                    return bed;
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
