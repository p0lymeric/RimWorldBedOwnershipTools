using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

// Harmony patches are applied after settings are loaded from the game and after
// any requested and resolvable references are found by the RuntimeHandleProvider

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static void ApplyHarmonyPatches(BedOwnershipTools mod) {
            PatchInClassShallow(mod.harmony, typeof(HarmonyPatches));
            Patch_CompAssignableToPawn_TryUnassignPawn.ApplyHarmonyPatches(mod.harmony);
            Patch_Pawn_Ownership_UnclaimBed.ApplyHarmonyPatches(mod.harmony);
            Patch_Pawn_Ownership_UnclaimDeathrestCasket.ApplyHarmonyPatches(mod.harmony);

            if (mod.runtimeHandles.modHospitalityLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_Hospitality));
            }
            if (mod.runtimeHandles.modOneBedToSleepWithAllLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_OneBedToSleepWithAll));
                ModCompatPatches_OneBedToSleepWithAll.Patch_UnclaimBedCalls.ApplyHarmonyPatches(mod.harmony);
                ModCompatPatches_OneBedToSleepWithAll.DelegatesAndRefs.ApplyHarmonyPatches(mod.harmony);
            }
            if (mod.runtimeHandles.modLoftBedLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_LoftBed));
                ModCompatPatches_LoftBed.Patch_Unpatches.ApplyHarmonyPatches(mod.harmony);
            }
            if (mod.runtimeHandles.modBunkBedsLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_BunkBeds));
                ModCompatPatches_BunkBeds.Patch_Unpatches.ApplyHarmonyPatches(mod.harmony);
                ModCompatPatches_BunkBeds.DelegatesAndRefs.ApplyHarmonyPatches(mod.harmony);
            }
            if (mod.runtimeHandles.modLoftBedLoadedForCompatPatching || mod.runtimeHandles.modBunkBedsLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_LoftBedBunkBeds));
            }
            if (mod.runtimeHandles.modMultiFloorsLoadedForCompatPatching) {
                PatchInClassShallow(mod.harmony, typeof(HarmonyPatches.ModCompatPatches_MultiFloors));
                ModCompatPatches_MultiFloors.DelegatesAndRefs.ApplyHarmonyPatches(mod.harmony);
            }
        }

        // It's very expensive to construct a PatchClassProcessor against a patch class for an unresolvable function as of Harmony 2.4.1
        // Resolution hits are very cheap (~0ms per hit), but resolution misses are penalizingly expensive (~100 ms per miss)
        // Misses contribute extra seconds of mod load time with Harmony's provided PatchAll/PatchUncategorized/PatchCategory implementations.
        // We know exactly which patches we want Harmony to evaluate to avoid resolution misses.
        public static void PatchInClassShallow(Harmony harmony, Type outerClass) {
            Assembly assembly = Assembly.GetExecutingAssembly();
            IEnumerable<Type> allHarmonyTypesUnderOuterClassNonRecursive =
                from type in outerClass.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                where HarmonyMethodExtensions.GetFromType(type).Count > 0
                select type
            ;
            // Log.Message($"[BOT] PatchInClassShallow {outerClass.Name}: {string.Join(", ", allHarmonyTypesUnderOuterClassNonRecursive.Select(x => x.Name))} EOS");
            foreach (Type type in allHarmonyTypesUnderOuterClassNonRecursive) {
                // using (new DeepProfilerScope($"[BOT] Applying Harmony patches -- {type.Name}")) {
                    PatchClassProcessor pcp = harmony.CreateClassProcessor(type);
                    pcp.Patch();
                // }
            }
        }
    }
}
