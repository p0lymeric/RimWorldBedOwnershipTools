using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;

// Summary of patches
// Spare deathrest bindings

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.BindTo))]
        public class Patch_CompDeathrestBindable_BindTo {
            static void Postfix(CompDeathrestBindable __instance, Pawn pawn) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return;
                }
                CompDeathrestBindableXAttrs cdbXAttrs = __instance.parent.GetComp<CompDeathrestBindableXAttrs>();
                cdbXAttrs.boundPawnOverlay = pawn;
            }
        }

        // we abuse calling this method to unbind a set of buildings
        // such calls occur independent of Gene_Deathrest.Reset
        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.Notify_DeathrestGeneRemoved))]
        public class Patch_CompDeathrestBindable_Notify_DeathrestGeneRemoved {
            static bool setBeforeCallingToNotClearOverlayBindee = false;

            public static void HintDontClearOverlayBindee() {
                setBeforeCallingToNotClearOverlayBindee = true;
            }
            public static void ClearHints() {
                setBeforeCallingToNotClearOverlayBindee = false;
            }
            static void Postfix(CompDeathrestBindable __instance) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    ClearHints();
                    return;
                }
                if (!setBeforeCallingToNotClearOverlayBindee) {
                    CompDeathrestBindableXAttrs cdbXAttrs = __instance.parent.GetComp<CompDeathrestBindableXAttrs>();
                    cdbXAttrs.boundPawnOverlay = null;
                }
                ClearHints();
            }
        }

        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.BindTo))]
        public class Patch_Gene_Deathrest_BindTo {
            static bool Prefix(Gene_Deathrest __instance, CompDeathrestBindable bindComp, List<Thing> ___boundBuildings, ref List<CompDeathrestBindable> ___cachedBoundComps) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return true;
                }
                CompDeathrestBindableXAttrs cdbXAttrs = bindComp.parent.GetComp<CompDeathrestBindableXAttrs>();
                cdbXAttrs?.UnbindAsSpare();
                bindComp.BindTo(__instance.pawn);
                ___boundBuildings.Add(bindComp.parent);
                ___cachedBoundComps = null;
                bool shouldSendNotification =
                    BedOwnershipTools.Singleton.settings.deathrestBindingsArePermanent && cdbXAttrs != null && cdbXAttrs.boundPawnOverlay != __instance.pawn &&
                    PawnUtility.ShouldSendNotificationAbout(__instance.pawn) && bindComp.Props.countsTowardsBuildingLimit;
                ;
                if (shouldSendNotification) {
                    Messages.Message("MessageDeathrestBuildingBound".Translate(bindComp.parent.Named("BUILDING"), __instance.pawn.Named("PAWN"), __instance.CurrentCapacity.Named("CUR"), __instance.DeathrestCapacity.Named("MAX")), new LookTargets(bindComp.parent, __instance.pawn), MessageTypeDefOf.NeutralEvent, historical: false);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.TryLinkToNearbyDeathrestBuildings))]
        public class Patch_Gene_Deathrest_TryLinkToNearbyDeathrestBuildings {
            static bool Prefix(Gene_Deathrest __instance, ref List<CompDeathrestBindable> ___cachedBoundComps) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return true;
                }
                if (!ModsConfig.BiotechActive || !__instance.pawn.Spawned) {
                    return false;
                }
                ___cachedBoundComps = null;
                Room room = __instance.pawn.GetRoom();
                if (room == null) {
                    return false;
                }
                List<CompDeathrestBindable> myBoundBuildings = new();
                List<CompDeathrestBindable> othersUnusedBoundBuildings = new();
                foreach (Region region in room.Regions) {
                    foreach (Thing item in region.ListerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)) {
                        CompDeathrestBindableXAttrs cdbXAttrs = item.TryGetComp<CompDeathrestBindableXAttrs>();
                        if (cdbXAttrs != null) {
                            CompDeathrestBindable bindComp = cdbXAttrs.sibling;
                            if (bindComp.BoundPawn == __instance.pawn || cdbXAttrs.boundPawnOverlay == __instance.pawn) {
                                if (__instance.CanBindToBindable(bindComp)) {
                                    myBoundBuildings.Add(bindComp);
                                }
                            } else {
                                if (__instance.CanBindToBindable(bindComp)) {
                                    othersUnusedBoundBuildings.Add(bindComp);
                                }
                            }
                        }
                    }
                }
                foreach (CompDeathrestBindable bindComp in myBoundBuildings) {
                    if (__instance.CanBindToBindable(bindComp)) {
                        __instance.BindTo(bindComp);
                        // Log.Message($"(mine) {__instance.pawn.LabelShort} will bind {bindComp.parent.GetUniqueLoadID()}");
                    } else {
                        // Log.Message($"(mine) {__instance.pawn.LabelShort} won't bind {bindComp.parent.GetUniqueLoadID()}");
                    }
                }
                foreach (CompDeathrestBindable bindComp in othersUnusedBoundBuildings) {
                    if (__instance.CanBindToBindable(bindComp)) {
                        __instance.BindTo(bindComp);
                        // Log.Message($"(theirs) {__instance.pawn.LabelShort} will bind {bindComp.parent.GetUniqueLoadID()}");
                    } else {
                        // Log.Message($"(theirs) {__instance.pawn.LabelShort} won't bind {bindComp.parent.GetUniqueLoadID()}");
                    }
                }
                return false;
            }
        }

