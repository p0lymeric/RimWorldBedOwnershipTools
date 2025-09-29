using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using BedOwnershipTools.Whathecode.System;

// Summary of patches

// With patches
// When a pawn needs to medical rest, no changes.
// When a pawn needs to rest:
// 1. if multiple bed assignments is enabled
//    a) find highest priority accessible bed
//    b) if that bed is on a different level and inactive, activate the bed
// 2. if communal beds are enabled
//    a) if the pawn couldn't reach an owned bed, then perform an all-floor scan for communal beds
//    b) go to the closest floor with communal beds

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_MultiFloors {
            public static class DelegatesAndRefs {
                // StairPathFinderUtility.CanReachAcrossLevel()
                public delegate bool MethodDelegate_StairPathFinderUtility_CanReachAcrossLevel(Pawn pawn, Thing thing, Map map, bool isRaider = false);
                public static MethodDelegate_StairPathFinderUtility_CanReachAcrossLevel StairPathFinderUtility_CanReachAcrossLevel =
                    (Pawn pawn, Thing thing, Map map, bool isRaider) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                // public delegate bool MethodDelegate_LevelUtility_TryGetLevelControllerOnCurrentTile(Map map, out object controller);
                // public static MethodDelegate_LevelUtility_TryGetLevelControllerOnCurrentTile LevelUtility_TryGetLevelControllerOnCurrentTile =
                //     (Map map, out object controller) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate IEnumerable<Map> MethodDelegate_LevelUtility_GetOtherMapVerticallyOutwardFromCache(Map map, object controller, int maxMapsToExplore = -1);
                public static MethodDelegate_LevelUtility_GetOtherMapVerticallyOutwardFromCache LevelUtility_GetOtherMapVerticallyOutwardFromCache =
                    (Map map, object controller, int maxMapsToExplore) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate bool MethodDelegate_LevelUtility_ConnectedToOtherLevel(Map map);
                public static MethodDelegate_LevelUtility_ConnectedToOtherLevel LevelUtility_ConnectedToOtherLevel =
                    (Map map) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate ref Map MethodDelegate_PrepatcherFields_GroundMap(Map map);
                public static MethodDelegate_PrepatcherFields_GroundMap PrepatcherFields_GroundMap =
                    (Map map) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate object MethodDelegate_PrepatcherFields_LevelMapComp(Map map);
                public static MethodDelegate_PrepatcherFields_LevelMapComp PrepatcherFields_LevelMapComp =
                    (Map map) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate ref int MethodDelegate_PrepatcherFields_Level(Map map);
                public static MethodDelegate_PrepatcherFields_Level PrepatcherFields_Level =
                    (Map map) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate ref object MethodDelegate_PrepatcherFields_LevelSettings(Pawn pawn);
                public static MethodDelegate_PrepatcherFields_LevelSettings PrepatcherFields_LevelSettings =
                    (Pawn pawn) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public delegate void MethodDelegate_Pawn_LevelSettings_SetLivingLevelTo(object thiss, int level);
                public static MethodDelegate_Pawn_LevelSettings_SetLivingLevelTo Pawn_LevelSettings_SetLivingLevelTo =
                    (object thiss, int level) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public static void ApplyHarmonyPatches(Harmony harmony) {
                    Type typeStairPathFinderUtility = BedOwnershipTools.Singleton.runtimeHandles.typeMultiFloors_StairPathFinderUtility;
                    Type typeLevelUtility = BedOwnershipTools.Singleton.runtimeHandles.typeMultiFloors_LevelUtility;
                    Type typePrepatcherFields = BedOwnershipTools.Singleton.runtimeHandles.typeMultiFloors_PrepatcherFields;
                    Type typePawn_LevelSettings = BedOwnershipTools.Singleton.runtimeHandles.typeMultiFloors_Pawn_LevelSettings;

                    StairPathFinderUtility_CanReachAcrossLevel =
                        AccessTools.MethodDelegate<MethodDelegate_StairPathFinderUtility_CanReachAcrossLevel>(
                            AccessTools.Method(typeStairPathFinderUtility, "CanReachAcrossLevel")
                        );

                    // LevelUtility_TryGetLevelControllerOnCurrentTile =
                    //     DelegateHelper.CreateDelegate<MethodDelegate_LevelUtility_TryGetLevelControllerOnCurrentTile>(
                    //         AccessTools.Method(typeLevelUtility, "TryGetLevelControllerOnCurrentTile"),
                    //         null,
                    //         DelegateHelper.CreateOptions.Downcasting
                    //     );

                    LevelUtility_GetOtherMapVerticallyOutwardFromCache =
                        DelegateHelper.CreateDelegate<MethodDelegate_LevelUtility_GetOtherMapVerticallyOutwardFromCache>(
                            AccessTools.Method(typeLevelUtility, "GetOtherMapVerticallyOutwardFromCache"),
                            null,
                            DelegateHelper.CreateOptions.Downcasting
                        );

                    LevelUtility_ConnectedToOtherLevel =
                        AccessTools.MethodDelegate<MethodDelegate_LevelUtility_ConnectedToOtherLevel>(
                            AccessTools.Method(typeLevelUtility, "ConnectedToOtherLevel")
                        );

                    PrepatcherFields_GroundMap =
                        AccessTools.MethodDelegate<MethodDelegate_PrepatcherFields_GroundMap>(
                            AccessTools.Method(typePrepatcherFields, "GroundMap")
                        );

                    PrepatcherFields_LevelMapComp =
                        AccessTools.MethodDelegate<MethodDelegate_PrepatcherFields_LevelMapComp>(
                            AccessTools.Method(typePrepatcherFields, "LevelMapComp")
                        );

                    PrepatcherFields_Level =
                        AccessTools.MethodDelegate<MethodDelegate_PrepatcherFields_Level>(
                            AccessTools.Method(typePrepatcherFields, "Level")
                        );

                    PrepatcherFields_LevelSettings =
                        AccessTools.MethodDelegate<MethodDelegate_PrepatcherFields_LevelSettings>(
                            AccessTools.Method(typePrepatcherFields, "LevelSettings")
                        );

                    Pawn_LevelSettings_SetLivingLevelTo =
                        DelegateHelper.CreateOpenInstanceDelegate<MethodDelegate_Pawn_LevelSettings_SetLivingLevelTo>(
                            AccessTools.Method(typePawn_LevelSettings, "SetLivingLevelTo"),
                            DelegateHelper.CreateOptions.Downcasting
                        );
                }
            }

            public static object RemoteCall_TryGetLevelControllerOnCurrentTile(Map map) {
                if (!DelegatesAndRefs.LevelUtility_ConnectedToOtherLevel(map)) {
                    return null;
                }
                if (DelegatesAndRefs.PrepatcherFields_GroundMap(map) is Map map2) {
                    return DelegatesAndRefs.PrepatcherFields_LevelMapComp(map2);
                }
                return null;
            }

            public static void RemoteCall_SetLivingLevelToMapLevel(Pawn pawn, Map map) {
                int num = DelegatesAndRefs.PrepatcherFields_Level(map);
                object levelSettings = DelegatesAndRefs.PrepatcherFields_LevelSettings(pawn);

                if (levelSettings != null) {
                    DelegatesAndRefs.Pawn_LevelSettings_SetLivingLevelTo(levelSettings, num);
                }
            }

            [HarmonyPatch("MultiFloors.HarmonyPatches.HarmonyPatch_MainColonistBehavior", "BackToLivingLevelForGetRest")]
            public class DoublePatch_JobGiver_GetRest_TryGiveJob_Prefix {
                public static Building_Bed PreprocessAndReturnBedCandidate(Pawn sleeper) {

                    CompPawnXAttrs sleeperXAttrs = sleeper.GetComp<CompPawnXAttrs>();
                    if (sleeperXAttrs == null) {
                        return sleeper.ownership.OwnedBed;
                    }

                    // pick and activate the highest priority reachable bed
                    if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                        foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                            if (sleeperXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed candidateBed)) {
                                if (sleeper.Map == candidateBed.Map && sleeper.CanReach(candidateBed, PathEndMode.OnCell, Danger.Deadly)) {
                                    return candidateBed;
                                } else if (candidateBed.Map?.Tile == sleeper.Map.Tile) {
                                    // repro for issue: tri103
                                    if (DelegatesAndRefs.StairPathFinderUtility_CanReachAcrossLevel(sleeper, candidateBed, null)) {
                                        sleeper.ownership.ClaimBedIfNonMedical(candidateBed);
                                        // don't really want to interact with this mod's settings
                                        // this change would occur too late for the existing transpiler implementation (where we'd perform this fixup after livingMap is already retrieved)
                                        // RemoteCall_SetLivingLevelToMapLevel(sleeper, candidateBed.Map);
                                        return candidateBed;
                                    }
                                }
                            }
                        }
                    }

                    // communal bed search
                    if (BedOwnershipTools.Singleton.settings.enableCommunalBeds && sleeper.Map != null) {
                        if (RestUtility.FindBedFor(sleeper, sleeper, checkSocialProperness: true) is Building_Bed bed) {
                            return bed;
                        }
                        // TODO level scanning settings?
                        // if (HarmonyPatch_ScanJobsOnOtherLevel.ScanningOtherLevel || HarmonyPatch_ScanJobsOnOtherLevelPrioritized.ScanningOtherLevels) {
                        //     return null;
                        // }
                        if (RemoteCall_TryGetLevelControllerOnCurrentTile(sleeper.Map) is object controller) {
                            foreach (Map item in DelegatesAndRefs.LevelUtility_GetOtherMapVerticallyOutwardFromCache(sleeper.Map, controller)) {
                                foreach (Thing item2 in item.listerThings.ThingsInGroup(ThingRequestGroup.Bed)) {
                                    if (item2 is Building_Bed building_Bed) {
                                        CompBuilding_BedXAttrs bedXAttrs = building_Bed.GetComp<CompBuilding_BedXAttrs>();
                                        // could also do a IsValidBedFor check, but AnyUnoccupiedSleepingSlot is probably good enough
                                        if (bedXAttrs != null && bedXAttrs.IsAssignedToCommunity && building_Bed.AnyUnoccupiedSleepingSlot && DelegatesAndRefs.StairPathFinderUtility_CanReachAcrossLevel(sleeper, building_Bed, item)) {
                                            // don't really want to interact with this mod's settings
                                            // RemoteCall_SetLivingLevelToMapLevel(sleeper, building_Bed.Map);
                                            return building_Bed;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return sleeper.ownership.OwnedBed;
                }
                // // Thing ownedBed = pawn.ownership.OwnedBed;
                // ...
                // IL_005e: ldarg.0 (Pawn)
                // -IL_005f: ldfld class ['Assembly-CSharp']RimWorld.Pawn_Ownership ['Assembly-CSharp']Verse.Pawn::ownership (Pawn_Ownership) <- S0 match and delete this insn
                // -IL_0064: callvirt instance class ['Assembly-CSharp']RimWorld.Building_Bed ['Assembly-CSharp']RimWorld.Pawn_Ownership::get_OwnedBed() (Building_Bed) <- S1 delete this insn
                // +call PreprocessAndReturnBedCandidate
                // IL_0069: stloc.2
                // ...
                // // __result = CrossLevelJobFactory.MakeChangeLevelThroughStairJob(ownedBed, livingMap);
                // IL_0133: ldarg.1
                // IL_0134: ldloc.2
                // IL_0135: ldloc.1
                // IL_0136: ldnull
                // +pop
                // +pop
                // +ldloc.2
                // +callvirt Thing.get_Map()
                // +ldnull
                // IL_0137: call class ['Assembly-CSharp']Verse.AI.Job MultiFloors.Jobs.CrossLevelJobFactory::MakeChangeLevelThroughStairJob(class ['Assembly-CSharp']Verse.Thing, class ['Assembly-CSharp']Verse.Map, class MultiFloors.Stair) <- S2 match and replace arguments
                // IL_013c: stind.ref
                // // return false;
                // IL_013d: ldc.i4.0
                // IL_013e: ret
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                    int state = 0;
                    foreach (CodeInstruction instruction in instructions) {
                        switch (state) {
                            case 0: // COPY_UNTIL_AND_DELETE_PAWN_OWNERSHIP_LOADFIELD
                                if (instruction.LoadsField(AccessTools.DeclaredField(typeof(Pawn), nameof(Pawn.ownership)))) {
                                    state++;
                                } else {
                                    yield return instruction;
                                }
                                break;
                            case 1: // DELETE_GET_OWNEDBED_CALL_AND_INSERT_REPLACEMENT
                                if (instruction.Calls(AccessTools.PropertyGetter(typeof(Pawn_Ownership), nameof(Pawn_Ownership.OwnedBed)))) {
                                    yield return new CodeInstruction(
                                        OpCodes.Call,
                                        AccessTools.Method(
                                            typeof(DoublePatch_JobGiver_GetRest_TryGiveJob_Prefix),
                                            nameof(DoublePatch_JobGiver_GetRest_TryGiveJob_Prefix.PreprocessAndReturnBedCandidate)
                                        )
                                    );
                                    state++;
                                } else {
                                    Log.Error("[BOT] Transpiler failed to match expected instruction token callvirt Pawn_Ownership.get_OwnedBed");
                                    yield break;
                                }
                                break;
                            case 2: // COPY_UNTIL_AND_REPLACE_MAKECHANGELEVELTHROUGHSTAIRJOB_ARGS
                                // If the bed's level differs from living level, the Pawn will switch to the bed's map
                                // then fail to complete routing towards the bed they've chosen.
                                // We'll set the target map in the find function to be that of the bed's level, like TryGoToMedicalBedOnOtherLevels.
                                if (instruction.Calls(AccessTools.Method("MultiFloors.Jobs.CrossLevelJobFactory:MakeChangeLevelThroughStairJob"))) {
                                    yield return new CodeInstruction(OpCodes.Pop);
                                    yield return new CodeInstruction(OpCodes.Pop);
                                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                                    yield return new CodeInstruction(
                                        OpCodes.Callvirt,
                                        AccessTools.PropertyGetter(
                                            typeof(Thing),
                                            nameof(Thing.Map)
                                        )
                                    );
                                    yield return new CodeInstruction(OpCodes.Ldnull);
                                    yield return instruction;
                                    state++;
                                } else {
                                    yield return instruction;
                                }
                                break;
                            case 3: // COPY_FINAL
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

            // BackToLivingLevelForGetRest
            // JobDriver_TakeToBedOnOtherLevel
        }
    }
}
