using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class ModCompatPatches_Hospitality {
            [HarmonyPatchCategory("HospitalityModCompatPatches")]
            [HarmonyPatch("Hospitality.Patches.Toils_LayDown_Patch+ApplyBedThoughts", "AddedBedIsOwned")]
            public class DoublePatch_Toils_LayDown_ApplyBedThoughts_AddedBedIsOwned {
                // Hospitality prefixes ApplyBedThoughts and detours out immediately afterwards
                // But they introduce a similar patch helper as us in CommunalBeds.cs
                // We'll add our check as an OR'd condition to their patch helper
                static void Postfix(ref bool __result, Pawn pawn, Building_Bed buildingBed) {
                    CompBuilding_BedXAttrs xAttrs = buildingBed.GetComp<CompBuilding_BedXAttrs>();
                    __result = __result || xAttrs.IsAssignedToCommunity;
                }
            }
        }
    }
}
