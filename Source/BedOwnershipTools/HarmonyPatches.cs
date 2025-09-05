using RimWorld;
using Verse;
using HarmonyLib;

// Harmony patches are applied after settings are loaded from the game and after
// any requested and resolvable references are found by the RuntimeHandleProvider

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static void ApplyHarmonyPatches(BedOwnershipTools mod) {
            mod.harmony.PatchAllUncategorized();
            Patch_Pawn_Ownership_UnclaimBed.ApplyHarmonyPatches(mod.harmony);
            if (mod.runtimeHandles.modHospitalityLoadedForCompatPatching) {
                mod.harmony.PatchCategory("HospitalityModCompatPatches");
            }
            if (mod.runtimeHandles.modOneBedToSleepWithAllLoadedForCompatPatching) {
                mod.harmony.PatchCategory("OneBedToSleepWithAllModCompatPatches");
                ModCompatPatches_OneBedToSleepWithAll.Patch_UnclaimBedCalls.ApplyHarmonyPatches(mod.harmony);
            }
            if (mod.runtimeHandles.modLoftBedLoadedForCompatPatching) {
                mod.harmony.PatchCategory("LoftBedModCompatPatches");
            }
        }
    }
}
