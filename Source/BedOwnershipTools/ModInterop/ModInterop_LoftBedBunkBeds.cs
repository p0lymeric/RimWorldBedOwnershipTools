// Fastpath can be bypassed to help exercise static and dynamic job target lookup
// #define BYPASS_FASTPATH
// Static job target lookup can be bypassed to exercise dynamic job target lookup
// #define BYPASS_STATIC_JOB_TARGET_LOOKUP
// Dynamic job target lookup caching can be disabled for debugging cache related issues
// #define BYPASS_DYNAMIC_JOB_TARGET_LOOKUP_CACHE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

// Summary of patches
// - Augment beds introduced by the Loft Bed and Bunk Beds mod to support medical/communal use (GetCurOccupant/CurrentBed)
// - Allow Pawns in loft/bunk beds to watch TV (JobDriver_WatchBuilding.TryMakePreToilReservations)

// The procedures in this file try to map a Pawn to a bed, when multiple beds or Pawns are stacked on the same grid square.

// This code was written to handle 2 mods with different approaches to stacked beds, without introducing extra stored data.
// It adds the ability to use those mods' beds as medical or communal beds.

// An overview of the decoding paths follows.

// The trivial and fast paths capture vanilla cases that can be matched early.
// If a case is classified as trivial or fastpath, its pawns and beds are probably going to be uninteresting, as far as bugs go.
// 1. Trivial, zero beds or zero pawns
// 2. Fastpath, one pawn and one non-bunk bed

// Following fastpath, we handle cases where more than one Pawn can sleep at one position on the map.
// The major requirement is that CurrentBed and GetCurOccupant return unique and matched Pawn <-> {Building_Bed, sleepingSlot} pairs.

// We start by checking the single bunk bed path (Bunk Beds).
// Bunk beds have multiple sleeping slots (probably 2-3) that overlap one another.
// RimWorld cares about a Pawn's current sleeping slot matching assignment list position unless that bed is medical (or communal).
// RimWorld cares about a Pawn's sleeping slot index being unique and matching for a bed between GetOccupant and CurBed.
// When this cannot be uniquely determined like vanilla, the ordering of Pawns in the cell's thingGrid list is abused for the purpose.
// Hence we select between two lists (assignedOwners or thingGrid) to assign sleeping slot numbers.
// 3.1. One bunk bed, medical or communal
// 3.2. One bunk bed, owned

// If there is more than one bed at the position, we enter the loft tower path (Loft Bed).
// Loft towers have multiple (probably 2) beds that overlap one another.
// RimWorld cares about a Pawn's current bed matching their sleeping job's stored Building_Bed reference.
// Hence we look at the Pawn's stored job in order to know which of the overlapping beds they are using.
// We also use the ordering of Pawns in the cell's thingGrid list.
// 4.1. Loft tower, no bunk beds in tower

// Finally, for fun, we handle the crossing between loft and bunk beds.
// Why? Because it's funny to install two separate mods, and stack one mod's bed atop another.
// This handling specially assumes there are two beds, one of which has 1 slot, and another which has 1 or more.
// 4.2.1. Bunk bed below loft bed, medical or communal
// 4.2.2. Bunk bed below loft bed, owned
// 4.2.3. Loft bed above bunk bed