#if RIMWORLD__1_6
        [HarmonyPatch(typeof(FloatMenuOptionProvider_Deathrest), "GetSingleOptionFor")]
        public class Patch_FloatMenuOptionProvider_Deathrest_GetSingleOptionFor {
            static void Postfix(FloatMenuOptionProvider_Deathrest __instance, ref FloatMenuOption __result, Thing clickedThing, FloatMenuContext context) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return;
                }
                if (__result?.action != null) {
                    return;
                }
                Pawn pawn = context.FirstSelectedPawn;
                Building_Bed bed = clickedThing as Building_Bed;
                if (bed == null || !bed.def.building.bed_humanlike) {
                    return;
                }
                Gene_Deathrest gene_Deathrest = context.FirstSelectedPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                CompDeathrestBindableXAttrs cdbXAttrs = bed.GetComp<CompDeathrestBindableXAttrs>();
                // Check the assignment overlay (internal assignment is separately checked by caller)
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                    if ((!CATPBAndPOMethodReplacements.HasFreeSlot(bed) || !CATPBAndPOMethodReplacements.BedOwnerWillShare(bed, pawn, pawn.guest.GuestStatus)) && !CATPBAndPOMethodReplacements.IsOwner(bed, pawn)) {
                        __result = new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "AssignedToOtherPawn".Translate(bed).CapitalizeFirst(), null);
                        return;
                    }
                }
                // Check reservations (game will silently block if reservation fails, make it explicit)
                if (!pawn.HasReserved(bed) && !pawn.CanReserve(bed, bed.SleepingSlotsCount, 0, null, ignoreOtherReservations: false)) {
                    Pawn conflictPawn = pawn.Map.reservationManager.FirstRespectedReserver(bed, pawn, null);
                    conflictPawn ??= pawn.Map.physicalInteractionReservationManager.FirstReserverOf(bed);
                    __result = new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + (bed.AnyUnoccupiedSleepingSlot ? "ReservedBy" : "SomeoneElseSleeping").Translate(conflictPawn.LabelShort, conflictPawn).CapitalizeFirst(), null);
                    return;
                }
            }
        }
