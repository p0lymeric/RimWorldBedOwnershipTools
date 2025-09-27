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

        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.TickDeathresting))]
        public class Patch_Gene_Deathrest_TickDeathresting {
            static bool MyAutoWakeHandler(Gene_Deathrest gene_Deathrest) {
                // return true to execute the original code path
                if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                    return true;
                }
                CompPawnXAttrs pawnXAttrs = gene_Deathrest.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs == null) {
                    return true;
                }
                if (pawnXAttrs.automaticDeathrestTracker.automaticDeathrestMode.Discipline() != AutomaticDeathrestScheduleDiscipline.Manual) {
                    Need_Deathrest need_Deathrest = gene_Deathrest.pawn.needs?.TryGetNeed<Need_Deathrest>();
                    if (need_Deathrest != null) {
                        // normally the game will wake a deathrester when it's safe before full recovery, around 80% need recovered
                        // we'll wake deathresters at 100% recovery in automatic modes
                        // calendar deathresters need to recover around 100% to meet the minimum needed to deathrest every 30 days
                        // technically need slightly less, but it's really a matter of wasting up to half a day in rest
                        if (gene_Deathrest.DeathrestPercent >= 1f && need_Deathrest.CurLevel >= 1.0f) {
                            gene_Deathrest.Wake();
                        }
                        return false;
                    }
                }
                return true;
            }
            // // if (DeathrestPercent >= 1f && !notifiedWakeOK)
            // IL_0079: ldarg.0
            // + call MyAutoWakeHandler
            // + brfalse <EXIT POINT>
            // + ldarg.0
            // IL_007a: call instance float32 RimWorld.Gene_Deathrest::get_DeathrestPercent() <- S0 copy until match and insert patch
            // IL_007f: ldc.r4 1 <- S1 match
            // IL_0084: blt.un.s IL_00e6 <- S2 match, label branch target as <EXIT POINT>
            // IL_0086: ldarg.0
            // IL_0087: ldfld bool RimWorld.Gene_Deathrest::notifiedWakeOK
            // IL_008c: brtrue.s IL_00e6
            // ...
            // ~ <EXIT POINT>: [IL_00e6]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                int state = 0;
                CodeInstruction brFalseToExitPoint = new(OpCodes.Brfalse_S);
                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case 0: // S0 copy until match and insert patch
                            if (instruction.Calls(AccessTools.PropertyGetter(typeof(Gene_Deathrest), nameof(Gene_Deathrest.DeathrestPercent)))) {
                                yield return new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_Gene_Deathrest_TickDeathresting),
                                        nameof(Patch_Gene_Deathrest_TickDeathresting.MyAutoWakeHandler)
                                    )
                                );
                                yield return brFalseToExitPoint;
                                yield return new CodeInstruction(OpCodes.Ldarg_0);
                                yield return instruction;
                                state++;
                            } else {
                                yield return instruction;
                            }
                            break;
                        case 1: // S1 match
                            if (instruction.LoadsConstant(1.0f)) {
                                yield return instruction;
                                state++;
                            } else {
                                Log.Error("[BOT] Transpiler could not match ldc.r4 1 after call to DeathrestPercent property getter.");
                                yield break;
                            }
                            break;
                        case 2: // S2 match, label branch target as <EXIT POINT>
                            Label? exitPointNullable = null;
                            if (instruction.Branches(out exitPointNullable) && exitPointNullable.HasValue) {
                                brFalseToExitPoint.operand = exitPointNullable.Value;
                                yield return instruction;
                                state++;
                            } else {
                                Log.Error("[BOT] Transpiler could not match branch after ldc.r4 1.");
                                yield break;
                            }
                            break;
                        case 3: // S3 terminal copy
                            yield return instruction;
                            break;
                        default:
                            Log.Error("[BOT] Transpiler reached illegal state");
                            yield break;
                    }
                }
                if (state != 3) {
                    Log.Error($"[BOT] Transpiler did not reach expected terminal state 3. It only reached state {state}.");
                }
            }
        }
    }

    // this doesn't seem to be hittable for some reason--not sure why autoWake seems to be unaffected even though this line should execute
    // TryStartDeathrest gene_Deathrest.autoWake = reason != DeathrestStartReason.PlayerForced;
}
