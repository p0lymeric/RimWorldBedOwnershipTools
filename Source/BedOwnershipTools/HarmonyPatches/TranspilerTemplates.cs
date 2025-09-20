using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class TranspilerTemplates {
            // Unfortunately transpiling seems to be the most sane way of "unpatching" a prefix/postfix
            public static IEnumerable<CodeInstruction> StubRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ret);
            }
            public static IEnumerable<CodeInstruction> StubPushI4OneRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ldc_I4, 1);
                yield return new CodeInstruction(OpCodes.Ret);
            }

            public static IEnumerable<CodeInstruction> InsertCodeInstructionsBeforePredicateTranspiler(
                IEnumerable<CodeInstruction> instructions,
                System.Predicate<CodeInstruction> predicate,
                IEnumerable<CodeInstruction> toInsert,
                bool firstMatchOnly,
                bool errorOnNonMatch
            ) {
                bool everMatched = false;
                foreach (CodeInstruction instruction in instructions) {
                    bool skipInsert = firstMatchOnly && everMatched;
                    if (!skipInsert && predicate(instruction)) {
                        foreach (CodeInstruction newInstruction in toInsert) {
                            yield return newInstruction;
                        }
                        yield return instruction;
                        everMatched = true;
                    } else {
                        yield return instruction;
                    }
                }
                if (!everMatched) {
                    if (errorOnNonMatch) {
                        // we will be proactively accountable for patches to the base game
                        Log.Error("[BOT] Transpiler never found the predicate instruction to trigger code modification");
                    } else if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableUnaccountedCaseLogging) {
                        // to not grab attention when patches to other mods fail to apply
                        Log.Warning("[BOT] Transpiler never found the predicate instruction to trigger code modification");
                    }
                }
            }
        }
    }
}
