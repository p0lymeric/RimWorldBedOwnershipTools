using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Multiple deathrest casket ownership (assignment groups)
// - similar patches as BedAssignmentGroups.cs, specifically for virtual overrides in CompAssignableToPawn_DeathrestCasket

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(CompAssignableToPawn_DeathrestCasket), nameof(CompAssignableToPawn_DeathrestCasket.TryAssignPawn))]
        public class Patch_CompAssignableToPawn_DeathrestCasket_TryAssignPawn {
            static void Postfix(CompAssignableToPawn_DeathrestCasket __instance, Pawn pawn) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                CATPBAndPOMethodReplacements.TryAssignPawn(__instance, pawn);
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn_DeathrestCasket), nameof(CompAssignableToPawn_DeathrestCasket.TryUnassignPawn))]
        public class Patch_CompAssignableToPawn_DeathrestCasket_TryUnassignPawn {
            static void Prefix(CompAssignableToPawn_DeathrestCasket __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                if (Patch_CompAssignableToPawn_TryUnassignPawn.setBeforeCallingToNotInvalidateAllOverlays) {
                    Patch_Pawn_Ownership_UnclaimDeathrestCasket.HintDontInvalidateOverlays();
                }
                if (Patch_CompAssignableToPawn_TryUnassignPawn.setBeforeCallingToInvalidateAllOverlaysWithoutWarning) {
                    Patch_Pawn_Ownership_UnclaimDeathrestCasket.HintInvalidateAllOverlays();
                }
                Patch_CompAssignableToPawn_TryUnassignPawn.ClearHints();
            }
            static void Postfix(CompAssignableToPawn_DeathrestCasket __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    Patch_Pawn_Ownership_UnclaimDeathrestCasket.ClearHints();
                    return;
                }
                CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, pawn, sort, uninstall);
                Patch_Pawn_Ownership_UnclaimDeathrestCasket.ClearHints();
            }
        }

        // We unfortunately need to patch each user of the unclaim method as a special case.
        [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimDeathrestCasket))]
        public class Patch_Pawn_Ownership_UnclaimDeathrestCasket {
            static bool setBeforeCallingToNotInvalidateAllOverlays = false;
            static bool setBeforeCallingToInvalidateAllOverlaysWithoutWarning = false;

            public static void HintDontInvalidateOverlays() {
                setBeforeCallingToNotInvalidateAllOverlays = true;
            }
            public static void HintInvalidateAllOverlays() {
                setBeforeCallingToInvalidateAllOverlaysWithoutWarning = true;
            }
            public static void ClearHints() {
                setBeforeCallingToNotInvalidateAllOverlays = false;
                setBeforeCallingToInvalidateAllOverlaysWithoutWarning = false;
            }

            static void Prefix(Pawn_Ownership __instance, ref bool __result, out Building_Bed __state) {
                __state = __instance.AssignedDeathrestCasket;
            }

            static void Postfix(Pawn_Ownership __instance, ref bool __result, Building_Bed __state) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    ClearHints();
                    return;
                }
                Pawn pawn = HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_pawn(__instance);
                if (!setBeforeCallingToNotInvalidateAllOverlays) {
                    if (!setBeforeCallingToInvalidateAllOverlaysWithoutWarning) {
                        if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging) {
                            Log.Warning($"[BOT] Pawn_Ownership.UnclaimDeathrestCasket was called, but Bed Ownership Tools doesn't have special handling for the calling case. All of {pawn.Label}'s deathrest caskets have been unassigned, as it is the safest default way to proceed.");
                        }
                    }
                    CATPBAndPOMethodReplacements.UnclaimDeathrestCasketAll(pawn);
                } else if (__result) {
                    // activate another deathrest casket if possible
                    CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                    if (pawnXAttrs != null) {
                        foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                            if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                                if (bed != __state) {
                                    bed.CompAssignableToPawn.ForceAddPawn(pawn);
                                    HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_AssignedDeathrestCasket_Set(pawn.ownership, bed);
                                    break;
                                }
                            }
                        }
                    }
                }
                ClearHints();
            }

            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertCodeInstructionsBeforePredicateTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimDeathrestCasket))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimDeathrestCasket),
                            nameof(Patch_Pawn_Ownership_UnclaimDeathrestCasket.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertCodeInstructionsBeforePredicateTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimDeathrestCasket))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimDeathrestCasket),
                            nameof(Patch_Pawn_Ownership_UnclaimDeathrestCasket.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    true
                );
            }

            // TODO can accomplish this with TargetMethods
            public static void ApplyHarmonyPatches(Harmony harmony) {
                // RimWorld.MoveColonyUtility.MoveColonyAndReset -- invall
                harmony.Patch(AccessTools.Method(typeof(MoveColonyUtility), nameof(MoveColonyUtility.MoveColonyAndReset)), transpiler: new HarmonyMethod(InsertHintInvalidateAllOverlaysTranspiler));

                // RimWorld.Pawn_Ownership.ClaimBedIfNonMedical -- directed, handling in BedAssignmentGroups.cs
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimBedIfNonMedical)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                // RimWorld.Pawn_Ownership.ClaimDeathrestCasket -- directed, handled
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimDeathrestCasket)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                // RimWorld.Pawn_Ownership.UnclaimAll -- invall
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimAll)), transpiler: new HarmonyMethod(InsertHintInvalidateAllOverlaysTranspiler));

                // RimWorld.Pawn_Ownership.Notify_ChangedGuestStatus -- directed, handling in BedAssignmentGroups.cs
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.Notify_ChangedGuestStatus)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                // RimWorld.CompAssignableToPawn_DeathrestCasket.TryUnassignPawn -- directed, HIT
                // handled in Patch_CompAssignableToPawn_TryUnassignPawn
            }
        }
    }
}
