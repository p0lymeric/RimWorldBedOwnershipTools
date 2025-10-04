using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        public static class TranspilerTemplates {
            // Unfortunately transpiling seems to be the sanest way of "unpatching" a prefix/postfix
            public static IEnumerable<CodeInstruction> StubRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ret);
            }

            public static IEnumerable<CodeInstruction> StubPushI4OneRetTranspiler(IEnumerable<CodeInstruction> instructions) {
                yield return new CodeInstruction(OpCodes.Ldc_I4, 1);
                yield return new CodeInstruction(OpCodes.Ret);
            }

            public static IEnumerable<CodeInstruction> InjectPerMatchingCodeInstructionTranspiler(
                IEnumerable<CodeInstruction> instructions,
                Predicate<CodeInstruction> predicateToMatch,
                IEnumerable<CodeInstruction> sequenceToInject,
                bool insertIfFalseReplaceIfTrue,
                bool insertSeqBeforeIfFalseInsertSeqAfterIfTrue,
                bool firstMatchOnly,
                bool errorOnNonMatch
            ) {
                bool everMatched = false;
                foreach (CodeInstruction instruction in instructions) {
                    bool skipInsert = firstMatchOnly && everMatched;
                    if (!skipInsert && predicateToMatch(instruction)) {
                        if (insertIfFalseReplaceIfTrue) {
                            // replace
                            foreach (CodeInstruction newInstruction in sequenceToInject) {
                                yield return newInstruction;
                            }
                        } else {
                            // insert
                            if (insertSeqBeforeIfFalseInsertSeqAfterIfTrue) {
                                // Insert the sequence AFTER the matching instruction
                                yield return instruction;
                            }
                            foreach (CodeInstruction newInstruction in sequenceToInject) {
                                yield return newInstruction;
                            }
                            if (!insertSeqBeforeIfFalseInsertSeqAfterIfTrue) {
                                // Insert the sequence BEFORE the matching instruction
                                yield return instruction;
                            }
                        }
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

            public static IEnumerable<CodeInstruction> InsertBeforeMatchingCodeInstructionTranspiler(
                IEnumerable<CodeInstruction> instructions,
                Predicate<CodeInstruction> predicateToMatch,
                IEnumerable<CodeInstruction> sequenceToInject,
                bool firstMatchOnly,
                bool errorOnNonMatch
            ) {
                return InjectPerMatchingCodeInstructionTranspiler(
                    instructions, predicateToMatch, sequenceToInject,
                    insertIfFalseReplaceIfTrue: false, insertSeqBeforeIfFalseInsertSeqAfterIfTrue: false,
                    firstMatchOnly, errorOnNonMatch
                );
            }

            public static IEnumerable<CodeInstruction> ReplaceAtMatchingCodeInstructionTranspiler(
                IEnumerable<CodeInstruction> instructions,
                Predicate<CodeInstruction> predicateToMatch,
                IEnumerable<CodeInstruction> sequenceToInject,
                bool firstMatchOnly,
                bool errorOnNonMatch
            ) {
                return InjectPerMatchingCodeInstructionTranspiler(
                    instructions, predicateToMatch, sequenceToInject,
                    insertIfFalseReplaceIfTrue: true, insertSeqBeforeIfFalseInsertSeqAfterIfTrue: false,
                    firstMatchOnly, errorOnNonMatch
                );
            }
        }
    }
}
