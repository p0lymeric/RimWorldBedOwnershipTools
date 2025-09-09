using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_LoftBedBunkBeds {

            // TPS with 224 chickens in 224 animal sleeping spots
            // (to gauge overhead impact on non-loft, non-bunk beds)
            // -----------------------------------------------------
            // profiler only: 740-800 TPS
            // Bed Ownership Tools: 750-780 TPS
            // Bunk Beds: 690-720 TPS
            // Loft Bed: 690-720 TPS
            // Bed Ownership Tools x Bunk Beds: 620-660 TPS
            // Bed Ownership Tools (1.0.5) x Loft Bed: 680-720 TPS
            // Bed Ownership Tools x Loft Bed: 680-710 TPS

            [HarmonyPatchCategory("LoftBedBunkBedsModCompatPatches")]
            [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CurrentBed))]
            [HarmonyPatch(new System.Type[] { typeof(Pawn), typeof(int?) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out })]
            class Patch_RestUtility_CurrentBed {
                static bool Prefix(ref Building_Bed __result, Pawn p, ref int? sleepingSlot) {
                    // Iterate through the sleeping Pawns and Building_Beds at the given pawn's position
                    // and map sleepers to the bed in their job's TargetA

                    // This implementation differs from Loft Bed and Bunk Beds' implementations
                    // because it checks the bed listed in the Pawn's job instead of ownership lists
                    // and trusts that reservation conflicts are handled by other systems in the games

                    // side-effect: Pawns won't sleep in the same order as their assignments with Bunk Beds

                    if (!p.Spawned || p.CurJob == null || !p.GetPosture().InBed()) {
                        __result = null;
                        sleepingSlot = null;
                        return false;
                    }

                    bool checkLoftBed = BedOwnershipTools.Singleton.runtimeHandles.modLoftBedLoadedForCompatPatching;
                    bool checkBunkBeds = BedOwnershipTools.Singleton.runtimeHandles.modBunkBedsLoadedForCompatPatching;

                    List<Thing> thingList = p.Position.GetThingList(p.Map);
                    List<Building_Bed> beds = new List<Building_Bed>(2);
                    List<Pawn> pawns = new List<Pawn>(4);
                    for (int i = 0; i < thingList.Count; i++) {
                        if (thingList[i] is Building_Bed bed) {
                            beds.Add(bed);
                        }
                        if (checkBunkBeds && thingList[i] is Pawn pawn && pawn.Spawned && pawn.CurJob != null && pawn.GetPosture().InBed() && pawn.CurJobDef == JobDefOf.LayDown) {
                            pawns.Add(pawn);
                        }
                    }

                    int bedsCnt = beds.Count;

                    // vanilla case handling with 0 beds
                    if (bedsCnt == 0) {
                        __result = null;
                        sleepingSlot = null;
                        return false;
                    }

                    bool isLoftBed = checkLoftBed && bedsCnt > 1;
                    bool isSingleBunkBed = checkBunkBeds && bedsCnt == 1 && HarmonyPatches.ModCompatPatches_BunkBeds.RemoteCall_IsBunkBed(beds[0]);
                    // fun, pathological case
                    bool couldBeLoftBedXBunkBedsCrossing = isLoftBed && checkBunkBeds && !isSingleBunkBed;

                    // vanilla case handling with 1 bed
                    // if (bedsCnt == 1 && !isSingleBunkBed) {
                    //     Building_Bed building_Bed = beds[0];
                    //     for (int j = 0; j < building_Bed.SleepingSlotsCount; j++) {
                    //         if (building_Bed.GetCurOccupant(j) == p) {
                    //             sleepingSlot = j;
                    //             __result = building_Bed;
                    //             return false;
                    //         }
                    //     }
                    // }

                    // loft/bunk case handling
                    if (p.CurJobDef == JobDefOf.LayDown) {
                        if (p.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                            if (beds.Contains(bed)) {
                                if (isSingleBunkBed) {
                                    // abusive indexing but let's hope that the order of still things returned by ThingsListAt is stable
                                    int slotIndex = pawns.IndexOf(p);
                                    if (slotIndex >= 0 && slotIndex < bed.SleepingSlotsCount) {
                                        // Log.Message($"{p.Label} called CurrentBed and matched {bed.GetUniqueLoadID()}");
                                        __result = bed;
                                        sleepingSlot = slotIndex;
                                        return false;
                                    }
                                // vanilla-like case handling with 1 bed OR Loft Bed handling
                                // main difference is that we don't call GetCurOccupant for a modest perf win
                                } else if (bedsCnt == 1 || (isLoftBed && !couldBeLoftBedXBunkBedsCrossing)) {
                                    int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed.Position, bed.Rotation, bed.def.size);
                                    if (sleepingSlotTmp >= 0) {
                                        // Log.Message($"{p.Label} called CurrentBed and matched {bed.GetUniqueLoadID()}");
                                        __result = bed;
                                        sleepingSlot = sleepingSlotTmp;
                                        return false;
                                    }
                                } else if (couldBeLoftBedXBunkBedsCrossing) {
                                    // yes, we handle the pathological case where the player has both mods installed and placed a loft bed atop a bunk bed
                                    // this requires extra filtering that are deferred to this scope and isn't expected to be common case
                                    if (HarmonyPatches.ModCompatPatches_BunkBeds.RemoteCall_IsBunkBed(bed)) {
                                        List<Pawn> pawnsInThisBed = pawns.Where(x => x.CurJob.GetTarget(TargetIndex.A).Thing == bed).ToList();
                                        int slotIndex = pawnsInThisBed.IndexOf(p);
                                        if (slotIndex >= 0 && slotIndex < bed.SleepingSlotsCount) {
                                            // Log.Message($"{p.Label} called CurrentBed and matched {bed.GetUniqueLoadID()}");
                                            __result = bed;
                                            sleepingSlot = slotIndex;
                                            return false;
                                        }
                                    } else if (isLoftBed) {
                                        int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed.Position, bed.Rotation, bed.def.size);
                                        if (sleepingSlotTmp >= 0) {
                                            // Log.Message($"{p.Label} called CurrentBed and matched {bed.GetUniqueLoadID()}");
                                            __result = bed;
                                            sleepingSlot = sleepingSlotTmp;
                                            return false;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    __result = null;
                    sleepingSlot = null;
                    return false;
                }
            }

            [HarmonyPatchCategory("LoftBedBunkBedsModCompatPatches")]
            [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetCurOccupant))]
            class Patch_Building_Bed_GetCurOccupant {
                static bool Prefix(Building_Bed __instance, ref Pawn __result, int slotIndex) {
                    if (!__instance.Spawned) {
                        __result = null;
                        return false;
                    }

                    bool checkLoftBed = BedOwnershipTools.Singleton.runtimeHandles.modLoftBedLoadedForCompatPatching;
                    bool checkBunkBeds = BedOwnershipTools.Singleton.runtimeHandles.modBunkBedsLoadedForCompatPatching;
                    bool isBunkBed = checkBunkBeds && HarmonyPatches.ModCompatPatches_BunkBeds.RemoteCall_IsBunkBed(__instance);

                    IntVec3 sleepingSlotPos;
                    if (isBunkBed) {
                        sleepingSlotPos = __instance.Position;
                    } else {
                        // Bunk Beds patches this function but not for returning the expected result
                        sleepingSlotPos = __instance.GetSleepingSlotPos(slotIndex);
                    }
                    List<Thing> list = __instance.Map.thingGrid.ThingsListAt(sleepingSlotPos);
                    int bedsCnt = 0;
                    List<Pawn> pawns = new List<Pawn>(4);
                    for (int i = 0; i < list.Count; i++){
                        if (checkLoftBed && list[i] is Building_Bed bed) {
                            bedsCnt++;
                        } else if (list[i] is Pawn pawn && pawn.Spawned && pawn.CurJob != null && pawn.GetPosture().InBed() && pawn.CurJobDef == JobDefOf.LayDown) {
                            pawns.Add(pawn);
                        }
                    }

                    int pawnsCnt = pawns.Count;
                    bool isLoftBed = checkLoftBed && bedsCnt > 1;
                    // pathological case
                    bool isLoftBedXBunkBedsCrossing = isLoftBed && isBunkBed;

                    // vanilla case handling with 0 pawns
                    if (pawnsCnt == 0) {
                        __result = null;
                        return false;
                    }

                    // vanilla case handling with 1 non-bunk bed
                    if (!isLoftBed && !isBunkBed && pawnsCnt == 1) {
                        __result = pawns[0];
                        return false;
                    }

                    // loft/bunk case handling
                    if (isBunkBed && !isLoftBed && slotIndex < pawnsCnt) {
                        Pawn pawn = pawns[slotIndex];
                        if (pawn.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                            if (bed == __instance) {
                                // Log.Message($"{__instance.GetUniqueLoadID()} called GetCurOccupant on slot {slotIndex} and matched {pawn.Label}");
                                __result = pawn;
                                return false;
                            }
                        }
                    } else if (!isBunkBed && isLoftBed) {
                        foreach (Pawn pawn in pawns) {
                            if (pawn.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                                if (bed == __instance) {
                                    // Log.Message($"{__instance.GetUniqueLoadID()} called GetCurOccupant on slot {slotIndex} and matched {pawn.Label}");
                                    __result = pawn;
                                    return false;
                                }
                            }
                        }
                    } else if (isLoftBedXBunkBedsCrossing) {
                        List<Pawn> pawnsInThisBed = pawns.Where(x => x.CurJob.GetTarget(TargetIndex.A).Thing == __instance).ToList();
                        if (slotIndex < pawnsInThisBed.Count) {
                            Pawn pawn = pawnsInThisBed[slotIndex];
                            if (pawn.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                                if (bed == __instance) {
                                    // Log.Message($"{__instance.GetUniqueLoadID()} called GetCurOccupant on slot {slotIndex} and matched {pawn.Label}");
                                    __result = pawn;
                                    return false;
                                }
                            }
                        }
                    }

                    // by elimination the Pawn doesn't have a valid sleeping slot
                    __result = null;
                    return false;
                }
            }
        }
    }
}