namespace BedOwnershipTools {
    public class ModInterop_LoftBedBunkBeds : ModInterop {
        public ModInterop_LoftBedBunkBeds(bool enabled) : base(enabled) {
            if (this.enabled) {
                this.detected = true;
                this.qualified = true;
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
            ModInteropHarmonyPatches.JobDriverToLayDownBedTargetIndexCache.Clear();
            ModInteropHarmonyPatches.JobDriverDevWarningFilter.Clear();
        }

        public static class ModInteropHarmonyPatches {

            // TPS with 224 chickens in 224 animal sleeping spots
            // (to gauge overhead impact on non-loft, non-bunk beds)
            // -----------------------------------------------------
            // profiler only: 740-800 TPS
            // Bed Ownership Tools: 750-780 TPS
            // Bunk Beds: 690-720 TPS
            // Loft Bed: 690-720 TPS
            // Bed Ownership Tools x Bunk Beds (fastpath): 620-660 TPS
            // Bed Ownership Tools (1.0.5) x Loft Bed: 680-720 TPS
            // Bed Ownership Tools x Loft Bed (fastpath): 680-720 TPS
            // Bed Ownership Tools x Loft Bed (slowpath static): 650-690 TPS
            // Bed Ownership Tools x Loft Bed (slowpath dynamic cached): 650-690 TPS
            // Bed Ownership Tools x Loft Bed (slowpath dynamic uncached): 520-550 TPS

            public static Building_Bed GetJobTargetedBedFromPawn(Pawn pawn, bool insertCache = true, bool warn = true) {
                if (pawn.jobs.curDriver is JobDriver driver) {
                    TargetIndex bedIndex = TargetIndex.None;
                    switch (driver) {
                        // We use a static lookup against all vanilla sleep jobs as a safe optimization before a catch-all dynamic lookup
#if !BYPASS_STATIC_JOB_TARGET_LOOKUP
                        case JobDriver_Wait: // HIT (WaitMaintainPosture, transient state after praying)
                            // A, false
                            bedIndex = TargetIndex.A;
                            break;
                        case JobDriver_RelaxAlone: // HIT (praying)
                            // A, true
                            bedIndex = TargetIndex.A;
                            break;
                        case JobDriver_WatchBuilding: // HIT (WatchTelevision)
                            // C, true
                            bedIndex = TargetIndex.C;
                            break;
                        case JobDriver_Deathrest: // HIT
                            // A, parameterized
                            bedIndex = TargetIndex.A;
                            break;
                        case JobDriver_LayDownResting: // 2nd gen subclass of JobDriver_LayDown, SmokeleafHigh, not sure how to trigger
                            // A, parameterized
                            bedIndex = TargetIndex.A;
                            break;
                        case JobDriver_LayDown: // HIT
                            // A, parameterized
                            bedIndex = TargetIndex.A;
                            break;
                        case JobDriver_Lovin: // HIT
                            // B, true
                            // funny enough, the issue that led to these decoders being written was, in fact related to two Pawns performin' Jobs on their double bed
                            bedIndex = TargetIndex.B;
                            break;
                        case JobDriver_Meditate: // HIT (note, don't cut off legs, instead give plague)
                            // B, parameterized
                            bedIndex = TargetIndex.B;
                            break;
#endif
                        default:
                            bedIndex = GetLayDownToilTargetedBedFromJobDriver(driver, insertCache, warn);
                            break;
                    }
                    if (bedIndex != TargetIndex.None) {
                        if (pawn.CurJob.GetTarget(bedIndex).Thing is Building_Bed bed) {
                            return bed;
                        }
                    }
                }
                return null;
            }

            // The upper limit to this cache's size is naturally bounded by the variety of JobDriver subclasses a Pawn could hold
            // in parallel with CurrentBed/GetCurOccupant calls, which should be a fixed and small set. Issues could occur if
            // an unbounded number of JobDriver types are generated during runtime. We ignore that pathology for now.
            public static Dictionary<Type, TargetIndex> JobDriverToLayDownBedTargetIndexCache = new Dictionary<Type, TargetIndex>();
            public static HashSet<Type> JobDriverDevWarningFilter = new HashSet<Type>();
            public static TargetIndex GetLayDownToilTargetedBedFromJobDriver(JobDriver jobDriver, bool insertCache, bool warn) {
                // Permanent blind caching isn't sufficient for JobDriver implementations where TargetIndex usage
                // varies between invokations. We ignore that limitation for now.
                Type jobDriverType = jobDriver.GetType();
                if (warn && Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging && !JobDriverDevWarningFilter.Contains(jobDriverType)) {
                    Log.Warning($"[BOT] A Pawn ({jobDriver.pawn.Label}) is performing a Job ({jobDriver.job}) on some bed, but Bed Ownership Tools couldn't match its JobDriver ({jobDriver}) with a statically defined handling case. Future warnings related to this JobDriver type will be suppressed.");
                    JobDriverDevWarningFilter.Add(jobDriverType);
                }
                TargetIndex returnVal;
                if (JobDriverToLayDownBedTargetIndexCache.TryGetValue(jobDriverType, out returnVal)) {
                    return returnVal;
                } else {
                    returnVal = GetLayDownToilTargetedBedFromJobDriverDirect(jobDriver);
#if !BYPASS_DYNAMIC_JOB_TARGET_LOOKUP_CACHE
                    if (insertCache) {
                        JobDriverToLayDownBedTargetIndexCache[jobDriverType] = returnVal;
                    }
#endif
                    return returnVal;
                }
            }

            public static TargetIndex GetLayDownToilTargetedBedFromJobDriverDirect(JobDriver jobDriver) {
                // This makes a heavy assumption that all sleep-like jobs invoke the vanilla game's Toils_LayDown toil
                // and that the toil can be uniquely identified by matching a debug string
                if (jobDriver != null && jobDriver.CurToilString == "LayDown") {
                    Toil curToil = DelegatesAndRefs.JobDriver_CurToil_Get(jobDriver);
                    if (curToil != null && curToil.initAction != null) {
                        object initActionTarget = curToil.initAction.Target;
                        // If the above assumption is violated, this would return None (on reflection mismatch) or a reference to some arbitrary Thing
                        // That appears to be a reasonable bound.
                        TargetIndex bedOrRestSpotIndex = Traverse.Create(initActionTarget).Field("bedOrRestSpotIndex").GetValue<TargetIndex>();
                        // We don't check hasBed because a null check is sufficient
                        // bool hasBed = Traverse.Create(initActionTarget).Field("hasBed").GetValue<bool>();
                        return bedOrRestSpotIndex;
                    }
                }
                return TargetIndex.None;
            }

            [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CurrentBed))]
            [HarmonyPatch(new Type[] { typeof(Pawn), typeof(int?) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out })]
            public class Patch_RestUtility_CurrentBed {
                static bool Prefix(ref Building_Bed __result, Pawn p, ref int? sleepingSlot) {
                    // Iterate through the sleeping Pawns and Building_Beds at the given Pawn p's position
                    // and map sleepers to the bed in their job's bed target

                    if (!p.Spawned || p.CurJob == null || !p.GetPosture().InBed()) {
                        __result = null;
                        sleepingSlot = null;
                        return false;
                    }

                    bool checkLoftBed = BedOwnershipTools.Singleton.modInteropMarshal.modInterop_LoftBed.active;
                    bool checkBunkBeds = BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.active;

                    List<Thing> thingList = p.Position.GetThingList(p.Map);
                    List<Building_Bed> beds = new List<Building_Bed>(2);
                    List<Pawn> pawns = new List<Pawn>(4);
                    for (int i = 0; i < thingList.Count; i++) {
                        if (thingList[i] is Building_Bed bed) {
                            beds.Add(bed);
                        }
                        if (checkBunkBeds && thingList[i] is Pawn pawn && pawn.Spawned && pawn.CurJob != null && pawn.GetPosture().InBed()) {
                            pawns.Add(pawn);
                        }
                    }

                    int bedsCnt = beds.Count;

                    // 1. Trivial, zero beds or zero pawns
                    if (bedsCnt == 0) {
                        __result = null;
                        sleepingSlot = null;
                        return false;
                    }

                    bool isOnLoftBedTower = checkLoftBed && bedsCnt > 1;
                    bool isSingleBunkBed = checkBunkBeds && bedsCnt == 1 && BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.RemoteCall_IsBunkBed(beds[0]);
                    bool couldBeLoftBedXBunkBedsCrossing = isOnLoftBedTower && checkBunkBeds && !isSingleBunkBed;

                    // 2. Fastpath, one pawn and one non-bunk bed
#if !BYPASS_FASTPATH
                    if (!isOnLoftBedTower && !isSingleBunkBed && bedsCnt == 1) {
                        Building_Bed bed2 = beds[0];
                        int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed2.Position, bed2.Rotation, bed2.def.size);
                        if (sleepingSlotTmp >= 0) {
                            __result = bed2;
                            sleepingSlot = sleepingSlotTmp;
                            return false;
                        }
                        __result = null;
                        sleepingSlot = null;
                        return false;
                    }
#endif

