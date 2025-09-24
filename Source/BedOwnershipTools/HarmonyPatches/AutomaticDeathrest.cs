using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Automatic deathrest
// - Notifies a Pawn's automatic deathrest tracker when deathrest has finished (Hediff_Deathrest.PostRemoved)
// - Notifies a Pawn's automatic deathrest tracker if a Pawn loses their deathrest gene (Gene_Deathrest.Reset)
// - Modifies time until the low deathrest alert when an automatic deathrest schedule is chosen

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(Hediff_Deathrest), nameof(Hediff_Deathrest.PostRemoved))]
        public class Patch_Hediff_Deathrest_PostRemoved {
            static void Postfix(Hediff_Deathrest __instance) {
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    pawnXAttrs.automaticDeathrestTracker.Notify_DeathrestEnded();
                }
            }
        }

        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.Reset))]
        public class Patch_Gene_Deathrest_Reset {
            static void Postfix(Gene_Deathrest __instance) {
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    pawnXAttrs.automaticDeathrestTracker.Notify_DeathrestGeneRemoved();
                }
            }
        }

        [HarmonyPatch(typeof(Alert_LowDeathrest), "CalculateTargets")]
        public class Patch_Alert_LowDeathrest_CalculateTargets {
            static float MyNeedWatermark(Pawn pawn) {
                if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                    return 0.1f;
                }
                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    return pawnXAttrs.automaticDeathrestTracker.automaticDeathrestMode.LowDeathrestAlertLevel();
                }
                return 0.1f;
            }

            // // if (item.RaceProps.Humanlike && item.Faction == Faction.OfPlayer && item.needs != null && item.needs.TryGetNeed(out Need_Deathrest need) && need.CurLevel <= 0.1f && !item.Deathresting)
			// IL_005b: ldloc.2
			// IL_005c: callvirt instance float32 RimWorld.Need::get_CurLevel()
			// IL_0061: ldc.r4 0.1 <- replace
			// IL_0066: bgt.un.s IL_009a
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.ReplaceAtMatchingCodeInstructionTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.LoadsConstant(0.1f),
                    new[] {
                        new CodeInstruction(OpCodes.Ldloc_1), // Pawn item
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(
                                typeof(Patch_Alert_LowDeathrest_CalculateTargets),
                                nameof(Patch_Alert_LowDeathrest_CalculateTargets.MyNeedWatermark)
                            )
                        ),
                    },
                    firstMatchOnly: true,
                    errorOnNonMatch: true
                );
            }
        }
    }
}
