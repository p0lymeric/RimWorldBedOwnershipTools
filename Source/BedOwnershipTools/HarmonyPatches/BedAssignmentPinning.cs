using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Pinned beds
// - while a pawn is searching for a bed, restrict the set of valid search candididates to exclude:
//   a) other ownable beds, in the case that sleeping in one would make them let go of a pinned bed
//   b) if forbidden by mod settings, unowned pinned beds, in the case that sleeping in one would bind them to a pinned assignment

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.IsValidBedFor))]
        public class Patch_RestUtility_IsValidBedFor {
            static void Postfix(ref bool __result, Thing bedThing, Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool allowMedBedEvenIfSetToNoCare = false, bool ignoreOtherReservations = false, GuestStatus? guestStatus = null) {
                // we need to typecheck here because when a Pawn is drafted carrying another Pawn
                // any right click that lands on top of something causes it to be queried with IsValidBedFor
                // POV: great... another exception... *click* *click*... wtf how does holding a baby make everything a bed???
                if (bedThing is Building_Bed building_Bed) {
                    bool pawnsMaySelfAssignToUnownedPinnedBeds = BedOwnershipTools.Singleton.settings.pawnsMaySelfAssignToUnownedPinnedBeds;
                    bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                    CompBuilding_BedXAttrs bedThingXAttrs = building_Bed.GetComp<CompBuilding_BedXAttrs>();
                    if (bedThingXAttrs == null) {
                        return;
                    }
                    // pass through original results for deathrest caskets, medical beds, prisoner beds, and communal beds
                    if ((ModsConfig.BiotechActive && building_Bed.def == ThingDefOf.DeathrestCasket) || building_Bed.Medical || building_Bed.ForPrisoners || bedThingXAttrs.IsAssignedToCommunity) {
                        return;
                    }

                    if (!pawnsMaySelfAssignToUnownedPinnedBeds && bedThingXAttrs.IsAssignmentPinned) {
                        // false if the bed has pinned ownership style and the pawn is disallowed from self-assigning
                        __result = false;
                        return;
                    }

                    // belongs to the assignment group feature
                    // CanUseBedNow !IsOwner && !BedOwnerWillShare
                    if (enableBedAssignmentGroups) {
                        if (!CATPBAndPOMethodReplacements.IsOwner(building_Bed, sleeper) && !CATPBAndPOMethodReplacements.BedOwnerWillShare(building_Bed, sleeper, guestStatus)) {
                            __result = false;
                        }
                    }

                    if (enableBedAssignmentGroups) {
                        CompPawnXAttrs sleeperXAttrs = sleeper.GetComp<CompPawnXAttrs>();
                        if (sleeperXAttrs == null) {
                            return;
                        }
                        foreach(var (assignmentGroup, bed) in sleeperXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                            if (building_Bed == bed) {
                                return;
                            }
                            CompBuilding_BedXAttrs ownedBedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                            if (ownedBedXAttrs == null) {
                                return;
                            }
                            // Pawns can own a destroyed bed (e.g. the pawn's previous bed before abandoning a map tile) so exclude that case
                            if (ownedBedXAttrs.IsAssignmentPinned && assignmentGroup == bedThingXAttrs.MyAssignmentGroup && !bed.Destroyed) {
                                // false if the Pawn holds ownership of a pinned bed in the same assignment group
                                __result = false;
                                return;
                            }
                        }
                    } else {
                        if (building_Bed != sleeper.ownership.OwnedBed) {
                            if (sleeper.ownership.OwnedBed != null) {
                                CompBuilding_BedXAttrs ownedBedXAttrs = sleeper.ownership.OwnedBed.GetComp<CompBuilding_BedXAttrs>();
                                if (ownedBedXAttrs == null) {
                                    return;
                                }
                                // Pawns can own a destroyed bed (e.g. the pawn's previous bed before abandoning a map tile) so exclude that case
                                if (ownedBedXAttrs.IsAssignmentPinned && !sleeper.ownership.OwnedBed.Destroyed) {
                                    // false if the Pawn holds ownership of a pinned bed
                                    __result = false;
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
