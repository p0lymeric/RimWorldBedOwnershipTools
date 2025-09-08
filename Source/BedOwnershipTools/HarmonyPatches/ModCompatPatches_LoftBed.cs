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
        public static class ModCompatPatches_LoftBed {
            public static class Patch_Unpatches {
                public static void ApplyHarmonyPatches(Harmony harmony) {
                    HarmonyPatches.Utils.UnpatchChecked(
                        harmony,
                        AccessTools.Method(typeof(RestUtility), nameof(RestUtility.CurrentBed), new Type[] { typeof(Pawn), typeof(int?).MakeByRefType() }),
                        AccessTools.Method("Nekoemi.LoftBed.Patch_CurrentBed:Postfix")
                    );
                    HarmonyPatches.Utils.UnpatchChecked(
                        harmony,
                        AccessTools.Method(typeof(Building_Bed), nameof(Building_Bed.GetCurOccupant)),
                        AccessTools.Method("Nekoemi.LoftBed.Patch_GetCurOccupant:Postfix")
                    );
                }
            }

            // Replacements are defined in ModCompatPatches_LoftBedBunkBeds.cs
        }
    }
}
