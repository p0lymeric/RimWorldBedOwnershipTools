using System;
using System.Collections.Generic;
using System.Linq;
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
                            BedOwnershipTools.Singleton.modInteropMarshal.modInterop_OneBedToSleepWithAll.RemoteCall_IfIsPolygamyThenDefineMaster(bed);
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
            static CompAssignableToPawn_Bed MakeCATPBAssignmentGroupOverlayAdapter(CompAssignableToPawn_Bed inner) {
                // CompAssignableToPawn_DeathrestCasket (base game binary)
                if (inner is CompAssignableToPawn_DeathrestCasket catpDC) {
                    return new CATPDCAssignmentGroupOverlayAdapter(catpDC);
                }
                // CompAssignableToPawn_AndroidStand (VRE Android)
                if (BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_IsCompAssignableToPawn_AndroidStand(inner)) {
                    return new CATPBUnspecializedAssignmentGroupOverlayAdapter(
                        inner,
                        assignedAnythingImpl: ModInterop_VanillaRacesExpandedAndroid.AssignedAnythingImpl,
                        assigningCandidatesGetterImpl: ModInterop_VanillaRacesExpandedAndroid.AssigningCandidatesGetterImpl
                    );
                }
                // CompAssignableToPawn_Bed (base game binary)
                return new CATPBAssignmentGroupOverlayAdapter(inner);
            }

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
                    CompAssignableToPawn wrappedInstance = MakeCATPBAssignmentGroupOverlayAdapter(x);
                    if (wrappedInstance != null) {
                        __instance = wrappedInstance;
                    }
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

        public class Patch_CompAssignableToPawn_TryUnassignPawn {
            // Since callers are unaware of multiple bed ownership, there is some ambiguity over
            // if a caller wishes to unbind everything the Pawn owns, or if it just wants the single bed unassigned.

            // The complicating factor is that virtual calls to CATPB/CATPDC reference the base class, so the patches
            // on the derived implementations need to proxy these hints and clear this tracker's state.

            // Note that unlike with hints directly against UnclaimBed, TryUnassignPawn handlers will automatically
            // insert a call to UnclaimBedDirected for the given bed and Pawn pair from the original call.
            // Patches are still necessary to account for pawns who own the bed in the overlay.

            public static bool setBeforeCallingToNotInvalidateAllOverlays = false;
            public static bool setBeforeCallingToInvalidateAllOverlaysWithoutWarning = false;

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

            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_CompAssignableToPawn_TryUnassignPawn),
                            nameof(Patch_CompAssignableToPawn_TryUnassignPawn.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_CompAssignableToPawn_TryUnassignPawn),
                            nameof(Patch_CompAssignableToPawn_TryUnassignPawn.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    true
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_CompAssignableToPawn_TryUnassignPawn),
                            nameof(Patch_CompAssignableToPawn_TryUnassignPawn.HintDontInvalidateOverlays))
                        )
                    },
                    false,
                    false
                );
            }
            public static IEnumerable<CodeInstruction> InsertHintInvalidateAllOverlaysNoErrorTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
                    instructions,
                    (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.TryUnassignPawn))),
                    new[] {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_CompAssignableToPawn_TryUnassignPawn),
                            nameof(Patch_CompAssignableToPawn_TryUnassignPawn.HintInvalidateAllOverlays))
                        )
                    },
                    false,
                    false
                );
            }

            public static void ApplyHarmonyPatches(Harmony harmony) {
                // RimWorld.CompAssignableToPawn.TryUnassignPawn RimWorld.CompAssignableToPawn.PostDeSpawn -- directed, HIT
                harmony.Patch(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostDeSpawn)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));

                // RimWorld.CompAssignableToPawn.TryUnassignPawn RimWorld.CompAssignableToPawn.PostSwapMap -- directed, pawn was destroyed check, HIT
#if RIMWORLD__1_6
                harmony.Patch(AccessTools.Method(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostSwapMap)), transpiler: new HarmonyMethod(InsertHintDontInvalidateOverlaysTranspiler));