                    // 3. One bunk bed
                    if (isSingleBunkBed) {
                        Building_Bed bed2 = beds[0];
                        int slotIndex = -1;
                        CompBuilding_BedXAttrs bedXAttrs = bed2.GetComp<CompBuilding_BedXAttrs>();
                        if (bed2.Medical || (bedXAttrs != null && bedXAttrs.IsAssignedToCommunity)) {
                            // 3.1. One bunk bed, medical or communal
                            // abusive indexing but let's hope that the order of still things returned by ThingsListAt is stable
                            slotIndex = pawns.IndexOf(p);
                        } else {
                            // 3.2. One bunk bed, owned
                            // the game expects the pawn to sleep at the same slot as their bed's assignedPawns idx
                            slotIndex = bed2.OwnersForReading.IndexOf(p);
                        }
                        if (slotIndex >= 0 && slotIndex < bed2.SleepingSlotsCount) {
                            __result = bed2;
                            sleepingSlot = slotIndex;
                            return false;
                        }
                    // 4. Loft tower
                    } else if (GetJobTargetedBedFromPawn(p) is Building_Bed bed3) {
                        if (beds.Contains(bed3)) {
                            if (bedsCnt == 1 || (isOnLoftBedTower && !couldBeLoftBedXBunkBedsCrossing)) {
                                // 4.1. Loft tower, no bunk beds in tower
                                // Also vanilla handling case if fastpath is disabled
                                int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed3.Position, bed3.Rotation, bed3.def.size);
                                if (sleepingSlotTmp >= 0) {
                                    __result = bed3;
                                    sleepingSlot = sleepingSlotTmp;
                                    return false;
                                }
                            // 4.2. Bunk bed and loft bed
                            } else if (couldBeLoftBedXBunkBedsCrossing) {
                                // yes, we handle the pathological case where the player has both mods installed and placed a loft bed atop a bunk bed
                                // this requires extra filtering deferred to this scope
                                if (BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.RemoteCall_IsBunkBed(bed3)) {
                                    int slotIndex = -1;
                                    CompBuilding_BedXAttrs bedXAttrs = bed3.GetComp<CompBuilding_BedXAttrs>();
                                    if (bed3.Medical || (bedXAttrs != null && bedXAttrs.IsAssignedToCommunity)) {
                                        // 4.2.1. Bunk bed below loft bed, medical or communal
                                        // abusive indexing but let's hope that the order of still things returned by ThingsListAt is stable
                                        List<Pawn> pawnsInThisBed = pawns.Where(x => GetJobTargetedBedFromPawn(x) == bed3).ToList();
                                        slotIndex = pawnsInThisBed.IndexOf(p);
                                    } else {
                                        // 4.2.2. Bunk bed below loft bed, owned
                                        // the game expects the pawn to sleep at the same slot as their bed's assignedPawns idx
                                        slotIndex = bed3.OwnersForReading.IndexOf(p);
                                    }
                                    if (slotIndex >= 0 && slotIndex < bed3.SleepingSlotsCount) {
                                        __result = bed3;
                                        sleepingSlot = slotIndex;
                                        return false;
                                    }
                                } else {
                                    // 4.2.3. Loft bed above bunk bed
                                    int sleepingSlotTmp = BedUtility.GetSlotFromPosition(p.Position, bed3.Position, bed3.Rotation, bed3.def.size);
                                    if (sleepingSlotTmp >= 0) {
                                        __result = bed3;
                                        sleepingSlot = sleepingSlotTmp;
                                        return false;
                                    }
                                }
                            }
                        }
                    }

