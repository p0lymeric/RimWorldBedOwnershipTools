// Static job target lookup can be bypassed to exercise dynamic job target lookup
// #define BYPASS_STATIC_JOB_TARGET_LOOKUP
// Dynamic job target lookup caching can be disabled for debugging cache related issues
// #define BYPASS_DYNAMIC_JOB_TARGET_LOOKUP_CACHE

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

namespace BedOwnershipTools {
    public class AGMCompartment_JobDriverTargetBedLUT : AGMCompartment {
        // The upper limit to this cache's size is naturally bounded by the variety of JobDriver subclasses a Pawn could hold
        // in parallel with CurrentBed/GetCurOccupant calls, which should be a fixed and small set.
        // The permanent nature of the caching would probably not interact well with code that dynamically generates throwaway
        // JobDriver types, if any were to exist. We ignore that pathology for now.
        public Dictionary<Type, TargetIndex> jobDriverToLayDownBedTargetIndexCache = new();
        public HashSet<Type> jobDriverDevWarningFilter = new();

        public AGMCompartment_JobDriverTargetBedLUT(Game game, GameComponent_AssignmentGroupManager parent) : base(game, parent) {
        }

        public Building_Bed GetJobTargetedBedFromPawn(Pawn pawn, bool useDynamicLookup = true, bool insertCache = true, bool warn = true) {
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
                        if (useDynamicLookup) {
                            bedIndex = GetLayDownToilTargetedBedFromJobDriver(driver, insertCache, warn);
                        }
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

        public TargetIndex GetLayDownToilTargetedBedFromJobDriver(JobDriver jobDriver, bool insertCache, bool warn) {
            // Permanent blind caching isn't sufficient for JobDriver implementations where TargetIndex usage
            // varies between invokations. We ignore that limitation for now.
            Type jobDriverType = jobDriver.GetType();
            if (warn && Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging && !jobDriverDevWarningFilter.Contains(jobDriverType)) {
                Log.Warning($"[BOT] A Pawn ({jobDriver.pawn.Label}) is performing a Job ({jobDriver.job}) on some bed, but Bed Ownership Tools couldn't match its JobDriver ({jobDriver}) with a statically defined handling case. Future warnings related to this JobDriver type will be suppressed.");
                jobDriverDevWarningFilter.Add(jobDriverType);
            }
            TargetIndex returnVal;
            if (jobDriverToLayDownBedTargetIndexCache.TryGetValue(jobDriverType, out returnVal)) {
                return returnVal;
            } else {
                returnVal = GetLayDownToilTargetedBedFromJobDriverDirect(jobDriver);
#if !BYPASS_DYNAMIC_JOB_TARGET_LOOKUP_CACHE
                if (insertCache) {
                    jobDriverToLayDownBedTargetIndexCache[jobDriverType] = returnVal;
                }
#endif
                return returnVal;
            }
        }

        public TargetIndex GetLayDownToilTargetedBedFromJobDriverDirect(JobDriver jobDriver) {
            // This makes a heavy assumption that all sleep-like jobs invoke the vanilla game's Toils_LayDown toil
            // and that the toil can be uniquely identified by matching a debug string
            if (jobDriver != null && jobDriver.CurToilString == "LayDown") {
                Toil curToil = DelegatesAndRefs.JobDriver_CurToil_Get(jobDriver);
                if (curToil != null && curToil.initAction != null) {
                    object initActionTarget = curToil.initAction.Target;
                    // If some other custom toil generator were to populate a toil with a "LayDown" debug string,
                    // possible indices will either
                    // point to a sensible target,
                    // be None (if bedOrRestSpotIndex is irresolvable in the toil's initAction closure),
                    // or point to None or some non-Building_Bed Thing.
                    // Those outcomes are reasonable for our use.
                    // We really only expect the vanilla case and can tolerate false positives/negatives.
                    TargetIndex bedOrRestSpotIndex = Traverse.Create(initActionTarget).Field("bedOrRestSpotIndex").GetValue<TargetIndex>();
                    return bedOrRestSpotIndex;
                }
            }
            return TargetIndex.None;
        }
    }
}
