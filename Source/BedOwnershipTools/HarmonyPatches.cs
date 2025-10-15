using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

// Harmony patches are applied after settings are loaded from the game and after
// any requested and resolvable references are found by ModInterop instances

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static void ApplyHarmonyPatches(BedOwnershipTools mod) {
            PatchInClassShallow(mod.harmony, typeof(HarmonyPatches));
            Patch_CompAssignableToPawn_TryUnassignPawn.ApplyHarmonyPatches(mod.harmony);
            Patch_Pawn_Ownership_UnclaimBed.ApplyHarmonyPatches(mod.harmony);
            Patch_Pawn_Ownership_UnclaimDeathrestCasket.ApplyHarmonyPatches(mod.harmony);

            foreach (ModInterop modInterop in mod.modInteropMarshal.modInteropList) {
                modInterop.ApplyHarmonyPatches(mod.harmony);
            }
        }

        // It's very expensive to construct a PatchClassProcessor against a patch class for an unresolvable function as of Harmony 2.4.1
        // Resolution hits are very cheap (~0ms per hit), but resolution misses are penalizingly expensive (~100 ms per miss)
        // Misses contribute extra seconds of mod load time with Harmony's provided PatchAll/PatchUncategorized/PatchCategory implementations.
        // We know exactly which patches we want Harmony to evaluate to avoid resolution misses.
        public static void PatchInClassShallow(Harmony harmony, Type outerClass) {
            IEnumerable<Type> allHarmonyTypesUnderOuterClassNonRecursive =
                from type in outerClass.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                where HarmonyMethodExtensions.GetFromType(type).Count > 0
                select type
            ;
            foreach (Type type in allHarmonyTypesUnderOuterClassNonRecursive) {
                PatchClassProcessor pcp = harmony.CreateClassProcessor(type);
                pcp.Patch();
            }
        }
    }
}
