using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        // FindBedFor
        // if "sleeper is a mechanoid":
        //     return null;
        // if "sleeper is deathresting and sleeper has a valid deathrest casket that is bound to sleeper":
        //     return assignedDeathrestCasket;
        // medicalBedDefList := "the sleeper must sleep in a slab bed" ? bedsBestToWorst_SlabBed_Medical : bedsBestToWorst_Medical
        // bedDefList := "the sleeper must sleep in a slab bed" ? bedsBestToWorst_SlabBed : bedsBestToWorst
        // if "sleeper should seek medical rest":
        //     if "sleeper is in a medical bed and the bed is valid for the sleeper":
        //         return sleeper.CurrentBed()
        //     if exists x st. "search for a medical bed, first prioritizing order of medicalBedDefList, then 'Danger.None' paths over 'Danger.Deadly' paths, then shortest distance":
        //         return x
        // if "sleeper is a dryad":
        //     return null;
        // if "sleeper owns a bed and can reach the bed over a 'Danger.Some' path":
        //     return ownedBed
        // <- INSERTION POINT 1: if exists partner st. "if relationship-aware bed search is enabled, search for a sleeping partner by opinion, where that partner and their current bed can accept the sleeper and the sleeper can reach the bed over a 'Danger.Some' path":
        //                           return partner.CurrentBed()
        // if "sleeper has a most liked partner and that partner and their owned bed can accept the sleeper and the sleeper can reach the bed over a 'Danger.Some' path":
        //     return mostLikedPartner.ownedBed
        // v EDIT POINT 1 append check "and is not communal if relationship-aware bed search is enabled"
        // if exists x st. "search for a bed, first prioritizing order of bedDefList, then 'Danger.None' paths over 'Danger.Deadly' paths, then shortest distance"
        //     // NB the game will check Danger.None twice probably due to some uncleaned cruft
        //     // NB the game can return a deathrest casket in this stage, but we don't want to sleep in it if a communal bed is available
        //    v EDIT POINT 2 if the bed is a deathrest casket belonging to me, save me in LOCAL reorderedFromPrevStage and jump to INSERTION POINT 2
        //     return x
        // <- INSERTION POINT 2: if exists x st. "if relationship-aware bed search is enabled, search for a communal bed, preferring single if the pawn doesn't have a partner, then prioritizing order of bedDefList, then 'Danger.None' paths over 'Danger.Deadly' paths, then shortest distance"
        //                           return x
        //                       if reorderedFromPrevStage != null
        //                           return reorderedFromPrevStage
        // return null

        [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor))]
        [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
        public class Patch_RelationshipAwareSearch_RestUtility_FindBedFor {
            public static bool MyBypassIfTrueCondition() {
                return !BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware;
            }
            public static bool MyApplySimpleSearchIfTrueCondition(Pawn sleeper) {
                // cannot be rolled with MyBypassIfTrueCondition since we also need an equivalent edit in
                // "Patch_RelationshipAwareSearch_RestUtility_FindBedFor_buildingBed2_SearchPredicate"
                // where it is convoluted to reference sleeper from that closure's environment

                // fast checks to disqualify some pawns from performing communal bed searches
                // to mitigate 10 PM TPS spikes with large barns or large guest/enemy mobs
                if (!sleeper.RaceProps.Humanlike) {
                    return true;
                }
                if (sleeper.Faction != Faction.OfPlayer) {
                    return true;
                }
                return false;
            }
            public static bool IsDeathrestCasketAndDefer(Building_Bed bed) {
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return false;
                }
                return CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def);
            }
            public static Building_Bed MyLovePartnersCurrentOrJobBed(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus) {
                if (MyApplySimpleSearchIfTrueCondition(sleeper)) {
                    return null;
                }
                IEnumerable<DirectPawnRelation> directPawnRelations =
                    LovePartnerRelationUtility
                    .ExistingLovePartners(sleeper, allowDead: false)
                    .OrderByDescending(x => sleeper.relations.OpinionOf(x.otherPawn));
                foreach (DirectPawnRelation directPawnRelation in directPawnRelations) {
                    // Log.Message($"{sleeper.LabelShort} and {directPawnRelation.otherPawn.LabelShort}, opinion {sleeper.relations.OpinionOf(directPawnRelation.otherPawn)}");
                    if (directPawnRelation != null) {
                        Building_Bed currentBed = directPawnRelation.otherPawn.CurrentBed();
                        // only take job bed from vanilla game jobs (mostly interested in LayDown when a pawn has committed to walking towards a bed)
                        currentBed ??= GameComponent_AssignmentGroupManager.Singleton.agmCompartment_JobDriverTargetBedLUT.GetJobTargetedBedFromPawn(directPawnRelation.otherPawn, useDynamicLookup: false);
                        if (currentBed != null && RestUtility.IsValidBedFor(currentBed, sleeper, traveler, checkSocialProperness, allowMedBedEvenIfSetToNoCare: false, ignoreOtherReservations, guestStatus)) {
                            return currentBed;
                        }
                    }
                }
                return null;
            }
            public static Building_Bed MyClosestFreeCommunalBedParamSleepingSlots(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, List<ThingDef> bedDefsBestToWorst, bool ignoreSleepingSlots, bool sleepingSlotsOneFalseManyTrue, bool disallowPartiallyOccupied) {
                for (int dg = 1; dg < 3; dg++) {
                    Danger maxDanger = (dg <= 1) ? Danger.None : Danger.Deadly;
                    for (int num = 0; num < bedDefsBestToWorst.Count; num++) {
                        ThingDef thingDef2 = bedDefsBestToWorst[num];
                        if (!RestUtility.CanUseBedEver(sleeper, thingDef2)) {
                            continue;
                        }
                        Building_Bed building_Bed2 = (Building_Bed)GenClosest.ClosestThingReachable(
                            sleeper.PositionHeld,
                            sleeper.MapHeld,
                            ThingRequest.ForDef(thingDef2),
                            PathEndMode.OnCell,
                            TraverseParms.For(traveler),
                            9999f,
                            (Thing b) => !((Building_Bed)b).Medical &&
                                         ((Building_Bed)b).GetComp<CompBuilding_BedXAttrs>() is CompBuilding_BedXAttrs bedXAttrs && bedXAttrs.IsAssignedToCommunity &&
                                         (int)b.Position.GetDangerFor(sleeper, sleeper.MapHeld) <= (int)maxDanger && RestUtility.IsValidBedFor(b, sleeper, traveler, checkSocialProperness, allowMedBedEvenIfSetToNoCare: false, ignoreOtherReservations, guestStatus) &&
                                         (dg > 0 || !b.Position.GetItems(b.Map).Any((Thing thing) => thing.def.IsCorpse)) &&
                                         (ignoreSleepingSlots || (sleepingSlotsOneFalseManyTrue ? (((Building_Bed)b).SleepingSlotsCount > 1 && !BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.RemoteCall_IsBunkBed((Building_Bed)b)) : ((Building_Bed)b).SleepingSlotsCount == 1) || BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.RemoteCall_IsBunkBed((Building_Bed)b)) &&
                                         // reservation checks are more eager than occupation checks
                                         (!disallowPartiallyOccupied /*|| !((Building_Bed)b).AnyOccupants*/ || sleeper.HasReserved((Building_Bed)b) || traveler.CanReserve((Building_Bed)b, 1, -1, null, ignoreOtherReservations))
                        );
                        if (building_Bed2 != null) {
                            return building_Bed2;
                        }
                    }
                }
                return null;
            }
            public static Building_Bed MyClosestFreeCommunalBed(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, List<ThingDef> bedDefsBestToWorst, Building_Bed reorderedFromPrevStage) {
                if (MyApplySimpleSearchIfTrueCondition(sleeper)) {
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: true, sleepingSlotsOneFalseManyTrue: false, disallowPartiallyOccupied: false
                    ) is Building_Bed any) {
                        return any;
                    }
                    return reorderedFromPrevStage;
                }
                // dear god the total unrolled number of calls to ClosestThingReachable...
                if (!PawnXAttrs_RelationshipAwareTracker.WouldWantToSleepWithSpouseOrLover(sleeper)) {
                    // single bed
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: false, sleepingSlotsOneFalseManyTrue: false, disallowPartiallyOccupied: false
                    ) is Building_Bed single) {
                        return single;
                    }
                    // double bed unoccupied
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: false, sleepingSlotsOneFalseManyTrue: true, disallowPartiallyOccupied: true
                    ) is Building_Bed doubleunocc) {
                        return doubleunocc;
                    }
                    // fallback (any, but probably would hit double bed occupied)
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: true, sleepingSlotsOneFalseManyTrue: false, disallowPartiallyOccupied: false
                    ) is Building_Bed doubleocc) {
                        return doubleocc;
                    }
                } else {
                    // double bed unoccupied
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: false, sleepingSlotsOneFalseManyTrue: true, disallowPartiallyOccupied: true
                    ) is Building_Bed doubleunocc) {
                        return doubleunocc;
                    }
                    // single bed
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: false, sleepingSlotsOneFalseManyTrue: false, disallowPartiallyOccupied: false
                    ) is Building_Bed single) {
                        return single;
                    }
                    // fallback (any, but probably would hit double bed occupied)
                    if (MyClosestFreeCommunalBedParamSleepingSlots(
                        sleeper, traveler, checkSocialProperness, ignoreOtherReservations, guestStatus, bedDefsBestToWorst,
                        ignoreSleepingSlots: true, sleepingSlotsOneFalseManyTrue: false, disallowPartiallyOccupied: false
                    ) is Building_Bed doubleocc) {
                        return doubleocc;
                    }
                }
                return reorderedFromPrevStage;
            }
            // // return sleeper.ownership.OwnedBed;
            // ...
            // IL_02e1: callvirt instance class RimWorld.Building_Bed RimWorld.Pawn_Ownership::get_OwnedBed() // <- S0 find
            // IL_02e6: ret
            // // return sleeper.ownership.OwnedBed;
            // IL_02d6: ldloc.0 // <- InsertMyLovePartnersCurrentOrJobBed
            // ...
            // // return building_Bed2;
            // IL_0443: ldloc.s 19
            // IL_0445: ret <- InsertDeferralForFoundDeathrestCasket
            // ...
            // return null;
            // IL_0476: ldnull
            // IL_0477: ret <- InsertMyClosestFreeCommunalBedCall
            enum TState {
                InsertMyLovePartnersCurrentOrJobBed,
                InsertDeferralForFoundDeathrestCasket,
                InsertMyClosestFreeCommunalBedCall,
                Terminal
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                TState state = TState.InsertMyLovePartnersCurrentOrJobBed;

                int maxLookbehindWindow = 3;
                List<CodeInstruction> editBuffer = new(maxLookbehindWindow);

                LocalBuilder myBypassIfTrueConditionLocal = generator.DeclareLocal(typeof(bool));
                LocalBuilder reorderedFromPrevStage = generator.DeclareLocal(typeof(Building_Bed));
                Label myClosestFreeCommunalBedStageLabel = generator.DefineLabel();

                editBuffer.Add(new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(
                        typeof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor),
                        nameof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor.MyBypassIfTrueCondition)
                    )
                ));
                editBuffer.Add(new CodeInstruction(OpCodes.Stloc, myBypassIfTrueConditionLocal));

                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case TState.InsertMyLovePartnersCurrentOrJobBed:
                            if (
                                instruction.opcode == OpCodes.Ldloc_0 &&
                                editBuffer.Count >= 2 &&
                                editBuffer[editBuffer.Count - 1].opcode == OpCodes.Ret &&
                                editBuffer[editBuffer.Count - 2].Calls(AccessTools.PropertyGetter(typeof(Pawn_Ownership), nameof(Pawn_Ownership.OwnedBed)))
                            ) {
                                LocalBuilder foundBed = generator.DeclareLocal(typeof(Building_Bed));
                                Label bypassPoint = generator.DefineLabel();

                                editBuffer.Add(new CodeInstruction(OpCodes.Ldloc, myBypassIfTrueConditionLocal).MoveLabelsFrom(instruction));
                                editBuffer.Add(new CodeInstruction(OpCodes.Brtrue_S, bypassPoint));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_0));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_1));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_2));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_3));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_S, 4));
                                editBuffer.Add(new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor),
                                        nameof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor.MyLovePartnersCurrentOrJobBed)
                                    )
                                ));
                                editBuffer.Add(new CodeInstruction(OpCodes.Stloc, foundBed));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldloc, foundBed));
                                editBuffer.Add(new CodeInstruction(OpCodes.Brfalse_S, bypassPoint));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldloc, foundBed));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ret));

                                instruction.WithLabels(bypassPoint);
                                editBuffer.Add(instruction);
                                state = TState.InsertDeferralForFoundDeathrestCasket;
                            } else {
                                editBuffer.Add(instruction);
                            }
                            break;
                        case TState.InsertDeferralForFoundDeathrestCasket:
                            if (
                                instruction.opcode == OpCodes.Ret &&
                                editBuffer.Count >= 3 &&
                                editBuffer[editBuffer.Count - 1].opcode == OpCodes.Ldloc_S &&
                                editBuffer[editBuffer.Count - 2].opcode == OpCodes.Brfalse_S &&
                                editBuffer[editBuffer.Count - 3].opcode == OpCodes.Ldloc_S
                            ) {
                                // ldloc.s 19 (bed)
                                // +dup (bed bed)
                                // +stloc reorderedFromPrevStage (bed)
                                // +call isDeathrestCasketAndDefer (bool)
                                // +brtrue myClosestFreeCommunalBedStageLabel ()
                                // +ldloc reorderedFromPrevStage (bed)
                                // ret ()
                                editBuffer.Add(new CodeInstruction(OpCodes.Dup));
                                editBuffer.Add(new CodeInstruction(OpCodes.Stloc, reorderedFromPrevStage));
                                editBuffer.Add(new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor),
                                        nameof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor.IsDeathrestCasketAndDefer)
                                    )
                                ));
                                editBuffer.Add(new CodeInstruction(OpCodes.Brtrue_S, myClosestFreeCommunalBedStageLabel));
                                editBuffer.Add(new CodeInstruction(OpCodes.Ldloc, reorderedFromPrevStage));
                                editBuffer.Add(instruction);
                                state = TState.InsertMyClosestFreeCommunalBedCall;
                            } else {
                                editBuffer.Add(instruction);
                            }
                            break;
                        case TState.InsertMyClosestFreeCommunalBedCall:
                            if (
                                instruction.opcode == OpCodes.Ret &&
                                editBuffer.Count >= 2 &&
                                editBuffer[editBuffer.Count - 1].opcode == OpCodes.Ldnull &&
                                // more robust check would be && "this is the last instruction in the stream"
                                editBuffer[editBuffer.Count - 2].opcode == OpCodes.Blt
                            ) {
                                LocalBuilder foundBed = generator.DeclareLocal(typeof(Building_Bed));
                                Label bypassPoint = generator.DefineLabel();

                                int insertionCursor = editBuffer.Count - 1;
                                CodeInstruction firstOriginalInstruction = editBuffer[insertionCursor];

                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, myBypassIfTrueConditionLocal).MoveLabelsFrom(firstOriginalInstruction).WithLabels(myClosestFreeCommunalBedStageLabel));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Brtrue_S, bypassPoint));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldarg_0));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldarg_1));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldarg_2));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldarg_3));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldarg_S, 4));
                                // TODO recalculate the list selection so that we don't depend on a locals index
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc_3));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, reorderedFromPrevStage));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor),
                                        nameof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor.MyClosestFreeCommunalBed)
                                    )
                                ));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Stloc, foundBed));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, foundBed));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Brfalse_S, bypassPoint));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, foundBed));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ret));

                                firstOriginalInstruction.WithLabels(bypassPoint);
                                editBuffer.Add(instruction);
                                state = TState.Terminal;
                            } else {
                                editBuffer.Add(instruction);
                            }
                            break;
                        case TState.Terminal:
                            editBuffer.Add(instruction);
                            break;
                        default:
                            Log.Error($"[BOT] Transpiler reached illegal state {state}.");
                            yield break;
                    }
                    while (editBuffer.Count > maxLookbehindWindow) {
                        yield return editBuffer.PopFront();
                    }
                }
                if (state != TState.Terminal) {
                    Log.Error($"[BOT] Transpiler did not reach its terminal state. It only reached state {state}.");
                }
                while (editBuffer.Count > 0) {
                    yield return editBuffer.PopFront();
                }
            }
        }

        [HarmonyPatch()]
        public class Patch_RelationshipAwareSearch_RestUtility_FindBedFor_buildingBed2_SearchPredicate {
            static MethodBase TargetMethod() {
                return typeof(RestUtility)
                    .GetNestedType("<>c__DisplayClass14_3", BindingFlags.NonPublic)
                    .GetMethod("<FindBedFor>b__1", BindingFlags.Instance | BindingFlags.NonPublic)
                ;
            }
            static bool CheckXAttrsIsAssignedToCommunity(Thing bedThing) {
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return false;
                }
                CompBuilding_BedXAttrs bedXAttrs = bedThing.TryGetComp<CompBuilding_BedXAttrs>();
                if (bedXAttrs == null) {
                    return false;
                }
                return bedXAttrs.IsAssignedToCommunity;
            }
            enum TState {
                InsertCheck,
                Terminal
            }
            // // if (!((Building_Bed)b).Medical && (int)b.Position.GetDangerFor(...
            // IL_0000: ldarg.1
            // IL_0001: castclass RimWorld.Building_Bed
            // IL_0006: callvirt instance bool RimWorld.Building_Bed::get_Medical()
            // IL_000b: brtrue IL_00ed <- InsertCheck, branch target is <EXIT POINT>
            // +ldarg.1
            // +call CheckXAttrsIsAssignedToCommunity
            // +brtrue <EXIT POINT>
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                TState state = TState.InsertCheck;

                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case TState.InsertCheck:
                            if (instruction.Branches(out Label? exitPoint) && exitPoint.HasValue) {
                                yield return instruction;
                                yield return new CodeInstruction(OpCodes.Ldarg_1);
                                yield return new CodeInstruction(
                                    OpCodes.Call,
                                    AccessTools.Method(
                                        typeof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor_buildingBed2_SearchPredicate),
                                        nameof(Patch_RelationshipAwareSearch_RestUtility_FindBedFor_buildingBed2_SearchPredicate.CheckXAttrsIsAssignedToCommunity)
                                    )
                                );
                                yield return new CodeInstruction(OpCodes.Brtrue, exitPoint);
                                state = TState.Terminal;
                            } else {
                                yield return instruction;
                            }
                            break;
                        case TState.Terminal:
                            yield return instruction;
                            break;
                        default:
                            Log.Error($"[BOT] Transpiler reached illegal state {state}.");
                            yield break;
                    }
                }
                if (state != TState.Terminal) {
                    Log.Error($"[BOT] Transpiler did not reach its terminal state. It only reached state {state}.");
                }
            }
        }

        // Thoughts
        [HarmonyPatch(typeof(ThoughtWorker_WantToSleepWithSpouseOrLover), "CurrentStateInternal")]
        public class Patch_ThoughtWorker_WantToSleepWithSpouseOrLover_CurrentStateInternal {
            static void Postfix(ThoughtWorker_WantToSleepWithSpouseOrLover __instance, ref ThoughtState __result, Pawn p) {
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return;
                }
                CompPawnXAttrs pawnXAttrs = p.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null && pawnXAttrs.relationshipAwareTracker.UseRelationshipAwareTrackerThoughts()) {
                    __result = pawnXAttrs.relationshipAwareTracker.ThinkWantToSleepWithSpouseOrLover();
                }
            }
        }
        [HarmonyPatch(typeof(ThoughtWorker_SharedBed), "CurrentStateInternal")]
        public class Patch_ThoughtWorker_SharedBed_CurrentStateInternal {
            static void Postfix(ThoughtWorker_SharedBed __instance, ref ThoughtState __result, Pawn p) {
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return;
                }
                CompPawnXAttrs pawnXAttrs = p.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null && pawnXAttrs.relationshipAwareTracker.UseRelationshipAwareTrackerThoughts()) {
                    __result = pawnXAttrs.relationshipAwareTracker.ThinkSharedBed();
                }
            }
        }
        [HarmonyPatch(typeof(Thought_SharedBed), "BaseMoodOffset", MethodType.Getter)]
        public class Patch_Thought_SharedBed_BaseMoodOffsetGetterImpl {
            static void Postfix(Thought_SharedBed __instance, ref float __result) {
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return;
                }
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null && pawnXAttrs.relationshipAwareTracker.UseRelationshipAwareTrackerThoughts()) {
                    if (pawnXAttrs.relationshipAwareTracker.ThinkSharedBed()) {
                        __result = pawnXAttrs.relationshipAwareTracker.sharedBedMoodOffset;
                    }
                }
            }
        }
        [HarmonyPatch(typeof(Toils_LayDown), "ApplyBedRelatedEffects")]
        public class Patch_Toils_LayDown_ApplyBedRelatedEffects {
#if RIMWORLD__1_6
            static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest, int delta) {
#else
            static void Postfix(Pawn p, Building_Bed bed, bool asleep, bool gainRest, bool deathrest) {
#endif
                if (!BedOwnershipTools.Singleton.settings.communalBedsAreRelationshipAware) {
                    return;
                }
#if RIMWORLD__1_6
                if (p.IsHashIntervalTick(GenDate.TicksPerHour, delta)) {
#else
                if (p.IsHashIntervalTick(GenDate.TicksPerHour)) {
#endif
                    // to avoid updating when animals last slept in a communal animal bed
                    if (!p.RaceProps.Humanlike) {
                        return;
                    }
                    if (bed == null) {
                        return;
                    }
                    CompPawnXAttrs pawnXAttrs = p.GetComp<CompPawnXAttrs>();
                    if (pawnXAttrs != null) {
                        CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                        if (bedXAttrs != null) {
                            if (bedXAttrs.IsAssignedToCommunity) {
                                pawnXAttrs.relationshipAwareTracker.Notify_SleptInCommunalBed();
                            }
                        }
                        foreach (Pawn bedPartner in bed.CurOccupants) {
                            if (bedPartner != p) {
                                if (LovePartnerRelationUtility.LovePartnerRelationExists(p, bedPartner)) {
                                    // we don't check Awake in this branch
                                    // e.g. androids don't sleep so just being in bed is good enough
                                    // (generosity is positive)
                                    pawnXAttrs.relationshipAwareTracker.Notify_SleptWithSpouseOrLover(bedPartner);
                                } else {
                                    // we check Awake in this branch but it's probably unnecessary to do so
                                    // (pessimism is positive)
                                    if (!bedPartner.Awake()) {
                                        pawnXAttrs.relationshipAwareTracker.Notify_SleptWithNonPartner(bedPartner);
                                    }
                                }
                            }

                        }
                    }
                }
            }
        }
    }
}