                    __result = null;
                    sleepingSlot = null;
                    return false;
                }
            }

            [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetCurOccupant))]
            public class Patch_Building_Bed_GetCurOccupant {
                static bool Prefix(Building_Bed __instance, ref Pawn __result, int slotIndex) {
                    if (!__instance.Spawned) {
                        __result = null;
                        return false;
                    }

                    bool checkLoftBed = BedOwnershipTools.Singleton.modInteropMarshal.modInterop_LoftBed.active;
                    bool checkBunkBeds = BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.active;
                    bool isBunkBed = checkBunkBeds && BedOwnershipTools.Singleton.modInteropMarshal.modInterop_BunkBeds.RemoteCall_IsBunkBed(__instance);

                    IntVec3 sleepingSlotPos;
                    if (isBunkBed) {
                        sleepingSlotPos = __instance.Position;
                    } else {
                        // Bunk Beds patches this function but not for returning the expected result
                        sleepingSlotPos = __instance.GetSleepingSlotPos(slotIndex);
                    }
                    List<Thing> list = __instance.Map.thingGrid.ThingsListAt(sleepingSlotPos);
                    int bedsCnt = 0;
                    List<Pawn> pawns = new List<Pawn>(4);
                    for (int i = 0; i < list.Count; i++){
                        if (checkLoftBed && list[i] is Building_Bed bed) {
                            bedsCnt++;
                        } else if (list[i] is Pawn pawn && pawn.Spawned && pawn.CurJob != null && pawn.GetPosture().InBed()) {
                            pawns.Add(pawn);
                        }
                    }

                    int pawnsCnt = pawns.Count;

                    // 1. Trivial, zero beds or zero pawns
                    if (pawnsCnt == 0) {
                        __result = null;
                        return false;
                    }

                    bool isOnLoftBedTower = checkLoftBed && bedsCnt > 1;
                    bool isSingleBunkBed = isBunkBed && bedsCnt == 1;

                    // 2. Fastpath, one pawn and one non-bunk bed
#if !BYPASS_FASTPATH
                    if (!isOnLoftBedTower && !isBunkBed && pawnsCnt == 1) {
                        __result = pawns[0];
                        return false;
                    }
#endif

                    // 3. One bunk bed
                    if (isSingleBunkBed) {
                        List<Pawn> indexingList = null;
                        CompBuilding_BedXAttrs bedXAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                        if (__instance.Medical || (bedXAttrs != null && bedXAttrs.IsAssignedToCommunity)) {
                            // 3.1. One bunk bed, medical or communal
                            // abusive indexing but let's hope that the order of still things returned by ThingsListAt is stable
                            indexingList = pawns;
                        } else {
                            // 3.2. One bunk bed, owned
                            // the game expects the pawn to sleep at the same slot as their bed's assignedPawns idx
                            indexingList = __instance.OwnersForReading;
                        }
                        if (slotIndex < indexingList.Count) {
                            Pawn pawn = indexingList[slotIndex];
                            __result = pawn;
                            return false;
                        }
                    // 4.1. Loft tower, no bunk beds in tower
                    // 4.2.3. Loft bed above bunk bed
                    } else if (bedsCnt == 1 || (isOnLoftBedTower && !isBunkBed)) {
                        foreach (Pawn pawn in pawns) {
                            if (GetJobTargetedBedFromPawn(pawn) is Building_Bed bed) {
                                if (bed == __instance) {
                                    __result = pawn;
                                    return false;
                                }
                            }
                        }
                    // 4.2. Bunk bed and loft bed
                    } else if (isOnLoftBedTower && isBunkBed) {
                        List<Pawn> indexingList = null;
                        CompBuilding_BedXAttrs bedXAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                        if (__instance.Medical || (bedXAttrs != null && bedXAttrs.IsAssignedToCommunity)) {
                            // 4.2.1. Bunk bed below loft bed, medical or communal
                            // abusive indexing but let's hope that the order of still things returned by ThingsListAt is stable
                            indexingList = pawns.Where(x => GetJobTargetedBedFromPawn(x) == __instance).ToList();
                        } else {
                            // 4.2.2. Bunk bed below loft bed, owned
                            // the game expects the pawn to sleep at the same slot as their bed's assignedPawns idx
                            indexingList = __instance.OwnersForReading;
                        }
                        if (slotIndex < indexingList.Count) {
                            Pawn pawn = indexingList[slotIndex];
                            if (GetJobTargetedBedFromPawn(pawn) is Building_Bed bed) {
                                if (bed == __instance) {
                                    // Log.Message($"{__instance.GetUniqueLoadID()} called GetCurOccupant on slot {slotIndex} and matched {pawn.Label}");
                                    __result = pawn;
                                    return false;
                                }
                            }
                        }
                    }

                    // by elimination the Pawn doesn't have a valid sleeping slot
                    __result = null;
                    return false;
                }
            }

            [HarmonyPatch(typeof(JobDriver_WatchBuilding), nameof(JobDriver_WatchBuilding.TryMakePreToilReservations))]
            public class Patch_JobDriver_WatchBuilding_TryMakePreToilReservations {
                // a moment to remember the legless Pawns whose sacrifices brought you working loft/bunk bed TV watching
                static bool IsTargetCBuildingBedIfNotThenTryReserveTargetB(JobDriver thiss, bool errorOnFailed) {
                    LocalTargetInfo targetC = thiss.job.targetC;
                    bool isTargetCBuildingBed = targetC.HasThing && targetC.Thing is Building_Bed;
                    if (!isTargetCBuildingBed) {
                        if (!thiss.pawn.ReserveSittableOrSpot(thiss.job.targetB.Cell, thiss.job, errorOnFailed)) {
                            return false;
                        }
                    }
                    return true;
                }
                // // if (!pawn.ReserveSittableOrSpot(job.targetB.Cell, job, errorOnFailed))
                // IL_0034: ldarg.0
                // IL_0035: ldfld class Verse.Pawn Verse.AI.JobDriver::pawn
                // IL_003a: ldarg.0
                // IL_003b: ldfld class Verse.AI.Job Verse.AI.JobDriver::job
                // IL_0040: ldflda valuetype Verse.LocalTargetInfo Verse.AI.Job::targetB
                // IL_0045: call instance valuetype Verse.IntVec3 Verse.LocalTargetInfo::get_Cell()
                // IL_004a: ldarg.0
                // IL_004b: ldfld class Verse.AI.Job Verse.AI.JobDriver::job
                // IL_0050: ldarg.1
                // ~ (Pawn IntVec3 Job bool)
                // + pop (Pawn IntVec3 Job)
                // + pop (Pawn IntVec3)
                // + pop (Pawn)
                // + pop ()
                // -IL_0051: call bool Verse.AI.ReservationUtility::ReserveSittableOrSpot(class Verse.Pawn, valuetype Verse.IntVec3, class Verse.AI.Job, bool) (bool)
                // + ldarg.0 (JobDriver)
                // + ldarg.1 (JobDriver bool)
                // + call IsTargetCBuildingBedIfNotThenTryReserveTargetB (bool)
                // ~ "if true then consider TargetB reservation a success and jump to TargetC reservation"
                // IL_0056: brtrue.s IL_005a
                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                    // TODO rewrite with token lookahead
                    return HarmonyPatches.TranspilerTemplates.ReplaceAtMatchingCodeInstructionTranspiler(
                        instructions,
                        (CodeInstruction instruction) => instruction.Calls(AccessTools.Method(typeof(ReservationUtility), nameof(ReservationUtility.ReserveSittableOrSpot))),
                        new[] {
                            new CodeInstruction(OpCodes.Pop),
                            new CodeInstruction(OpCodes.Pop),
                            new CodeInstruction(OpCodes.Pop),
                            new CodeInstruction(OpCodes.Pop),
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldarg_1),
                            new CodeInstruction(
                                OpCodes.Call,
                                AccessTools.Method(
                                    typeof(Patch_JobDriver_WatchBuilding_TryMakePreToilReservations),
                                    nameof(Patch_JobDriver_WatchBuilding_TryMakePreToilReservations.IsTargetCBuildingBedIfNotThenTryReserveTargetB)
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
}