#else
        [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
        public class Patch_FloatMenuMakerMap_AddHumanlikeOrders {
            static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts) {
                IntVec3 clickCell = IntVec3.FromVector3(clickPos);
                if (ModsConfig.BiotechActive && pawn.CanDeathrest()) {
                    // should technically look inside the closure of the FloatMenuOption to match its environment's value of "bed"
                    // should also account for stacked buildings
                    // let's not consider those
                	List<Thing> thingList2 = clickCell.GetThingList(pawn.Map);
                	for (int num = 0; num < thingList2.Count; num++) {
                		Building_Bed bed;
                		if ((bed = thingList2[num] as Building_Bed) == null || !bed.def.building.bed_humanlike) {
                            continue;
                		}
                        Gene_Deathrest gene_Deathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                        CompDeathrestBindableXAttrs cdbXAttrs = bed.GetComp<CompDeathrestBindableXAttrs>();
                        int indexOfStartDeathrest = opts.FindIndex(x => x.Label == "StartDeathrest".Translate());
                        if (indexOfStartDeathrest >= 0) {
                            // Check the assignment overlay (internal assignment is separately checked by caller)
                            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                                if ((!CATPBAndPOMethodReplacements.HasFreeSlot(bed) || !CATPBAndPOMethodReplacements.BedOwnerWillShare(bed, pawn, pawn.guest.GuestStatus)) && !CATPBAndPOMethodReplacements.IsOwner(bed, pawn)) {
                                    opts[indexOfStartDeathrest] = new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "AssignedToOtherPawn".Translate(bed).CapitalizeFirst(), null);
                                }
                            }
                            // Check reservations (game will silently block if reservation fails, make it explicit)
                            // FIXME doesn't seem to work in 1.5
                            if (!pawn.HasReserved(bed) && !pawn.CanReserve(bed, bed.SleepingSlotsCount, 0, null, ignoreOtherReservations: false)) {
                                Pawn conflictPawn = pawn.Map.reservationManager.FirstRespectedReserver(bed, pawn, null);
                                conflictPawn ??= pawn.Map.physicalInteractionReservationManager.FirstReserverOf(bed);
                                opts[indexOfStartDeathrest] = new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + (bed.AnyUnoccupiedSleepingSlot ? "ReservedBy" : "SomeoneElseSleeping").Translate(conflictPawn.LabelShort, conflictPawn).CapitalizeFirst(), null);
                            }
                        }
                    }
                }
            }
        }
