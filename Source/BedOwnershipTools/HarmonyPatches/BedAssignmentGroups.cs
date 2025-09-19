using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Multiple bed ownership (assignment groups)
// - when a Pawn wishes to use a bed, do a pre-search against all their owned beds and mark the highest priority accessible bed active (FindBedFor)
// - when the player wishes to edit bed assignments, wrap the passed object with a special class that limits conflicting bed assignments to only
//   be in the same assignment group as the selected bed (DelegateAssignGizmoAction)
// - postfix all mutating users of assignedPawns/uninstallAssignedPawns to apply their changes to their corresponding overlay lists
// - (similar overlay update done in CommunalBeds.cs for Pawn_Ownership.ClaimBedIfNonMedical)
// - augment vanilla game calls to UnclaimBed/TryUnassignPawn to allow pawns to relinquish a precise set of beds per case

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.FindBedFor))]
        [HarmonyPatch(new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?) })]
        public class Patch_RestUtility_FindBedFor {
            static void Prefix(ref Building_Bed __result, Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations = false, GuestStatus? guestStatus = null) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }

                CompPawnXAttrs sleeperXAttrs = sleeper.GetComp<CompPawnXAttrs>();
                if (sleeperXAttrs == null) {
                    return;
                }
                foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                    if (sleeperXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                        if (RestUtility.IsValidBedFor(bed, sleeper, traveler, checkSocialProperness, allowMedBedEvenIfSetToNoCare: false, ignoreOtherReservations, guestStatus)) {
                            sleeper.ownership.ClaimBedIfNonMedical(bed);
                            if (BedOwnershipTools.Singleton.runtimeHandles.modOneBedToSleepWithAllLoadedForCompatPatching) {
                                HarmonyPatches.ModCompatPatches_OneBedToSleepWithAll.RemoteCall_IfIsPolygamyThenDefineMaster(bed);
                            }
                            break;
                        }
                    }
                }
                // TODO want to make the pawn search for beds of lower priority and claim them if present
                // veto - can't really think of a way to do it without multiplying the existing search loop inside FindBedFor. don't want to break that function open
            }
        }

        // Shims the CATPBGroupAssignmentOverlayAdapter onto a CompAssignableToPawn_Bed
        // instance when an owner assignment dialog is opened
        [HarmonyPatch(typeof(CompAssignableToPawn), "<CompGetGizmosExtra>b__31_0")]
        public class Patch_CompAssignableToPawn_CompGetGizmosExtra_DelegateAssignGizmoAction {
            [HarmonyPriority(Priority.First)]
            static void Prefix(ref CompAssignableToPawn __instance) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                if (__instance is CompAssignableToPawn_Bed x) {
                    if (x.parent.GetComp<CompBuilding_BedXAttrs>() == null) {
                        return;
                    }
                    if(ModsConfig.BiotechActive && x.parent.def == ThingDefOf.DeathrestCasket) {
                        return;
                    }
                    __instance = new CATPBGroupAssignmentOverlayAdapter(x);
                }
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.TryAssignPawn))]
        public class Patch_CompAssignableToPawn_Bed_TryAssignPawn {
            static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                CATPBAndPOMethodReplacements.TryAssignPawn(__instance, pawn);
            }
        }

        // All users of this path should set one of the UnclaimBed hints because this function will call UnclaimBed exactly once
        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.TryUnassignPawn))]
        public class Patch_CompAssignableToPawn_Bed_TryUnassignPawn {
            static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, pawn, sort, uninstall);
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostSpawnSetup))]
        public class Patch_CompAssignableToPawn_PostSpawnSetup {
            static void Postfix(CompAssignableToPawn __instance, bool respawningAfterLoad) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                if (__instance is CompAssignableToPawn_Bed) {
                    CATPBAndPOMethodReplacements.PostSpawnSetup(__instance, respawningAfterLoad);
                }
            }
        }

        // We unfortunately need to patch each user of the unclaim method as a special case.
        [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))]
        public class Patch_Pawn_Ownership_UnclaimBed {
            // When UnclaimBed is called, the method isn't told which bed was targeted
            // This works with single bed ownership since the information can be recovered from the Pawn itself
            // i.e. iff there is a bed in the world that has a Pawn in assignedPawns, that Pawn's Pawn_Ownership
            // will have that bed referenced by OwnedBed

            // This scheme obviously breaks when multiple bed ownership is introduced
            // To handle this, we postfix UnclaimBed and introduce a "hinting" system where parameters are
            // set inside this patch before it executes (crude, thread-unsafe way to pass arguments).

            // Several major handling categories emerge:
            // Directed
            // - The caller was detoured/transpiled to call CATPBAndPOMethodReplacements.UnclaimBedDirected for the desired beds
            // - The caller has HintDontInvalidateOverlays calls inserted before its calls to UnclaimBed
            // -> the postfix in this patch will not intervene in unclaiming beds in the overlay
            // InvAll (hinted)
            // - The caller's code was reviewed, and
            //   1) the intent is for the Pawn to lose all its beds
            //   2) each call to UnclaimBed in the function is not qualified by conditionals along the line of "if (the pawn owns a bed) then UnclaimBed"
            //      a) if this is false, then the function may need to be handled as a directed case
            // - The caller has HintInvalidateAllOverlays calls inserted before its calls to UnclaimBed
            // -> the postfix in this patch will relinqiush all the Pawn's beds in the overlay
            // InvAll (unhinted)
            // - The caller's code was not reviewed (e.g. game updates, this mod developer's own miss, or other mods making bed-related calls)
            //   1) the system will automatically assume the intent is for the Pawn to lose all its beds
            //      a) this may be annoying if that is not the case, but it is a safe option
            //   2) the system cannot assure that "if (the pawn owns a bed) then UnclaimBed" cases will be captured
            //      a) the mod will not capture cases where the Pawn only owns inactive beds and there is no active bed to trigger that logic
            //      TODO can make it so that if an active bed is unassigned, then the Pawn will choose an inactive bed to reclaim
            // -> the postfix in this patch will relinqiush all the Pawn's beds in the overlay
            // -> the postfix in this patch will write a warning to the console for debugging purposes

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
                __state = __instance.OwnedBed;
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
                            Log.Warning($"[BOT] Pawn_Ownership.UnclaimBed was called, but Bed Ownership Tools doesn't have special handling for the calling case. All of {pawn.Label}'s beds have been unassigned, as it is the safest default way to proceed.");
                        }
                    }
                    CATPBAndPOMethodReplacements.UnclaimBedAll(pawn);
                } else if (__result) {
                    // activate another bed if possible
                    CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                    if (pawnXAttrs != null) {
                        foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                            if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                                if (bed != __state) {
                                    bed.CompAssignableToPawn.ForceAddPawn(pawn);
                                    HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_intOwnedBed(pawn.ownership) = bed;
                                    break;
                                }
                            }
                        }
                    }
                }
                ClearHints();
            }

            static IEnumerable<CodeInstruction> InsertCodeInstructionsBeforePredicate(
                IEnumerable<CodeInstruction> instructions,
                System.Predicate<CodeInstruction> predicate,
                IEnumerable<CodeInstruction> toInsert,
                bool firstMatchOnly,
                bool errorOnNonMatch
            ) {
                bool everMatched = false;
                foreach (CodeInstruction instruction in instructions) {
                    bool skipInsert = firstMatchOnly && everMatched;
                    if (!skipInsert && predicate(instruction)) {
                        foreach (CodeInstruction newInstruction in toInsert) {
                            yield return newInstruction;
                        }
                        yield return instruction;
                        everMatched = true;
                    } else {
                        yield return instruction;
                    }
                }
                if (!everMatched) {
                    if (errorOnNonMatch) {
                        // we will be proactively accountable for patches to the base game
                        Log.Error("[BOT] Transpiler never found the predicate instruction to trigger code modification");
                    } else if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging) {
                        // to not grab attention when patches to other mods fail to apply
                        Log.Warning("[BOT] Transpiler never found the predicate instruction to trigger code modification");
                    }
                }
            }

            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    false
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimBed))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    false
                );
            }

            public static void HintDontInvalidateOverlaysWithCompAssignableToPawn(CompAssignableToPawn catp) {
                if (catp is CompAssignableToPawn_Bed) {
                    setBeforeCallingToNotInvalidateAllOverlays = true;
                }
            }
            public static void HintInvalidateAllOverlaysWithCompAssignableToPawn(CompAssignableToPawn catp) {
                if (catp is CompAssignableToPawn_Bed) {
                    setBeforeCallingToInvalidateAllOverlaysWithoutWarning = true;
                }
            }
            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTryUnassignPawnTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintDontInvalidateOverlaysWithCompAssignableToPawn))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysTryUnassignPawnTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintInvalidateAllOverlaysWithCompAssignableToPawn))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTryUnassignPawnUncheckedNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    false
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysTryUnassignPawnUncheckedNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return InsertCodeInstructionsBeforePredicate(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_Pawn_Ownership_UnclaimBed),
                            nameof(Patch_Pawn_Ownership_UnclaimBed.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    false
                );
            }

            // TODO can accomplish this with TargetMethods
            public static void ApplyHarmonyPatches(Harmony harmony) {
                // You aren't truly playing a game until you can close your eyes and it continues simulating in your mind...
                // daydreaming... at work... on the road... in bed...
                // you wake up, naked and alone in the... wilderness?... thoughts clouded by an onset of severe nausea...
                // the last thing you remember was going under anaesthesia for knee surgery...
                // suddenly it hits you...
                // no... NO!
                // you forgot to trace one of the branches that lead into UnclaimBed...
                // you reminded yourself to write it down too...
                // you locate a cryptosleep casket and decide to make it another era's problem

                //Verse.Pawn_AgeTracker.BirthdayBiological -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(Pawn_AgeTracker), "BirthdayBiological"), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.JobDriver_TakeToBed.MakeNewToils -- invall buried within a generator, HIT
                harmony.Patch(AccessTools.Method(typeof(JobDriver_TakeToBed), "<MakeNewToils>b__10_1"), transpiler: new HarmonyMethod(InsertHintInvalidateAllOverlaysTranspiler));

                //RimWorld.InteractionWorker_Breakup.Interacted -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.Interacted)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.SpouseRelationUtility.DoDivorce -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.DoDivorce)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.Pawn_IdeoTracker.SetIdeo -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.SetIdeo)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.Pawn_Ownership.ExposeData -- directed, ClaimBedIfNonMedical follows immediately after
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ExposeData)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.Pawn_Ownership.ClaimBedIfNonMedical -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.ClaimBedIfNonMedical)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.Pawn_Ownership.UnclaimAll -- invall, HIT
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.UnclaimAll)), transpiler: new HarmonyMethod(InsertHintInvalidateAllOverlaysTranspiler));

                //RimWorld.Pawn_Ownership.Notify_ChangedGuestStatus -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(Pawn_Ownership), nameof(Pawn_Ownership.Notify_ChangedGuestStatus)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                //RimWorld.Building_Bed.RemoveAllOwners -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(Building_Bed), "RemoveAllOwners"), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                // RimWorld.CompAssignableToPawn_Bed.TryUnassignPawn RimWorld.CompAssignableToPawn.PostDeSpawn -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostDeSpawn)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTryUnassignPawnTranspiler));

                // RimWorld.CompAssignableToPawn_Bed.TryUnassignPawn RimWorld.CompAssignableToPawn.PostSwapMap -- invall, pawn was destroyed check, HIT
