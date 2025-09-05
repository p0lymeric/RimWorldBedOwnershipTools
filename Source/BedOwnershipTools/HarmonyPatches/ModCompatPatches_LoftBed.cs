using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_LoftBed {
            // Prefix and detour out instead of unpatching
            // (to avoid experimenting with Harmony's unpatching functions and finding out whether they create load-order dependent behaviours during init)
            // It appears this approach is incompatible with Dubs Performance Analyzer's stack trace collection
            // but the issue won't be hit unless someone actively selects one of the functions in the patch chain and clicks the button to enable stack trace collection
            [HarmonyPatchCategory("LoftBedModCompatPatches")]
            [HarmonyPatch("Nekoemi.LoftBed.Patch_CurrentBed", "Postfix")]
            public class DoublePatch_Nekoemi_LoftBed_Patch_CurrentBed {
                static bool Prefix() {
                    return false;
                }
            }
            [HarmonyPatchCategory("LoftBedModCompatPatches")]
            [HarmonyPatch("Nekoemi.LoftBed.Patch_GetCurOccupant", "Postfix")]
            public class DoublePatch_Nekoemi_LoftBed_Patch_GetCurOccupant {
                static bool Prefix() {
                    return false;
                }
            }

            [HarmonyPatchCategory("LoftBedModCompatPatches")]
            [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CurrentBed))]
            [HarmonyPatch(new System.Type[] { typeof(Pawn), typeof(int?) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out })]
            class Patch_RestUtility_CurrentBed {
                static void Postfix(ref Building_Bed __result, Pawn p, ref int? sleepingSlot) {
                    // iterate through the sleeping Pawns and Building_Beds at the given pawn's position
                    // and map sleeping owners to their beds
                    // and sleeping communal users to the bed in their job's TargetA

                    // CurrentBed is called every tick per sleeper in a bed
                    // let's pray we don't trash TPS

                    // short-circuit since the base method would've
                    // called GetCurOccupant on one of the beds at the Pawn's position
                    if (__result != null) {
                        return;
                    }

                    if (!p.Spawned || p.CurJob == null || !p.GetPosture().InBed()) {
                        return;
                    }

                    // maybe the following code could somehow be refactored to call the patched GetCurOccupant too

                    List<Thing> thingList = p.Position.GetThingList(p.Map);
                    List<Building_Bed> beds = new List<Building_Bed>(2);
                    for (int i = 0; i < thingList.Count; i++) {
                        if (thingList[i] is Building_Bed bed) {
                            beds.Add(bed);
                        }
                    }

                    if (beds.Count <= 1) {
                        return;
                    }

                    // assigned owner search
                    for (int num = beds.Count - 1; num >= 0; num--) {
                        List<Pawn> owners = beds[num].OwnersForReading;
                        for (int i = 0; i < owners.Count; i++) {
                            if (owners[i] == p) {
                                int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, beds[num].Position, beds[num].Rotation, beds[num].def.size);
                                if (sleepingSlotTmp >= 0) {
                                    __result = beds[num];
                                    sleepingSlot = sleepingSlotTmp;
                                    return;
                                }
                            }
                        }
                        if (beds[num].OwnersForReading.Count > 0) {
                            beds.RemoveAt(num);
                        }
                    }

                    // communal bed search
                    // beds with owners were filtered above
                    // a bit looser since we don't check isAssignedToCommunity but that should be OK (might fix medical beds as well)
                    if (p.CurJob != null && p.CurJobDef == JobDefOf.LayDown) {
                        if (p.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                            if (beds.Contains(bed)) {
                                // Log.Message($"{p.Label} called CurrentBed and matched {bed.GetUniqueLoadID()}");
                                int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed.Position, bed.Rotation, bed.def.size);
                                if (sleepingSlotTmp >= 0) {
                                    __result = bed;
                                    sleepingSlot = sleepingSlotTmp;
                                    return;
                                }
                            }
                        }
                    }

                    // __result is necessarily null if we fell through here
                }
            }

            [HarmonyPatchCategory("LoftBedModCompatPatches")]
            [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetCurOccupant))]
            class Patch_Building_Bed_GetCurOccupant {
                static void Postfix(Building_Bed __instance, ref Pawn __result, int slotIndex) {
                    // short-circuit since if the base function couldn't identify a Pawn,
                    // we're assured there are zero sleepers on the vertical stack
                    if (__result == null) {
                        return;
                    }

                    IntVec3 sleepingSlotPos = __instance.GetSleepingSlotPos(slotIndex);
                    List<Thing> list = __instance.Map.thingGrid.ThingsListAt(sleepingSlotPos);
                    // declaring initial capacity seems to reduce Add call overhead
                    List<Building_Bed> beds = new List<Building_Bed>(2);
                    List<Pawn> pawns = new List<Pawn>(2);
                    for (int i = 0; i < list.Count; i++){
                        if (list[i] is Building_Bed bed) {
                            beds.Add(bed);
                        } else if (list[i] is Pawn pawn && pawn.Spawned && pawn.CurJob != null && pawn.GetPosture().InBed()) {
                            pawns.Add(pawn);
                        }
                    }

                    // if we are the only bed at specified coords => keep original result
                    if (beds.Count <= 1 || pawns.Count == 0) {
                        return;
                    }

                    // there is at least one other bed and at least one pawn
                    List<Pawn> owners = __instance.OwnersForReading;
                    foreach (Pawn pawn in pawns) {
                        if (owners.Contains(pawn)) {
                            // if pawn is assigned to this bed => return it
                            __result = pawn;
                            return;
                        }
                    }

                    // apply bed list filtering in alignment with CurrentBed
                    // may be unnecessary
                    for (int num = beds.Count - 1; num >= 0; num--) {
                        if (beds[num].OwnersForReading.Count > 0) {
                            beds.RemoveAt(num);
                        }
                    }

                    // communal bed search
                    // beds with owners were filtered above
                    // a bit looser since we don't check isAssignedToCommunity but that should be OK (might fix medical beds as well)
                    foreach (Pawn pawn in pawns) {
                        if (pawn.CurJob != null && pawn.CurJobDef == JobDefOf.LayDown) {
                            if (pawn.CurJob.GetTarget(TargetIndex.A).Thing is Building_Bed bed) {
                                if (beds.Contains(bed) && bed == __instance) {
                                    // we might not catch cases where two Pawns are sleeping in the same slot
                                    // but there seems to be other systems in parallel that mitigate or prevent any issues (ReservationManager?)
                                    // Log.Message($"{__instance.GetUniqueLoadID()} called GetCurOccupant and matched {pawn.Label}");
                                    __result = pawn;
                                    return;
                                }
                            }
                        }
                    }

                    // by elimination the Pawn doesn't have a valid sleeping slot
                    __result = null;
                }
            }
        }
    }
}
