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
                    harmony.Patch(
                        AccessTools.Method("Nekoemi.LoftBed.Patch_CurrentBed:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Utils.StubRetTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method("Nekoemi.LoftBed.Patch_GetCurOccupant:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.Utils.StubRetTranspiler)
                    );
                }
            }

            // Replacements are defined in ModCompatPatches_LoftBedBunkBeds.cs
        }
    }
}