#if RIMWORLD__1_6
                harmony.Patch(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostSwapMap)), transpiler: new HarmonyMethod(InsertHintInvalidateAllOverlaysTryUnassignPawnTranspiler));
#endif

                // RimWorld.CompAssignableToPawn_Bed.TryUnassignPawn RimWorld.Dialog_AssignBuildingOwner.DrawAssignedRow -- directed, other calling paths shouldn't exist, HIT
                // OK hint already inserted in CATPBGroupAssignmentOverlayAdapter
            }
        }

        // TODO baby growing up doesn't cause them to let go of their crib
        // should trace the path that sends the X became a child letter and add a bed unassign there
        // otherwise if the player doesn't put cribs in the same assignment group as adult beds, they'll have to manually unassign the cribs
        [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
        public class Patch_Pawn_AgeTracker_BirthdayBiological {
            static void Postfix(Pawn_AgeTracker __instance, int birthdayAge) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                Pawn pawn = HarmonyPatches.DelegatesAndRefs.Pawn_AgeTracker_pawn(__instance);
                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs == null) {
                    return;
                }
                List<AssignmentGroup> assignmentGroupsToRemove = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    if (pawn.ageTracker.CurLifeStage.bodySizeFactor > bed.def.building.bed_maxBodySize) {
                        assignmentGroupsToRemove.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                    CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, assignmentGroup);
                }
            }
        }

        // NOTE this is technically the "divorce"/relationship breakup branch
        [HarmonyPatch(typeof(InteractionWorker_Breakup), nameof(InteractionWorker_Breakup.Interacted))]
        public class Patch_InteractionWorker_Breakup_Interacted {
            static void Postfix(Pawn initiator, Pawn recipient /*, List<RulePackDef> extraSentencePacks, out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets*/) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                // since we're working with the subset of shared beds we only need to iterate through one of the divorcees
                CompPawnXAttrs initXAttrs = initiator.GetComp<CompPawnXAttrs>();
                if (initXAttrs == null) {
                    return;
                }
                List<AssignmentGroup> bedsAGToDistribute = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in initXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        continue;
                    }
                    if (bedXAttrs.assignedPawnsOverlay.Contains(recipient)) {
                        bedsAGToDistribute.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in bedsAGToDistribute) {
                    Pawn relinquisher = (Rand.Value < 0.5f) ? initiator : recipient;
                    CATPBAndPOMethodReplacements.UnclaimBedDirected(relinquisher, assignmentGroup);
                }
            }
        }

        // this is actually the ideological "honey, I joined a monogamous flesh eater cult and we can't be together anymore. it's not you it's me" separation branch
        // (divorce to meet new ideology's requirements on monogamy)
        [HarmonyPatch(typeof(SpouseRelationUtility), nameof(SpouseRelationUtility.DoDivorce))]
        public class Patch_SpouseRelationUtility_DoDivorce {
            static void Postfix(Pawn initiator, Pawn recipient) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                // since we're working with the subset of shared beds we only need to iterate through one of the divorcees
                CompPawnXAttrs initXAttrs = initiator.GetComp<CompPawnXAttrs>();
                if (initXAttrs == null) {
                    return;
                }
                List<AssignmentGroup> bedsAGToDistribute = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in initXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        continue;
                    }
                    if (bedXAttrs.assignedPawnsOverlay.Contains(recipient)) {
                        bedsAGToDistribute.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in bedsAGToDistribute) {
                    Pawn relinquisher = (Rand.Value < 0.5f) ? initiator : recipient;
                    CATPBAndPOMethodReplacements.UnclaimBedDirected(relinquisher, assignmentGroup);
                }
            }
        }

        // and this is the "duuude the trees started talking to me last night they said sleeping together creates baaad vibes... hope you understand" branch
        // (relinquish bed if sleeping with other person violates physical love precept in new ideology)
        [HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.SetIdeo))]
        public class Patch_Pawn_IdeoTracker_SetIdeo {
            static void Postfix(Pawn_IdeoTracker __instance, Ideo ideo) {
                Pawn pawn = HarmonyPatches.DelegatesAndRefs.Pawn_IdeoTracker_pawn(__instance);
                // after the base method, __instance.Ideo == ideo will be true
                // if (__instance.Ideo == ideo || pawn.DevelopmentalStage.Baby()) {
                //     return;
                // }
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs == null) {
                    return;
                }
                List<AssignmentGroup> assignmentGroupsToRemove = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    // if (pawn.ownership.OwnedBed.CompAssignableToPawn.IdeoligionForbids(pawn)) {
                    //     assignmentGroupsToRemove.Add(assignmentGroup);
                    // }
                    CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        continue;
                    }
                    foreach (Pawn assignedPawn in bedXAttrs.assignedPawnsOverlay) {
                        if (!BedUtility.WillingToShareBed(pawn, assignedPawn)) {
                            assignmentGroupsToRemove.Add(assignmentGroup);
                            break;
                        }
                    }
                }
                foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                    CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, assignmentGroup);
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_Ownership), nameof(Pawn_Ownership.Notify_ChangedGuestStatus))]
        public class Patch_Pawn_Ownership_Notify_ChangedGuestStatus {
            static void Postfix(Pawn_Ownership __instance) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                Pawn pawn = HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_pawn(__instance);
                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs == null) {
                    return;
                }
                List<AssignmentGroup> assignmentGroupsToRemove = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                    if (((bed.ForPrisoners && !pawn.IsPrisoner && !PawnUtility.IsBeingArrested(pawn)) || (!bed.ForPrisoners && pawn.IsPrisoner) || (bed.ForColonists && pawn.HostFaction == null))) {
                        assignmentGroupsToRemove.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                    CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, assignmentGroup);
                }
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostDeSpawn))]
        public class Patch_CompAssignableToPawn_PostDeSpawn {
#if RIMWORLD__1_6
            static void Postfix(CompAssignableToPawn __instance, Map map, DestroyMode mode = DestroyMode.Vanish) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                if (__instance is CompAssignableToPawn_Bed) {
                    CompBuilding_BedXAttrs bedXAttrs = __instance.parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    if (mode != DestroyMode.WillReplace) {
                        for (int num = bedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                            CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, bedXAttrs.assignedPawnsOverlay[num], sort: false, !__instance.parent.DestroyedOrNull());
                        }
                    }
                }
            }
#else
            static void Postfix(CompAssignableToPawn __instance, Map map) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                if (__instance is CompAssignableToPawn_Bed) {
                    CompBuilding_BedXAttrs bedXAttrs = __instance.parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    for (int num = bedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                        CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, bedXAttrs.assignedPawnsOverlay[num], sort: false, !__instance.parent.DestroyedOrNull());
                    }
                }
            }
#endif
        }

        [HarmonyPatch(typeof(Building_Bed), "RemoveAllOwners")]
        public class Patch_Building_Bed_RemoveAllOwners {
            static void Postfix(Building_Bed __instance, bool destroyed = false) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    return;
                }
                CATPBAndPOMethodReplacements.RemoveAllOwners(__instance, destroyed);
            }
        }
    }
}
