using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_BunkBeds {
            public static class DelegatesAndRefs {
                // BunkBeds.Utils.IsBunkBed()
                public delegate bool MethodDelegate_BunkBed_Utils_IsBunkBed(ThingWithComps bed);
                public static MethodDelegate_BunkBed_Utils_IsBunkBed BunkBed_Utils_IsBunkBed =
                    (ThingWithComps bed) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

                public static void ApplyHarmonyPatches(Harmony harmony) {
                    Type typeUtils = BedOwnershipTools.Singleton.runtimeHandles.typeBunkBeds_Utils;

                    BunkBed_Utils_IsBunkBed =
                        AccessTools.MethodDelegate<MethodDelegate_BunkBed_Utils_IsBunkBed>(
                            AccessTools.Method(typeUtils, "IsBunkBed", new Type[] { typeof(ThingWithComps) })
                        );
                }
            }

            public static bool RemoteCall_IsBunkBed(Building_Bed bed) {
                return DelegatesAndRefs.BunkBed_Utils_IsBunkBed(bed);
            }

            public static class Patch_Unpatches {
                public static void ApplyHarmonyPatches(Harmony harmony) {
                    HarmonyPatches.Utils.UnpatchChecked(
                        harmony,
                        AccessTools.Method(typeof(Building_Bed), nameof(Building_Bed.GetCurOccupant)),
                        AccessTools.Method("BunkBeds.Building_Bed_GetCurOccupant_Patch:Prefix")
                    );
                }
            }

             // Replacements are defined in ModCompatPatches_LoftBedBunkBeds.cs
        }
    }
}
