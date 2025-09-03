using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_OneBedToSleepWithAll {
            public static void RemoteCall_IfIsPolygamyThenDefineMaster(Building_Bed bed) {
                ThingComp polyComp = null;
                // wish that compsByType was a public field but linear search is probably good enough
                // it seems ThingWithComps doesn't expose a search method that works with dynamic Types (or I'm blind)
                // ThingComp polyComp = bed.GetComp<BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode>();
                foreach (ThingComp x in bed.AllComps) {
                    if (BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode.IsInstanceOfType(x)) {
                        polyComp = x;
                        break;
                    }
                }
                if (polyComp != null && Traverse.Create(polyComp).Field("isPolygamy").GetValue<bool>()) {
                    Traverse.Create(polyComp).Method("DefineMaster").GetValue();
                }
            }

            public static bool RemoteCall_IsPolygamy(Building_Bed bed) {
                ThingComp polyComp = null;
                foreach (ThingComp x in bed.AllComps) {
                    if (BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode.IsInstanceOfType(x)) {
                        polyComp = x;
                        break;
                    }
                }
                if (polyComp != null) {
                    return Traverse.Create(polyComp).Field("isPolygamy").GetValue<bool>();
                }
                return false;
            }

            public static class Patch_UnclaimBedCalls {
                // TODO can accomplish this with TargetMethods
                public static void ApplyHarmonyPatches(Harmony harmony) {
                    harmony.Patch(
                        AccessTools.Method(BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_PolygamyModeUtility, "AddMakeMasterButton"),
                        transpiler: new HarmonyMethod(Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysTryUnassignPawnUncheckedNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode, "AssignesUpdated"),
                        transpiler: new HarmonyMethod(Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode, "DefineMaster"),
                        transpiler: new HarmonyMethod(Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    // bedXAttrs.assignedPawnsOverlay.Count > bed.SleepingSlotsCount does not seem to be hittable in normal circumstances
                    harmony.Patch(
                        AccessTools.Method(BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode, "UpdateCondition"),
                        transpiler: new HarmonyMethod(Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(BedOwnershipTools.Singleton.runtimeHandles.typeOneBedToSleepWithAll_CompPolygamyMode, "ResetAll"),
                        transpiler: new HarmonyMethod(Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                }
            }

            [HarmonyPatchCategory("OneBedToSleepWithAllModCompatPatches")]
            [HarmonyPatch("OneBedToSleepWithAll.PolygamyModeUtility", "AddMakeMasterButton")]
            public class Patch_PolygamyModeUtility_AddMakeMasterButton {
                static void Postfix(ref bool __result, Rect rect, object pawn_raw, ThingWithComps parent, bool isRectFull) {
                    // the mod returns true if the button was pressed
                    if (__result) {
                        Pawn pawn = ((!(pawn_raw is Pawn)) ? ((Pawn)pawn_raw.GetType().GetField("pawn").GetValue(pawn_raw)) : ((Pawn)pawn_raw));
                        Building_Bed bed = (Building_Bed)parent;
                        CompAssignableToPawn catp = parent.GetComp<CompAssignableToPawn>();
                        CompBuilding_BedXAttrs bedXAttrs = parent.GetComp<CompBuilding_BedXAttrs>();
                        for (int num = bedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                            CATPBAndPOMethodReplacements.TryUnassignPawn(catp, bedXAttrs.assignedPawnsOverlay[num]);
                        }
                        CATPBAndPOMethodReplacements.TryAssignPawn(catp, pawn);
                    }
                }
            }

            [HarmonyPatchCategory("OneBedToSleepWithAllModCompatPatches")]
            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "AssignesUpdated")]
            public class Patch_CompPolygamyMode_AssignesUpdated {
                static void Postfix(Pawn pawn, ThingWithComps ___parent, Pawn ___master) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bed.OwnersForReading.Contains(pawn) && ___master != null && ___master != pawn) {
                        foreach (Pawn owner in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                            if (owner != pawn) {
                                CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                            }
                        }
                    }
                }
            }

            [HarmonyPatchCategory("OneBedToSleepWithAllModCompatPatches")]
            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "DefineMaster")]
            public class Patch_CompPolygamyMode_DefineMaster {
                static void Postfix(ThingWithComps ___parent, Pawn ___master) {
                    if (!(___parent is Building_Bed bed)) {
                        return;
                    }
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                        if (___master != null) {
                            CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                        }
                    }
                }
            }

            // bedXAttrs.assignedPawnsOverlay.Count > bed.SleepingSlotsCount does not seem to be hittable in normal circumstances
            [HarmonyPatchCategory("OneBedToSleepWithAllModCompatPatches")]
            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "UpdateCondition")]
            public class Patch_CompPolygamyMode_UpdateCondition {
                static void Postfix(ThingWithComps ___parent, Pawn ___master, bool ___isPolygamy, Pawn ___currentNeighbor) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (!___isPolygamy) {
                        return;
                    }
                    if (___master == null && bedXAttrs.assignedPawnsOverlay.Count > 1) {
                        return;
                    }
                    if (bedXAttrs.assignedPawnsOverlay.Count > bed.SleepingSlotsCount) {
                        // Log.Warning("Bed " + bed.ToString() + " has more owners than sleeping slots, unassigning incorrect people.");
                        foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay) {
                            if (pawn == ___master) {
                                // Log.Message(pawn.ToString() + " is master, skipping.");
                                continue;
                            }
                            if (pawn == ___currentNeighbor) {
                                // Log.Message(pawn.ToString() + " is current neighbor, skipping.");
                                continue;
                            }
                            // Log.Message("Unassigning " + pawn.ToString());
                            CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                        }
                    }
                }
            }

            [HarmonyPatchCategory("OneBedToSleepWithAllModCompatPatches")]
            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "ResetAll")]
            public class Patch_CompPolygamyMode_ResetAll {
                static void Postfix(ThingWithComps ___parent, Pawn ___master) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                        CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                    }
                }
            }
        }
    }
}
