using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using BedOwnershipTools.Whathecode.System;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_VanillaRacesExpandedAndroid : ModInterop {
        public Assembly assembly;
        public Type typeBuilding_AndroidStand;
        public Type typeCompAssignableToPawn_AndroidStand;
        public Type typeVREA_DefOf;

        public ModInterop_VanillaRacesExpandedAndroid(bool enabled) : base(enabled) {
            if (enabled) {
                this.assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assy => assy.GetName().Name == "VREAndroids");
                if (assembly != null) {
                    this.detected = true;
                    this.typeBuilding_AndroidStand = assembly.GetType("VREAndroids.Building_AndroidStand");
                    this.typeCompAssignableToPawn_AndroidStand = assembly.GetType("VREAndroids.CompAssignableToPawn_AndroidStand");
                    this.typeVREA_DefOf = assembly.GetType("VREAndroids.VREA_DefOf");
                    this.qualified =
                        typeBuilding_AndroidStand != null &&
                        typeCompAssignableToPawn_AndroidStand != null &&
                        typeVREA_DefOf != null;
                }
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                ModInteropDelegatesAndRefs.Resolve(this);
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
        }

        public bool RemoteCall_IsCompAssignableToPawn_AndroidStand(CompAssignableToPawn catp) {
            if (this.active) {
                return this.typeCompAssignableToPawn_AndroidStand.IsInstanceOfType(catp);
            }
            return false;
        }

        public IEnumerable<Building_Bed> RemoteCall_All_Building_AndroidStands() {
            // TODO we can probably perform generation-time verification of a ClassA<Child> -> InterfaceImplementedByClassA<Parent> cast
            // instead of runtime cast checking through object
            // (AccessTools.FieldRef<T, F> appears to document some considerations it makes for variance verification)
            if (this.active && ModInteropDelegatesAndRefs.Building_AndroidStand_stands() is IEnumerable<Building_Bed> x) {
                return x;
            }
            return Enumerable.Empty<Building_Bed>();
        }

        public IEnumerable<Pawn> RemoteCall_IsCompAssignableToPawn_AndroidStand_AssigningCandidates(CompAssignableToPawn_Bed thiss) {
            if (this.active) {
                return ModInteropDelegatesAndRefs.CompAssignableToPawn_AndroidStand_AssigningCandidates_Get(thiss);
            }
            return Enumerable.Empty<Pawn>();
        }

        public static bool AssignedAnythingImpl(CATPBUnspecializedAssignmentGroupOverlayAdapter thiss, Pawn pawn) {
            CompBuilding_BedXAttrs newBedXAttrs = thiss.inner.parent.GetComp<CompBuilding_BedXAttrs>();
            if (newBedXAttrs == null) {
                // assuredly unreachable
                return false;
            }
            foreach (Building_Bed stand in BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_All_Building_AndroidStands()) {
                CompBuilding_BedXAttrs oldBedXAttrs = stand.GetComp<CompBuilding_BedXAttrs>();
                if (oldBedXAttrs == null) {
                    continue;
                }
                if (oldBedXAttrs.MyAssignmentGroup == newBedXAttrs.MyAssignmentGroup && oldBedXAttrs.assignedPawnsOverlay.Contains(pawn)) {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<Pawn> AssigningCandidatesGetterImpl(CATPBUnspecializedAssignmentGroupOverlayAdapter thiss) {
            if (!BedOwnershipTools.Singleton.settings.showColonistsAcrossAllMapsInAssignmentDialog) {
                return thiss.inner.AssigningCandidates;
            } else {
                if (!thiss.inner.parent.Spawned) {
                    return Enumerable.Empty<Pawn>();
                }
#if RIMWORLD__1_6
                return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
#else
                return PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists
#endif
                    .Where((Pawn p) => p.genes?.GetGene(ModInteropDelegatesAndRefs.VREA_DefOf_VREA_MemoryProcessing())?.Active ?? false);
            }
        }

        public static class ModInteropDelegatesAndRefs {
            // CompAssignableToPawn_AndroidStand.stands
            public static AccessTools.FieldRef<object> Building_AndroidStand_stands =
                () => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // CompAssignableToPawn_AndroidStand.AssigningCandidates (getter call)
            public delegate IEnumerable<Pawn> MethodDelegatePropertyGetter_CompAssignableToPawn_AndroidStand_AssigningCandidates(CompAssignableToPawn_Bed thiss);
            public static MethodDelegatePropertyGetter_CompAssignableToPawn_AndroidStand_AssigningCandidates CompAssignableToPawn_AndroidStand_AssigningCandidates_Get =
                (CompAssignableToPawn_Bed thiss) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            // VREA_DefOf.VREA_MemoryProcessing
            public static AccessTools.FieldRef<GeneDef> VREA_DefOf_VREA_MemoryProcessing =
                () => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // VREA_DefOf.VREA_AndroidStandSpot
            public static AccessTools.FieldRef<ThingDef> VREA_DefOf_VREA_AndroidStandSpot =
                () => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // VREA_DefOf.VREA_AndroidStand
            public static AccessTools.FieldRef<ThingDef> VREA_DefOf_VREA_AndroidStand =
                () => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // CompAssignableToPawn_AndroidStand.AssigningCandidates (getter call)
            public delegate string MethodDelegate_Building_AndroidStand_CannotUseNowReason(Building_Bed thiss, Pawn selPawn);
            public static MethodDelegate_Building_AndroidStand_CannotUseNowReason Building_AndroidStand_CannotUseNowReason =
                (Building_Bed thiss, Pawn selPawn) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            public static void Resolve(ModInterop_VanillaRacesExpandedAndroid modInterop) {
                Building_AndroidStand_stands =
                    AccessTools.StaticFieldRefAccess<object>(AccessTools.Field(modInterop.typeBuilding_AndroidStand, "stands"));

                CompAssignableToPawn_AndroidStand_AssigningCandidates_Get =
                    AccessTools.MethodDelegate<MethodDelegatePropertyGetter_CompAssignableToPawn_AndroidStand_AssigningCandidates>(AccessTools.PropertyGetter(modInterop.typeCompAssignableToPawn_AndroidStand, "AssigningCandidates"));

                VREA_DefOf_VREA_MemoryProcessing =
                    AccessTools.StaticFieldRefAccess<GeneDef>(AccessTools.Field(modInterop.typeVREA_DefOf, "VREA_MemoryProcessing"));

                VREA_DefOf_VREA_AndroidStandSpot =
                    AccessTools.StaticFieldRefAccess<ThingDef>(AccessTools.Field(modInterop.typeVREA_DefOf, "VREA_AndroidStandSpot"));

                VREA_DefOf_VREA_AndroidStand =
                    AccessTools.StaticFieldRefAccess<ThingDef>(AccessTools.Field(modInterop.typeVREA_DefOf, "VREA_AndroidStand"));

                Building_AndroidStand_CannotUseNowReason =
                    DelegateHelper.CreateOpenInstanceDelegate<MethodDelegate_Building_AndroidStand_CannotUseNowReason>(AccessTools.Method(modInterop.typeBuilding_AndroidStand, "CannotUseNowReason"), DelegateHelper.CreateOptions.Downcasting);
            }
        }

        public static class ModInteropHarmonyPatches {
            [HarmonyPatch("VREAndroids.JobGiver_FreeMemorySpace", "FindStandFor")]
            public class Patch_JobGiver_FreeMemorySpace_FindStandFor {
                static bool Prefix(ref Building_Bed __result, Pawn pawn) {
                    // no need to account for prisoners as the base mod does not do so (prisoners won't use a stand in jail)
                    // already owned stands
                    IEnumerable<Building_Bed> androidStandsAssociatedWithPawn = BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_All_Building_AndroidStands()
                        .Where(x => x.CompAssignableToPawn.AssignedPawns.Contains(pawn))
                        .OrderBy(x => {
                            CompBuilding_BedXAttrs bedXAttrs = x.GetComp<CompBuilding_BedXAttrs>();
                            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups && bedXAttrs != null) {
                                return bedXAttrs.MyAssignmentGroup.Priority();
                            } else {
                                return int.MaxValue;
                            }
                        });
                    foreach (Building_Bed stand in androidStandsAssociatedWithPawn) {
                        if (ModInteropDelegatesAndRefs.Building_AndroidStand_CannotUseNowReason(stand, pawn) == null && pawn.CanReserveAndReach(stand, PathEndMode.OnCell, Danger.Deadly)) {
                            stand.CompAssignableToPawn.TryAssignPawn(pawn);
                            __result = stand;
                            return false;
                        }
                    }

                    // free owned stands
                    foreach (ThingDef standDef in new ThingDef[] { ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStand(), ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStandSpot() }) {
                        Building_Bed closestStand = (Building_Bed)GenClosest.ClosestThingReachable(
                            pawn.PositionHeld,
                            pawn.MapHeld,
                            ThingRequest.ForDef(standDef),
                            PathEndMode.OnCell,
                            TraverseParms.For(pawn),
                            9999f,
                            (Thing b) => ModInteropDelegatesAndRefs.Building_AndroidStand_CannotUseNowReason((Building_Bed)b, pawn) == null &&
                                (((Building_Bed)b).GetComp<CompBuilding_BedXAttrs>() is CompBuilding_BedXAttrs bedXAttrs ? !bedXAttrs.IsAssignedToCommunity : true) &&
                                (pawn.HasReserved((Building_Bed)b) || pawn.CanReserve((Building_Bed)b, 1, -1, null, ignoreOtherReservations: false))
                        );
                        if (closestStand != null) {
                            CompBuilding_BedXAttrs bedXAttrs = closestStand.GetComp<CompBuilding_BedXAttrs>();
                            if (bedXAttrs != null) {
                                CompBuilding_BedXAttrs oldBedXAttrs = androidStandsAssociatedWithPawn
                                    .Select(x => x.GetComp<CompBuilding_BedXAttrs>())
                                    .Where(x => x != null)
                                    .FirstOrDefault(x => x.MyAssignmentGroup == bedXAttrs.MyAssignmentGroup)
                                ;
                                if (oldBedXAttrs != null && oldBedXAttrs.IsAssignmentPinned) {
                                    continue;
                                }
                            }
                            closestStand.CompAssignableToPawn.TryAssignPawn(pawn);
                            __result = closestStand;
                            return false;
                        }
                    }

                    // communal stands
                    if (BedOwnershipTools.Singleton.settings.enableCommunalBeds) {
                        foreach (ThingDef standDef in new ThingDef[] { ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStand(), ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStandSpot() }) {
                            Building_Bed closestStand = (Building_Bed)GenClosest.ClosestThingReachable(
                                pawn.PositionHeld,
                                pawn.MapHeld,
                                ThingRequest.ForDef(standDef),
                                PathEndMode.OnCell,
                                TraverseParms.For(pawn),
                                9999f,
                                (Thing b) => ModInteropDelegatesAndRefs.Building_AndroidStand_CannotUseNowReason((Building_Bed)b, pawn) == null &&
                                    ((Building_Bed)b).GetComp<CompBuilding_BedXAttrs>() is CompBuilding_BedXAttrs bedXAttrs && bedXAttrs.IsAssignedToCommunity &&
                                    (pawn.HasReserved((Building_Bed)b) || pawn.CanReserve((Building_Bed)b, 1, -1, null, ignoreOtherReservations: false))
                            );
                            if (closestStand != null) {
                                __result = closestStand;
                                return false;
                            }
                        }
                    }
                    __result = null;
                    return false;
                }
            }

            [HarmonyPatch("VREAndroids.CompAssignableToPawn_AndroidStand", "TryAssignPawn")]
            public class Patch_CompAssignableToPawn_AndroidStand_TryAssignPawn {
                static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn) {
                    Building_Bed bed = (Building_Bed)__instance.parent;
                    CompBuilding_BedXAttrs newBedXAttrs = __instance.parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bed.Medical || (newBedXAttrs?.IsAssignedToCommunity ?? false)) {
                        // here we avoid a small issue with VRE Android, because it does not hide the assignment dialog for medical stands
                        // "Could not find good sleeping slot position for ..."
                        // we'll prevent assignments from successfully occurring if a stand is medical or communal
                        foreach (Pawn item in bed.OwnersForReading.ToList()) {
                            __instance.TryUnassignPawn(item);
                        }
                        return;
                    }
                    // Log.Message($"[BOT] TryAssignPawn {__instance.parent.GetUniqueLoadID()} {pawn.LabelShort}");
                    bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                    if (!enableBedAssignmentGroups) {
                        return;
                    }
                    if (newBedXAttrs == null) {
                        return;
                    }
                    if (GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.defaultAssignmentGroup == null) {
                        Log.Warning($"[BOT] A Pawn ({pawn.Label}) tried to claim an android stand but the AssigmentGroupManager hasn't been set up in this save yet. (This is harmless if Bed Ownership Tools was newly added, in which case the claim will be placed in the default group.)");
                        return;
                    }
                    foreach (Pawn item in newBedXAttrs.assignedPawnsOverlay.ToList()) {
                        CATPBAndPOMethodReplacements.ForceRemovePawn(__instance, item);
                    }
                    foreach (Building_Bed stand in BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_All_Building_AndroidStands()) {
                        CompBuilding_BedXAttrs oldBedXAttrs = stand.GetComp<CompBuilding_BedXAttrs>();
                        if (oldBedXAttrs == null) {
                            continue;
                        }
                        if (oldBedXAttrs.MyAssignmentGroup == newBedXAttrs.MyAssignmentGroup && oldBedXAttrs.assignedPawnsOverlay.Contains(pawn)) {
                            CATPBAndPOMethodReplacements.ForceRemovePawn(stand.CompAssignableToPawn, pawn);
                        }
                    }
                    CATPBAndPOMethodReplacements.ForceAddPawn(__instance, pawn);
                }
            }

            [HarmonyPatch("VREAndroids.CompAssignableToPawn_AndroidStand", "TryUnassignPawn")]
            public class Patch_CompAssignableToPawn_AndroidStand_TryUnassignPawn {
                static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool sort = true, bool uninstall = false) {
                    // Log.Message($"[BOT] TryUnassignPawn {__instance.parent.GetUniqueLoadID()} {pawn.LabelShort}");
                    bool enableBedAssignmentGroups = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
                    if (!enableBedAssignmentGroups) {
                        return;
                    }
                    CATPBAndPOMethodReplacements.ForceRemovePawn(__instance, pawn);
                }
            }

            // we include these patches in BedAssignmentGroups.cs as we have a case decoder there
            // [HarmonyPatch(typeof(Building_Bed), "RemoveAllOwners")]
            // public class Patch_Building_Bed_RemoveAllOwners {
            //     static void Postfix(Building_Bed __instance, bool destroyed = false) {
            //         // here we avoid a small issue with VRE Android (setting a stand to Medical does not clear its underlying owners)
            //         // "Could not find good sleeping slot position for ..."
            //         // we also want to clear owners for communal use, to avoid implicating this mod in these error messages
            //         if (BedOwnershipTools.Singleton.modInteropMarshal.modInterop_VanillaRacesExpandedAndroid.RemoteCall_IsCompAssignableToPawn_AndroidStand(__instance.CompAssignableToPawn)) {
            //             CompBuilding_BedXAttrs bedXAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
            //             if (bedXAttrs == null) {
            //                 return;
            //             }
            //             if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
            //                 foreach (Pawn item in bedXAttrs.assignedPawnsOverlay.ToList()) {
            //                     CATPBAndPOMethodReplacements.ForceRemovePawn(__instance.CompAssignableToPawn, item);
            //                 }
            //             }
            //             foreach (Pawn item in __instance.OwnersForReading.ToList()) {
            //                 __instance.CompAssignableToPawn.ForceRemovePawn(item);
            //             }
            //         }
            //     }
            // }

            [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
            public class Patch_DefGenerator_GenerateImpliedDefs_PreResolve {
                static void Postfix(bool hotReload = false) {
                    CompProperties compPropertiesWithBuilding_BedXAttrs = Activator.CreateInstance<CompProperties>();
                    compPropertiesWithBuilding_BedXAttrs.compClass = typeof(CompBuilding_BedXAttrs);
                    ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStandSpot().comps.Add(compPropertiesWithBuilding_BedXAttrs);
                    ModInteropDelegatesAndRefs.VREA_DefOf_VREA_AndroidStand().comps.Add(compPropertiesWithBuilding_BedXAttrs);
                }
            }
        }
    }
}
