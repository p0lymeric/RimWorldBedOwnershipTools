using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class Utils {
            public static void UnpatchChecked(Harmony harmony, MethodBase original, MethodInfo patch) {
                if (original != null && patch != null) {
                    harmony.Unpatch(original, patch);
                    // TODO also determine whether unpatching actually did anything--it appears Harmony doesn't warn
                } else {
                    Log.Error("[BOT] Got null method descriptors for unpatching");
                }
            }
        }
    }
}
