using System;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public abstract class ModInterop {
        public bool enabled;
        public bool detected;
        public bool qualified;
        public bool active;

        public ModInterop(bool enabled) {
            this.enabled = enabled;
        }

        public abstract void ApplyHarmonyPatches(Harmony harmony);

        public abstract void Notify_AGMCompartment_HarmonyPatchState_Constructed();

        public static Pair<ModContentPack, Assembly>? FindMCPAssemblyPair(Predicate<Assembly> assemblyPredicate) {
            foreach (ModContentPack mcp in LoadedModManager.RunningMods) {
                foreach (Assembly assembly in mcp.assemblies.loadedAssemblies) {
                    if (assemblyPredicate(assembly)) {
                        return new Pair<ModContentPack, Assembly>(mcp, assembly);
                    }
                }
            }
            return null;
        }
    }
}