#endif

        [HarmonyPatch(typeof(JobDriver_Deathrest), nameof(JobDriver_Deathrest.TryMakePreToilReservations))]
        public class Patch_JobDriver_Deathrest_TryMakePreToilReservations {
            static void Postfix(JobDriver_Deathrest __instance, ref bool __result, bool errorOnFailed) {
                if (__result) {
                    Building_Bed bed = __instance.job.GetTarget(TargetIndex.A).Thing as Building_Bed;
                    Gene_Deathrest gene_Deathrest = __instance.pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                    if (gene_Deathrest != null) {
                        if (!BedOwnershipTools.Singleton.settings.deathrestBindingsArePermanent) {
                            if (bed != null) {
                                if (CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                                    bed.GetComp<CompDeathrestBindableXAttrs>()?.UnbindPermanent();
                                }
                            }
                        }
                        // BoundComps doesn't appear to populate as expected until the pawn deathrests for the first time after a save reload
                        for (int num = gene_Deathrest.BoundBuildings.Count - 1; num >= 0; num--) {
                            Thing boundBuilding = gene_Deathrest.BoundBuildings[num];
                            boundBuilding.TryGetComp<CompDeathrestBindableXAttrs>()?.UnbindAsSpare();
                        }

                        // if building is unbound and pawn has binds, warn before issuing job

                        // don't actually bind to more than bindComp.Props.stackLimit buildings
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.CanBindTo))]
        public class Patch_CompDeathrestBindable_CanBindTo {
            static bool MyBoundPawnCheck(CompDeathrestBindable thiss, Pawn pawn) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    // TODO should make a clean bypass path in the transpiled code, not by repeating checks in helper function
                    if (thiss.BoundPawn != null) {
                        return thiss.BoundPawn == pawn;
                    }
                } else {
                    if (!BedOwnershipTools.Singleton.settings.deathrestBindingsArePermanent) {
                        // return true;
                        return thiss.presenceTicks == 0;
                    }
                    if (thiss.BoundPawn != null) {
                        return thiss.BoundPawn == pawn;
                    }
                    CompDeathrestBindableXAttrs cdbXAttrs = thiss.parent.GetComp<CompDeathrestBindableXAttrs>();
                    if (cdbXAttrs != null && cdbXAttrs.boundPawnOverlay != null) {
                        return cdbXAttrs.boundPawnOverlay == pawn;
                    }
                }
                return true;
            }
            // // if (boundPawn != null)
            // IL_001d: ldarg.0
            // IL_001e: ldfld class Verse.Pawn RimWorld.CompDeathrestBindable::boundPawn <- S0 match
            // IL_0023: brfalse.s IL_002f                                                <- S1 branch target is <EXIT POINT>. Replace with pop and jump to <EXIT POINT>
            // // return boundPawn == pawn;
            // ...
            // // return true;
            // IL_002f: ldc.i4.1                                                         <- S2 at <EXIT POINT>, prepend call to MyBoundPawnCheck and if false then return false
            // IL_0030: ret
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                int state = 0;
                Label? exitPointLabelNullable = null;
                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case 0: // S0 match
                            yield return instruction;
                            if (instruction.LoadsField(AccessTools.Field(typeof(CompDeathrestBindable), "boundPawn"))) {
                                state++;
                            }
                            break;
                        case 1: // S1 branch target is <EXIT POINT>. Replace with pop and jump to <EXIT POINT>
                            if (!instruction.Branches(out exitPointLabelNullable)) {
                                Log.Error("[BOT] Transpiler expected a branch but matched some other instruction.");
                                yield break;
                            }
                            yield return new CodeInstruction(OpCodes.Pop);
                            yield return new CodeInstruction(OpCodes.Br, exitPointLabelNullable.Value);
                            state++;
                            break;
                        case 2: // S2 at <EXIT POINT>, prepend call to MyBoundPawnCheck and if false then return false
                            if (exitPointLabelNullable.HasValue) {
                                if (instruction.labels.Contains(exitPointLabelNullable.Value)) {
                                    Label movedExitPoint = generator.DefineLabel();
                                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                                    yield return new CodeInstruction(
                                        OpCodes.Call,
                                        AccessTools.Method(
                                            typeof(Patch_CompDeathrestBindable_CanBindTo),
                                            nameof(Patch_CompDeathrestBindable_CanBindTo.MyBoundPawnCheck)
                                        )
                                    );
                                    yield return new CodeInstruction(OpCodes.Brtrue_S, movedExitPoint);
                                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                                    yield return new CodeInstruction(OpCodes.Ret);
                                    instruction.WithLabels(movedExitPoint);
                                    state++;
                                }
                            }
                            yield return instruction;
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

        [HarmonyPatch(typeof(CompAssignableToPawn_DeathrestCasket), nameof(CompAssignableToPawn_DeathrestCasket.CanAssignTo))]
        public class Patch_CompAssignableToPawn_DeathrestCasket_CanAssignTo {
            static AcceptanceReport? MyBoundPawnAcceptanceReport(CompAssignableToPawn_DeathrestCasket thiss, Pawn pawn) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    // TODO should make a clean bypass path in the transpiled code, not by repeating checks in helper function
                    CompDeathrestBindable compDeathrestBindable = thiss.parent.TryGetComp<CompDeathrestBindable>();
                    if (compDeathrestBindable != null && compDeathrestBindable.BoundPawn != null && compDeathrestBindable.BoundPawn != pawn) {
                        return "CannotAssignAlreadyBound".Translate(compDeathrestBindable.BoundPawn);
                    }
                    Gene_Deathrest gene_Deathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                    if (compDeathrestBindable != null && gene_Deathrest.BindingWillExceedStackLimit(compDeathrestBindable)) {
                        return "CannotAssignBedCannotBindToMoreBuildings".Translate(NamedArgumentUtility.Named(thiss.parent.def, "BUILDING"));
                    }
                } else {
                    CompDeathrestBindableXAttrs cdbXAttrs = thiss.parent.GetComp<CompDeathrestBindableXAttrs>();
                    // Check the binding overlay
                    if (BedOwnershipTools.Singleton.settings.deathrestBindingsArePermanent) {
                        if (cdbXAttrs != null && cdbXAttrs.sibling.BoundPawn != null && cdbXAttrs.sibling.BoundPawn != pawn) {
                            return "CannotAssignAlreadyBound".Translate(cdbXAttrs.sibling.BoundPawn);
                        }
                        if (cdbXAttrs != null && cdbXAttrs.boundPawnOverlay != null && cdbXAttrs.boundPawnOverlay != pawn) {
                            return "CannotAssignAlreadyBound".Translate(cdbXAttrs.boundPawnOverlay);
                        }
                    }
                    // Don't check stack limit for deathrest caskets
                    Gene_Deathrest gene_Deathrest = pawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                    if (cdbXAttrs != null && !CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(thiss.parent.def) && gene_Deathrest.BindingWillExceedStackLimit(cdbXAttrs.sibling)) {
                        return "CannotAssignBedCannotBindToMoreBuildings".Translate(NamedArgumentUtility.Named(thiss.parent.def, "BUILDING"));
                    }
                    // Do not place these two checks here. They must be placed where float options are generated.
                    // Check the assignment overlay (internal assignment is separately checked by caller)
                    // Check reservations (game will silently block if reservation fails, make it explicit)
                }
                return null;
            }
            // // CompDeathrestBindable compDeathrestBindable = parent.TryGetComp<CompDeathrestBindable>();
            // IL_0074: ldarg.0
            // IL_0075: ldfld class Verse.ThingWithComps Verse.ThingComp::parent
            // IL_007a: call !!0 Verse.ThingCompUtility::TryGetComp<class RimWorld.CompDeathrestBindable>(class Verse.Thing) <- S0 replace with with pop, push null
            // IL_007f: stloc.1
            // ...
            // // return AcceptanceReport.WasAccepted;
            // IL_0106: call valuetype Verse.AcceptanceReport Verse.AcceptanceReport::get_WasAccepted() <- S1 insert custom acceptance checks
            // IL_010b: ret
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                // strategy: abuse the short circuiting null checks on compDeathrestBindable and set local 1 to null
                // insert extra checks before returning AcceptanceReport.WasAccepted
                // ordering shouldn't really matter here (CannotAssignBedCannotDeathrest will now show with higher priority over CannotAssignAlreadyBound)
                int state = 0;
                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case 0: // S0 replace with with pop, push null
                            if (instruction.Calls(AccessTools.Method(
                                typeof(ThingCompUtility),
                                nameof(ThingCompUtility.TryGetComp),
                                parameters: new[] { typeof(Thing) },
                                generics: new[] { typeof(CompDeathrestBindable) }
                            ))) {
                                yield return new CodeInstruction(OpCodes.Pop);
                                yield return new CodeInstruction(OpCodes.Ldnull);
                                state++;
                            } else {
                                yield return instruction;
                            }
                            break;
                        case 1: // S1 insert custom acceptance checks
                            if (instruction.Calls(AccessTools.PropertyGetter(typeof(AcceptanceReport), nameof(AcceptanceReport.WasAccepted)))) {
                                Label movedExitPoint = generator.DefineLabel();
                                LocalBuilder myBoundPawnAcceptanceReportLocal = generator.DeclareLocal(typeof(Nullable<AcceptanceReport>));
                                yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
                                yield return new CodeInstruction(OpCodes.Ldarg_1);
                                yield return new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_CompAssignableToPawn_DeathrestCasket_CanAssignTo),
                                        nameof(Patch_CompAssignableToPawn_DeathrestCasket_CanAssignTo.MyBoundPawnAcceptanceReport)
                                    )
                                );
                                // need to obtain a reference to the returned value
                                // idiomatic way of doing it is by storing a local and then calling ldloca
                                yield return new CodeInstruction(OpCodes.Stloc, myBoundPawnAcceptanceReportLocal);
                                yield return new CodeInstruction(OpCodes.Ldloca, myBoundPawnAcceptanceReportLocal);
                                yield return new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.PropertyGetter(
                                        typeof(Nullable<AcceptanceReport>),
                                        nameof(Nullable<AcceptanceReport>.HasValue)
                                    )
                                );
                                yield return new CodeInstruction(OpCodes.Brfalse, movedExitPoint);
                                yield return new CodeInstruction(OpCodes.Ldloca, myBoundPawnAcceptanceReportLocal);
                                yield return new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.PropertyGetter(
                                        typeof(Nullable<AcceptanceReport>),
                                        nameof(Nullable<AcceptanceReport>.Value)
                                    )
                                );
                                yield return new CodeInstruction(OpCodes.Ret);
                                instruction.WithLabels(movedExitPoint);
                                state++;
                            }
                            yield return instruction;
                            break;
                        case 2: // S2 terminal copy
                            yield return instruction;
                            break;
                        default:
                            Log.Error("[BOT] Transpiler reached illegal state");
                            yield break;
                    }
                }
                if (state != 2) {
                    Log.Error($"[BOT] Transpiler did not reach expected terminal state 2. It only reached state {state}.");
                }
            }
        }

        // TODO patch FloatMenuOptionProvider_Deathrest.GetSingleOptionFor/FloatMenuMakerMap.AddHumanlikeOrders
        // to add confirmation for overbinding
    }
}
