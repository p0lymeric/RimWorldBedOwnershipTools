using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class Utils {
            // Unfortunately seems to be the most sane way of "unpatching" a prefix/postfix
            public static IEnumerable<CodeInstruction> StubRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ret);
            }
            public static IEnumerable<CodeInstruction> StubPushI4OneRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ldc_I4, 1);
                yield return new CodeInstruction(OpCodes.Ret);
            }
        }
    }
}
