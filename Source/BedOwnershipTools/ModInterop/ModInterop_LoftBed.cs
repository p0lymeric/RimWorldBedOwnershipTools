using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_LoftBed : ModInterop {
        public Assembly assemblyLoftBed;
        public Type typeLoftBed_Building_LoftBed;

        public ModInterop_LoftBed(bool enabled) : base(enabled) {
            if (enabled) {
                this.assemblyLoftBed = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assy => assy.GetName().Name == "LoftBed");
                if (assemblyLoftBed != null) {
                    this.detected = true;
                    this.typeLoftBed_Building_LoftBed = assemblyLoftBed.GetType("Nekoemi.LoftBed.Building_LoftBed");
                    this.qualified =
                        this.typeLoftBed_Building_LoftBed != null;
                }
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                ModInteropHarmonyPatches.Patch_Unpatches.ApplyHarmonyPatches(this, harmony);
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
        }

        public static class ModInteropHarmonyPatches {
            public static class Patch_Unpatches {
                public static void ApplyHarmonyPatches(ModInterop_LoftBed modInterop, Harmony harmony) {
                    harmony.Patch(
                        AccessTools.Method("Nekoemi.LoftBed.Patch_CurrentBed:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubRetTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method("Nekoemi.LoftBed.Patch_GetCurOccupant:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubRetTranspiler)
                    );
                }
            }
            // Replacements are defined in ModInterop_LoftBedBunkBeds.cs
        }
    }
}
