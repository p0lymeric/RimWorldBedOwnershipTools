using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Communal beds
// - block pawns from claiming communal beds via ClaimBedIfNotMedical
// - (also one stray overlay update call for the assignment groups feature in ClaimBedIfNotMedical)
// - allow pawns to sleep in the bed via CanUseBedNow (otherwise they lie awake)
// - allow a Pawn to gain thoughts regarding the quality of the bedroom/barracks after sleeping in a communal bed via ApplyBedThoughts

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimBedIfNonMedical))]
        public class Patch_Pawn_Ownership_ClaimBedIfNonMedical {
            static bool Prefix(Pawn_Ownership __instance, ref bool __result, Building_Bed newBed) {
                CompBuilding_BedXAttrs newBedAssignableXAttrs = newBed.GetComp<CompBuilding_BedXAttrs>();
                if (newBedAssignableXAttrs == null) {
                    return true;
                }

                // ClaimBedIfNotMedical handles mutating both Pawn_Ownership and CompAssignableToPawn_Bed so
                // it should be safe to block its effects by not allowing it to execute (won't break sync between owner-bed pairs)

                // Pawn has been consigned to a community bed--don't mark them as owner of the bed
                if (newBedAssignableXAttrs.IsAssignedToCommunity) {
                    __result = false;
                    return false;
                }

                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (enableBedAssignmentGroups) {
                    Pawn pawn = HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_pawn(__instance);
                    CATPBAndPOMethodReplacements.ClaimBedIfNotMedical(pawn, newBed);
                }

                // Cascade into the base implementation if above check fails
                return true;
            }
        }

        // this is a lukewarm function, it's called once per occupied bed per tick
        // sleepers include both humanlike and animal Pawns
        // analysis brought to you by a pen of 224 chickens
        [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedNow))]
        public class Patch_RestUtility_CanUseBedNow {
            static bool CheckXAttrsIsAssignedToCommunity(Building_Bed bedThing) {
                // 0.13 us/call
                CompBuilding_BedXAttrs bedXAttrs = bedThing.GetComp<CompBuilding_BedXAttrs>();
                if (bedXAttrs == null) {
                    return false;
                }
                return bedXAttrs.IsAssignedToCommunity;
            }
            // need to vector to IL_0188
            // bedThing is ARG 0
            // // if (building_Bed.Medical)
            // IL_012a: ldloc.0
            // IL_012b: callvirt instance bool RimWorld.Building_Bed::get_Medical() <- S0 match this insn
            // IL_0130: brfalse.s IL_0149                                           <- S1 call this branch target <TRAP POINT>
            // // if (!allowMedBedEvenIfSetToNoCare && !HealthAIUtility.ShouldEverReceiveMedicalCareFromPlayer(sleeper))
            // ...
            // // return false;
            // ...
            // // if (!HealthAIUtility.ShouldSeekMedicalRest(sleeper))
            // IL_013f: ldarg.1
            // IL_0140: call bool RimWorld.HealthAIUtility::ShouldSeekMedicalRest(class Verse.Pawn) <- S2 match this insn
            // IL_0145: brtrue.s IL_0188                                            <- S3 call this branch target <EXIT POINT>
            // // return false;
            // ...
            // // if (!flag && !BedOwnerWillShare(building_Bed, sleeper, guestStatusOverride))
            // IL_0149: ldloc.1                                                     <- S4 at <TRAP POINT> insert "if (xAttrs.isAssignedToCommunity) goto <EXIT POINT>;"
            // IL_014a: brtrue.s IL_0159                                            <- S5 no actions beyond this point
            // ...
            // // return false;
            // ...
            // // if (flag2 && sleepingSlot != assignedSleepingSlot)
            // ...
            // // return false;
            // ...
            // // if (sleeper.IsColonist && !flag3)
            // IL_0188: ldarg.1
            // IL_0189: callvirt instance bool Verse.Pawn::get_IsColonist()
            // IL_018e: brfalse.s IL_01bb
            // ...
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                int state = 0;
                Label? trapPointLabelNullable = null;
                Label? exitPointLabelNullable = null;
                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case 0: // COPY_UNTIL_MATCH_GET_MEDICAL
                            yield return instruction;
                            if (instruction.Calls(AccessTools.PropertyGetter(typeof(Building_Bed), nameof(Building_Bed.Medical)))) {
                                state++;
                            }
                            break;
                        case 1: // CAPTURE_TRAP_POINT
                            yield return instruction;
                            instruction.Branches(out trapPointLabelNullable);
                            state++;
                            break;
                        case 2: // COPY_UNTIL_MATCH_SHOULDSEEKMEDICALREST
                            yield return instruction;
                            if (instruction.Calls(AccessTools.Method(typeof(HealthAIUtility), nameof(HealthAIUtility.ShouldSeekMedicalRest)))) {
                                state++;
                            }
                            break;
                        case 3: // CAPTURE_EXIT_POINT
                            yield return instruction;
                            instruction.Branches(out exitPointLabelNullable);
                            state++;
                            break;
                        case 4: // COPY_UNTIL_AT_TRAP_POINT_INSERT_GOTO
                            if (trapPointLabelNullable.HasValue && exitPointLabelNullable.HasValue) {
                                if (instruction.labels.Contains(trapPointLabelNullable.Value)) {
                                    yield return new CodeInstruction(
                                        OpCodes.Ldarg_0
                                    ).MoveLabelsFrom(instruction);
                                    yield return new CodeInstruction(
                                        OpCodes.Call,
                                        AccessTools.Method(typeof(Patch_RestUtility_CanUseBedNow),
                                        nameof(Patch_RestUtility_CanUseBedNow.CheckXAttrsIsAssignedToCommunity))
                                    );
                                    yield return new CodeInstruction(
                                        OpCodes.Brtrue_S,
                                        exitPointLabelNullable.Value
                                    );
                                    state++;
                                }
                            }
                            yield return instruction;
                            break;
                        case 5: // COPY_FINAL
                            yield return instruction;
                            break;
                        default:
                            Log.Error("[BOT] Transpiler reached illegal state");
                            yield break;
                    }
                }
                if (state != 5) {
                    Log.Error($"[BOT] Transpiler did not reach expected terminal state 5. It only reached state {state}.");
                }
            }
        }

        [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetBedRestFloatMenuOption))]
        public class Patch_Building_Bed_GetBedRestFloatMenuOption {
            static bool MyMedicalGetterImpl(Building_Bed thiss) {
                bool communalBedsSupportOrderedMedicalSleep = BedOwnershipTools.Singleton.settings.communalBedsSupportOrderedMedicalSleep;
                CompBuilding_BedXAttrs bedXAttrs = thiss.GetComp<CompBuilding_BedXAttrs>();
                if (bedXAttrs == null) {
                    return thiss.Medical;
                } else {
                    return (communalBedsSupportOrderedMedicalSleep && bedXAttrs.IsAssignedToCommunity) || thiss.Medical;
                }
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                bool everMatched = false;
                foreach (CodeInstruction instruction in instructions) {
                    if (!everMatched && instruction.Calls(AccessTools.PropertyGetter(typeof(Building_Bed), nameof(Building_Bed.Medical)))) {
                        yield return new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Building_Bed_GetBedRestFloatMenuOption),
                            nameof(Patch_Building_Bed_GetBedRestFloatMenuOption.MyMedicalGetterImpl))
                        );
                        everMatched = true;
                    } else {
                        yield return instruction;
                    }
                }
                if (!everMatched) {
                    Log.Error("[BOT] Transpiler never found a Building_Bed.Medical getter call");
                }
            }
        }

        [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedThoughts")]
        public class Patch_Toils_LayDown_ApplyBedThoughts {
            static bool BedEqOwnedBedOrBedIsAssignedToCommunity(Building_Bed bed, Building_Bed ownedBed) {
                // null check already performed earlier
                CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                if (bedXAttrs == null) {
                    return bed == ownedBed;
                } else {
                    return (bed == ownedBed) || bedXAttrs.IsAssignedToCommunity;
                }
            }
            // commandeer the bed == actor.ownership.OwnedBed comparison to insert our check
            // if (bed != null && bed == actor.ownership.OwnedBed && !bed.ForPrisoners && !actor.story.traits.HasTrait(TraitDefOf.Ascetic))
            // IL_022b: ldarg.1 // (bed)
            // IL_022c: ldarg.0 (bed actor)
            // IL_022d: ldfld public class RimWorld.Pawn_Ownership Verse.Pawn::ownership // (bed actor.ownership)
            // IL_0232: callvirt instance public class RimWorld.Building_Bed RimWorld.Pawn_Ownership::get_OwnedBed() // (bed actor.ownership.OwnedBed)
            // - IL_0237: bne.un IL_02f7 ()
            // + call BedEqOwnedBedOrBedIsAssignedToCommunity // (testResult)
            // + brfalse IL_02f7 ()
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                bool currentOpReplaceBneUn = false;
                foreach (CodeInstruction instruction in instructions) {
                    if (instruction.Calls(AccessTools.PropertyGetter(typeof(Pawn_Ownership), nameof(Pawn_Ownership.OwnedBed)))) {
                        yield return instruction;
                        yield return new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Toils_LayDown_ApplyBedThoughts),
                            nameof(Patch_Toils_LayDown_ApplyBedThoughts.BedEqOwnedBedOrBedIsAssignedToCommunity))
                        );
                        currentOpReplaceBneUn = true;
                    } else if (currentOpReplaceBneUn) {
                        if (instruction.opcode == OpCodes.Bne_Un) {
                            yield return new CodeInstruction(OpCodes.Brfalse, instruction.operand);
                            currentOpReplaceBneUn = false;
                        } else {
                            Log.Error("[BOT] Transpile failed to locate bne.un after call to get_OwnedBed");
                        }
                    } else {
                        yield return instruction;
                    }
                }
            }
        }

        // TODO
        // implement relationship based search for double bed
        // veto - can't see a way to do this easily without adding to FindBedFor's search loops. too messy
        // implement lingering thought for ThoughtWorker_WantToSleepWithSpouseOrLover, ThoughtWorker_SharedBed for recent slumbers with or without lover
        // implement ThoughtWorker_BedroomJealous ThoughtWorker_BedroomRequirementsNotMet ThoughtWorker_Greedy
        // veto - would either need to use the overlay system to make the pawn treat the communal bed as owned and take the vanilla mood effect
        //        or add some extra tracking logic. maybe later
    }
}
