using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using BedOwnershipTools.Whathecode.System;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_OneBedToSleepWithAll : ModInterop {
        public Assembly assemblyOneBedToSleepWithAll;
        public Type typeOneBedToSleepWithAll_CompPolygamyMode;
        public Type typeOneBedToSleepWithAll_PolygamyModeUtility;

        public ModInterop_OneBedToSleepWithAll(bool enabled) : base(enabled) {
            if (this.enabled) {
                this.assemblyOneBedToSleepWithAll = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assy => assy.GetName().Name == "OneBedToSleepWithAll");
                if (this.assemblyOneBedToSleepWithAll != null) {
                    this.detected = true;
                    this.typeOneBedToSleepWithAll_CompPolygamyMode = assemblyOneBedToSleepWithAll.GetType("OneBedToSleepWithAll.CompPolygamyMode");
                    this.typeOneBedToSleepWithAll_PolygamyModeUtility = assemblyOneBedToSleepWithAll.GetType("OneBedToSleepWithAll.PolygamyModeUtility");
                    this.qualified =
                        this.typeOneBedToSleepWithAll_CompPolygamyMode != null &&
                        this.typeOneBedToSleepWithAll_PolygamyModeUtility != null;
                }
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                ModInteropHarmonyPatches.Patch_UnclaimBedCalls.ApplyHarmonyPatches(this, harmony);
                ModInteropDelegatesAndRefs.Resolve(this);
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
        }

        public void RemoteCall_IfIsPolygamyThenDefineMaster(Building_Bed bed) {
            if (this.active) {
                object polyComp = ModInteropDelegatesAndRefs.ThingWithComps_GetComp_SpecializedCompPolygamyMode(bed);

                if (polyComp != null && ModInteropDelegatesAndRefs.CompPolygamyMode_isPolygamy(polyComp)) {
                    ModInteropDelegatesAndRefs.CompPolygamyMode_DefineMaster(polyComp);
                }
            }
        }

        public bool RemoteCall_IsPolygamy(Building_Bed bed) {
            if (this.active) {
                object polyComp = ModInteropDelegatesAndRefs.ThingWithComps_GetComp_SpecializedCompPolygamyMode(bed);

                if (polyComp != null) {
                    return ModInteropDelegatesAndRefs.CompPolygamyMode_isPolygamy(polyComp);
                }
            }
            return false;
        }

        public static class ModInteropDelegatesAndRefs {
            // CompPolygamyMode.DefineMaster()
            public delegate void MethodDelegate_CompPolygamyMode_DefineMaster(object thiss);
            public static MethodDelegate_CompPolygamyMode_DefineMaster CompPolygamyMode_DefineMaster =
                (object thiss) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            // CompPolygamyMode.isPolygamy
            public static AccessTools.FieldRef<object, bool> CompPolygamyMode_isPolygamy =
                (object thiss) => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // ThingWithComps.GetComp<CompPolygamyMode>()
            public delegate object MethodDelegate_ThingWithComps_GetComp_SpecializedCompPolygamyMode(ThingWithComps thiss);
            public static MethodDelegate_ThingWithComps_GetComp_SpecializedCompPolygamyMode ThingWithComps_GetComp_SpecializedCompPolygamyMode =
                (ThingWithComps thiss) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            public static void Resolve(ModInterop_OneBedToSleepWithAll modInterop) {
                Type typeCompPolygamyMode = modInterop.typeOneBedToSleepWithAll_CompPolygamyMode;

                CompPolygamyMode_DefineMaster =
                    DelegateHelper.CreateOpenInstanceDelegate<MethodDelegate_CompPolygamyMode_DefineMaster>(
                        AccessTools.Method(typeCompPolygamyMode, "DefineMaster"),
                        DelegateHelper.CreateOptions.Downcasting
                    );

                CompPolygamyMode_isPolygamy = AccessTools.FieldRefAccess<bool>(typeCompPolygamyMode, "isPolygamy");

                ThingWithComps_GetComp_SpecializedCompPolygamyMode =
                    AccessTools.MethodDelegate<MethodDelegate_ThingWithComps_GetComp_SpecializedCompPolygamyMode>(
                        AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.GetComp), null, new Type[] { typeCompPolygamyMode })
                    );
            }
        }

        public static class ModInteropHarmonyPatches {
            public static class Patch_UnclaimBedCalls {
                // TODO can accomplish this with TargetMethods
                public static void ApplyHarmonyPatches(ModInterop_OneBedToSleepWithAll modInterop, Harmony harmony) {
                    harmony.Patch(
                        AccessTools.Method(modInterop.typeOneBedToSleepWithAll_PolygamyModeUtility, "AddMakeMasterButton"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Patch_CompAssignableToPawn_TryUnassignPawn.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(modInterop.typeOneBedToSleepWithAll_CompPolygamyMode, "AssignesUpdated"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(modInterop.typeOneBedToSleepWithAll_CompPolygamyMode, "DefineMaster"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    // bedXAttrs.assignedPawnsOverlay.Count > bed.SleepingSlotsCount does not seem to be hittable in normal circumstances
                    harmony.Patch(
                        AccessTools.Method(modInterop.typeOneBedToSleepWithAll_CompPolygamyMode, "UpdateCondition"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method(modInterop.typeOneBedToSleepWithAll_CompPolygamyMode, "ResetAll"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Patch_Pawn_Ownership_UnclaimBed.InsertHintDontInvalidateOverlaysNoErrorTranspiler)
                    );
                }
            }

            [HarmonyPatch("OneBedToSleepWithAll.PolygamyModeUtility", "AddMakeMasterButton")]
            public class Patch_PolygamyModeUtility_AddMakeMasterButton {
                static void Postfix(ref bool __result, Rect rect, object pawn_raw, ThingWithComps parent, bool isRectFull) {
                    // the mod returns true if the button was pressed
                    if (__result) {
                        Pawn pawn = ((!(pawn_raw is Pawn)) ? ((Pawn)pawn_raw.GetType().GetField("pawn").GetValue(pawn_raw)) : ((Pawn)pawn_raw));
                        Building_Bed bed = (Building_Bed)parent;
                        CompAssignableToPawn catp = parent.GetComp<CompAssignableToPawn>();
                        CompBuilding_BedXAttrs bedXAttrs = parent.GetComp<CompBuilding_BedXAttrs>();
                        if (bedXAttrs == null) {
                            return;
                        }
                        for (int num = bedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                            CATPBAndPOMethodReplacements.TryUnassignPawn(catp, bedXAttrs.assignedPawnsOverlay[num]);
                        }
                        CATPBAndPOMethodReplacements.TryAssignPawn(catp, pawn);
                    }
                }
            }

            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "AssignesUpdated")]
            public class Patch_CompPolygamyMode_AssignesUpdated {
                static void Postfix(Pawn pawn, ThingWithComps ___parent, Pawn ___master) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    if (bed.OwnersForReading.Contains(pawn) && ___master != null && ___master != pawn) {
                        foreach (Pawn owner in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                            if (owner != pawn) {
                                CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                            }
                        }
                    }
                }
            }

            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "DefineMaster")]
            public class Patch_CompPolygamyMode_DefineMaster {
                static void Postfix(ThingWithComps ___parent, Pawn ___master) {
                    if (!(___parent is Building_Bed bed)) {
                        return;
                    }
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                        if (___master != null) {
                            CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                        }
                    }
                }
            }

            // bedXAttrs.assignedPawnsOverlay.Count > bed.SleepingSlotsCount does not seem to be hittable in normal circumstances
            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "UpdateCondition")]
            public class Patch_CompPolygamyMode_UpdateCondition {
                static void Postfix(ThingWithComps ___parent, Pawn ___master, bool ___isPolygamy, Pawn ___currentNeighbor) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
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

            [HarmonyPatch("OneBedToSleepWithAll.CompPolygamyMode", "ResetAll")]
            public class Patch_CompPolygamyMode_ResetAll {
                static void Postfix(ThingWithComps ___parent, Pawn ___master) {
                    Building_Bed bed = ___parent as Building_Bed;
                    CompBuilding_BedXAttrs bedXAttrs = ___parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay.ListFullCopy()) {
                        CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                    }
                }
            }
        }
    }
}
