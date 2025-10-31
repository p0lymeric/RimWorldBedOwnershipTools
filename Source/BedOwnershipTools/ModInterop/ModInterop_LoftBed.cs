using System;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_LoftBed : ModInterop {
        // 1.5 (mod targets 1.4)
        // zed.0xff.LoftBed
        // zed_0xff
        // Loft Bed
        // 2961708299
        // LoftBed

        // 1.6
        // Nekoemi.LoftBed
        // Nekoemi, zed_0xff
        // Loft Bed (Continued)
        // 3531523881
        // LoftBed

        public enum PatchVariant {
            PackageIdZed0xffLoftBed,
            PackageIdNekoemiLoftBed,
        }

        public ModContentPack modContentPack;
        public Assembly assembly;
        public PatchVariant patchVariant;
        public Type typeBuilding_LoftBed;

        public string outerNamespace;

        public ModInterop_LoftBed(bool enabled) : base(enabled) {
            if (enabled) {
                Pair<ModContentPack, Assembly>? searchResult = FindMCPAssemblyPair(assembly => assembly.GetName().Name == "LoftBed");
                if (searchResult != null) {
                    this.modContentPack = searchResult.Value.First;
                    this.assembly = searchResult.Value.Second;
                    this.patchVariant = modContentPack.PackageId switch {
                        "zed.0xff.loftbed" => PatchVariant.PackageIdZed0xffLoftBed,
                        _ => PatchVariant.PackageIdNekoemiLoftBed,
                    };
                    this.detected = true;

                    this.outerNamespace = this.patchVariant switch {
                        PatchVariant.PackageIdZed0xffLoftBed => "zed_0xff",
                        _ => "Nekoemi",
                    };

                    this.typeBuilding_LoftBed = assembly.GetType($"{this.outerNamespace}.LoftBed.Building_LoftBed");
                    this.qualified =
                        this.typeBuilding_LoftBed != null;
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
                        AccessTools.Method($"{modInterop.outerNamespace}.LoftBed.Patch_CurrentBed:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubRetTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method($"{modInterop.outerNamespace}.LoftBed.Patch_GetCurOccupant:Postfix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubRetTranspiler)
                    );
                }
            }
            // Replacements are defined in ModInterop_LoftBedBunkBeds.cs
        }
    }
}