#endif

                // RimWorld.CompAssignableToPawn_Bed.TryUnassignPawn RimWorld.Dialog_AssignBuildingOwner.DrawAssignedRow -- directed, other calling paths shouldn't exist, HIT
                // OK hint already inserted in CATPBGroupAssignmentOverlayAdapter
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.TryUnassignPawn))]
        public class Patch_CompAssignableToPawn_Bed_TryUnassignPawn {
            static void Prefix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                if (Patch_CompAssignableToPawn_TryUnassignPawn.setBeforeCallingToNotInvalidateAllOverlays) {
                    Patch_Pawn_Ownership_UnclaimBed.HintDontInvalidateOverlays();
                }
                if (Patch_CompAssignableToPawn_TryUnassignPawn.setBeforeCallingToInvalidateAllOverlaysWithoutWarning) {
                    Patch_Pawn_Ownership_UnclaimBed.HintInvalidateAllOverlays();
                }
                Patch_CompAssignableToPawn_TryUnassignPawn.ClearHints();
            }

            static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    Patch_Pawn_Ownership_UnclaimBed.ClearHints();
                    return;
                }
                CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, pawn, sort, uninstall);
                Patch_Pawn_Ownership_UnclaimBed.ClearHints();
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

            static void Postfix(Pawn_Ownership __instance, ref bool __result) {
                bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                if (!enableBedAssignmentGroups) {
                    ClearHints();
                    return;
                }
                Pawn pawn = DelegatesAndRefs.Pawn_Ownership_pawn(__instance);
                if (!setBeforeCallingToNotInvalidateAllOverlays) {
                    if (!setBeforeCallingToInvalidateAllOverlaysWithoutWarning) {
                        if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging) {
                            Log.Warning($"[BOT] Pawn_Ownership.UnclaimBed was called, but Bed Ownership Tools doesn't have special handling for the calling case. All of {pawn.Label}'s beds have been unassigned, as it is the safest default way to proceed.");
                        }
                    }
                    CATPBAndPOMethodReplacements.UnclaimBedAll(pawn);
                }
                ClearHints();
            }

            public static IEnumerable<CodeInstruction> InsertHintDontInvalidateOverlaysTranspiler(IEnumerable<CodeInstruction> instructions) {
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
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
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
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
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
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
                return TranspilerTemplates.InsertBeforeMatchingCodeInstructionTranspiler(
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

                // RimWorld.CompAssignableToPawn_Bed.TryUnassignPawn -- directed, HIT
                // handled in Patch_CompAssignableToPawn_TryUnassignPawn
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
                Pawn pawn = DelegatesAndRefs.Pawn_AgeTracker_pawn(__instance);
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
                Pawn pawn = DelegatesAndRefs.Pawn_IdeoTracker_pawn(__instance);
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
                Pawn pawn = DelegatesAndRefs.Pawn_Ownership_pawn(__instance);
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
                assignmentGroupsToRemove.Clear();
                foreach (var (assignmentGroup, deathrestCasket) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap) {
                    if (((deathrestCasket.ForPrisoners && !pawn.IsPrisoner && !PawnUtility.IsBeingArrested(pawn)) || (!deathrestCasket.ForPrisoners && pawn.IsPrisoner) || (deathrestCasket.ForColonists && pawn.HostFaction == null))) {
                        assignmentGroupsToRemove.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                    CATPBAndPOMethodReplacements.UnclaimDeathrestCasketDirected(pawn, assignmentGroup);
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

#if RIMWORLD__1_6
        [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.PostSwapMap))]
        public class Patch_CompAssignableToPawn_PostSwapMap {
            static void Postfix(CompAssignableToPawn __instance) {
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
                        if (bedXAttrs.assignedPawnsOverlay[num].DestroyedOrNull() || !bedXAttrs.assignedPawnsOverlay[num].SpawnedOrAnyParentSpawned) {
                            CATPBAndPOMethodReplacements.TryUnassignPawn(__instance, bedXAttrs.assignedPawnsOverlay[num]);
                        }
                    }
                }
            }
        }
#endif

        [HarmonyPatch(typeof(Building_Bed), "RemoveAllOwners")]
        public class Patch_Building_Bed_RemoveAllOwners {
            enum BedKind {
                Bed,
                DeathrestCasket,
                AndroidStand
            }

            static bool Prefix(Building_Bed __instance, ref BedKind __state, bool destroyed = false) {
                // destroying a Building_Bed/marking it as Medical/communal calls RemoveAllOwners, which in turn blindly calls UnclaimBed
                // problem is that bed assignments are affected when performing these actions with deathrest caskets and android stands
                if (__instance.CompAssignableToPawn is CompAssignableToPawn_DeathrestCasket) {
                    // bug filed against the base game for this case
                    // "[1.6.4566] Deconstructing a deathrest casket also unassigns the owner's bed"
                    __state = BedKind.DeathrestCasket;
                    return false;
                }
                if (BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_IsCompAssignableToPawn_AndroidStand(__instance.CompAssignableToPawn)) {
                    __state = BedKind.AndroidStand;
                    return false;
                }
                __state = BedKind.Bed;
                return true;
            }

            static void Postfix(Building_Bed __instance, ref BedKind __state, bool destroyed = false) {
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                    if (__state == BedKind.Bed) {
                        CATPBAndPOMethodReplacements.RemoveAllOwners(__instance, destroyed);
                    }
                }
                if (__state != BedKind.Bed) {
                    foreach (Pawn item in __instance.OwnersForReading.ToList()) {
                        string key = "MessageBedLostAssignment";
                        if (destroyed) {
                            key = "MessageBedDestroyed";
                        }
                        Messages.Message(key.Translate(__instance.def, item), new LookTargets(__instance, item), MessageTypeDefOf.CautionInput, historical: false);
                    }
                }
                if (__state == BedKind.AndroidStand) {
                    CompBuilding_BedXAttrs bedXAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                    if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                        if (bedXAttrs != null) {
                            foreach (Pawn item in bedXAttrs.assignedPawnsOverlay.ToList()) {
                                CATPBAndPOMethodReplacements.ForceRemovePawn(__instance.CompAssignableToPawn, item);
                            }
                        }
                    }
                    // otherwise the stand will throw "Could not find good sleeping slot position for ..." errors
                    // if it is marked Medical or communal
                    foreach (Pawn item in __instance.OwnersForReading.ToList()) {
                        __instance.CompAssignableToPawn.ForceRemovePawn(item);
                    }
                }
                // else if(__state == BedKind.DeathrestCasket) {
                //     // the "right" thing to do here might be to iterate through OwnersForReading and call TryUnassignPawn
                //     // but we don't do it in our workaround patch since it doesn't create problems for us
                //     // pawns will retain ownership of their deathrest casket when the casket's guest type is changed
                //     // deathrest caskets don't support ownerless modes like Medical or communal that could cause them to throw
                // }
            }
        }
    }
}
