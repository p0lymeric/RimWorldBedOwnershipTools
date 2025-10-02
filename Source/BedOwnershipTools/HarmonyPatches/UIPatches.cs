using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

// Summary of patches
// UI patches
// Print information about communal beds, pinning, and assignment information in the inspector panel
// Display information on the GUI overlay

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetInspectString))]
        public class Patch_Building_Bed_GetInspectString {
            static bool Prefix(Building_Bed __instance, ref string __result) {
                if(CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(__instance.def)) {
                    return true;
                }
                CompBuilding_BedXAttrs xAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                if (xAttrs == null) {
                    return true;
                }

                __result = GetInspectStringImpl(__instance, xAttrs, HarmonyPatches.DelegatesAndRefs.NonVirtual_ThingWithComps_GetInspectString(__instance));
                return false;
            }

            static string GetInspectStringImpl(Building_Bed __instance, CompBuilding_BedXAttrs xAttrs, string toPrepend) {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(toPrepend);
                if (__instance.def.building.bed_humanlike && __instance.def.building.bed_DisplayOwnerType && __instance.Faction == Faction.OfPlayer) {
                    switch (__instance.ForOwnerType)
                    {
                    case BedOwnerType.Prisoner:
                        stringBuilder.AppendInNewLine("ForPrisonerUse".Translate());
                        break;
                    case BedOwnerType.Slave:
                        stringBuilder.AppendInNewLine("ForSlaveUse".Translate());
                        break;
                    case BedOwnerType.Colonist:
                        stringBuilder.AppendInNewLine("ForColonistUse".Translate());
                        break;
                    default:
                        Log.Error($"Unknown bed owner type: {__instance.ForOwnerType}");
                        break;
                    }
                }
                if (__instance.Medical) {
                    stringBuilder.AppendInNewLine("MedicalBed".Translate());
                    if (__instance.Spawned) {
                        stringBuilder.AppendInNewLine("RoomInfectionChanceFactor".Translate() + ": " + __instance.GetRoom().GetStat(RoomStatDefOf.InfectionChanceFactor).ToStringPercent());
                    }
                }
                else if (__instance.CompAssignableToPawn.PlayerCanSeeAssignments && __instance.def.building.bed_DisplayOwnersInInspectString) {
                    Color grey = new Color(0.5f, 0.5f, 0.5f, 1f);
                    List<Pawn> assignedPawns = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups ? xAttrs.assignedPawnsOverlay : __instance.CompAssignableToPawn.AssignedPawnsForReading;
                    if (assignedPawns.Count == 0) {
                        stringBuilder.AppendInNewLine("Owner".Translate() + ": " + "Nobody".Translate());
                    }
                    else if (assignedPawns.Count == 1) {
                        bool active = __instance.CompAssignableToPawn.AssignedPawnsForReading.Contains(assignedPawns[0]);
                        string pawnString = active ? assignedPawns[0].Label : assignedPawns[0].Label + " " + "BedOwnershipTools.InactiveBrackets".Translate();
                        stringBuilder.AppendInNewLine("Owner".Translate() + ": " + pawnString);
                    }
                    else {
                        stringBuilder.AppendInNewLine("Owners".Translate() + ": ");
                        bool flag = false;
                        for (int i = 0; i < assignedPawns.Count; i++) {
                            if (flag) {
                                stringBuilder.Append(", ");
                            }
                            flag = true;
                            bool active = __instance.CompAssignableToPawn.AssignedPawnsForReading.Contains(assignedPawns[i]);
                            string pawnString = active ? assignedPawns[i].LabelShort : assignedPawns[i].LabelShort + " " + "BedOwnershipTools.InactiveBrackets".Translate();
                            stringBuilder.Append(pawnString);
                        }
                    }
                    if (!__instance.ForPrisoners) {
                        if (xAttrs.IsAssignedToCommunity) {
                            stringBuilder.Append(" " + "BedOwnershipTools.CommunalBrackets".Translate());
                        } else if (xAttrs.IsAssignmentPinned) {
                            stringBuilder.Append(" " + "BedOwnershipTools.PinnedBrackets".Translate());
                        }
                        if (!xAttrs.IsAssignedToCommunity && BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                            stringBuilder.AppendInNewLine("BedOwnershipTools.AssignmentGroup".Translate() + ": " + xAttrs.MyAssignmentGroup.name);
                        }
                    }
                    if (__instance.CompAssignableToPawn.AssignedPawnsForReading.Count == 1 && ChildcareUtility.CanSuckle(__instance.CompAssignableToPawn.AssignedPawnsForReading[0], out var _)) {
                        Pawn p = __instance.CompAssignableToPawn.AssignedPawnsForReading[0];
                        float ambientTemperature = __instance.AmbientTemperature;
                        if (!p.SafeTemperatureRange().IncludesEpsilon(ambientTemperature))
                        {
                            stringBuilder.AppendInNewLine("BedUnsafeTemperature".Translate().Colorize(ColoredText.WarningColor));
                        }
                        else if (!p.ComfortableTemperatureRange().IncludesEpsilon(ambientTemperature))
                        {
                            stringBuilder.AppendInNewLine("BedUncomfortableTemperature".Translate());
                        }
                    }
                }
                return stringBuilder.ToString();
            }
        }

        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.CompInspectStringExtra))]
        public class Patch_CompDeathrestBindable_CompInspectStringExtra {
            static bool Prefix(CompDeathrestBindable __instance, ref string __result) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return true;
                }
                string text = null;
                CompDeathrestBindableXAttrs cdbXAttrs = __instance.parent.GetComp<CompDeathrestBindableXAttrs>();
                Pawn bindee = __instance.BoundPawn ?? cdbXAttrs.boundPawnOverlay;
                bool virtuallyButNotActuallyBound = (__instance.BoundPawn == null) && (cdbXAttrs.boundPawnOverlay != null);
                Gene_Deathrest deathrestGene = bindee?.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                if (bindee != null && deathrestGene != null) {
                    text = text + ("BoundTo".Translate() + ": " + bindee.NameShortColored).Resolve() + string.Format(" ({0}/{1} {2})", deathrestGene.CurrentCapacity, deathrestGene.DeathrestCapacity, "DeathrestCapacity".Translate());
                    if (virtuallyButNotActuallyBound) {
                        text += " " + "BedOwnershipTools.InactiveBrackets".Translate().Resolve();
                    }
                    if (__instance.Props.displayTimeActive && __instance.presenceTicks > 0 && deathrestGene.deathrestTicks > 0) {
                        float f = Mathf.Clamp01((float)__instance.presenceTicks / (float)deathrestGene.deathrestTicks);
                        text += string.Format("\n{0}: {1} / {2} ({3})\n{4}", "TimeActiveThisDeathrest".Translate(), __instance.presenceTicks.ToStringTicksToPeriod(allowSeconds: true, shortForm: true), deathrestGene.deathrestTicks.ToStringTicksToPeriod(allowSeconds: true, shortForm: true), f.ToStringPercent(), "MinimumNeededToApply".Translate(0.75f.ToStringPercent()));
                    }
                } else {
                    text += "WillBindOnFirstUse".Translate();
                }
                __result = text;
                return false;
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "ShouldShowAssignmentGizmo")]
        public class Patch_CompAssignableToPawn_Bed_ShouldShowAssignmentGizmo {
            static void Postfix(CompAssignableToPawn_Bed __instance, ref bool __result) {
                CompBuilding_BedXAttrs xAttrs = __instance.parent.GetComp<CompBuilding_BedXAttrs>();
                if (xAttrs == null) {
                    return;
                }
                __result = __result && !xAttrs.IsAssignedToCommunity;
            }
        }

        // show overlay inactive or bound unassigned as grey
        [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.DrawGUIOverlay))]
        public class Patch_Building_Bed_DrawGUIOverlay {
            static bool Prefix(Building_Bed __instance) {
                if (__instance.Medical || Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest || !__instance.CompAssignableToPawn.PlayerCanSeeAssignments) {
                    return false;
                }

                bool showCommunalGUIOverlayInsteadOfBlankUnderBed = BedOwnershipTools.Singleton.settings.showCommunalGUIOverlayInsteadOfBlankUnderBed;
                bool hideDisplayStringForNonHumanlikeBeds = !__instance.def.building.bed_humanlike && BedOwnershipTools.Singleton.settings.hideGUIOverlayOnNonHumanlikeBeds;

                CompBuilding_BedXAttrs xAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                if (xAttrs == null) {
                    return true;
                }

                Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
                Color grey = new Color(0.6f, 0.6f, 0.6f, 1f);
                Color lightGrey = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                bool boundButNotAssigned = false;
                List<Pawn> assignedPawns = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups ? xAttrs.assignedPawnsOverlay : __instance.CompAssignableToPawn.AssignedPawnsForReading;
                // bleh
                if (assignedPawns.Count == 0) {
                    if (CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(__instance.def)) {
                        CompDeathrestBindableXAttrs cdbXAttrs = __instance.GetComp<CompDeathrestBindableXAttrs>();
                        if (cdbXAttrs != null) {
                            assignedPawns = new();
                            Pawn boundPawn = BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings ? cdbXAttrs.boundPawnOverlay : cdbXAttrs.sibling.BoundPawn;
                            if (boundPawn != null) {
                                assignedPawns.Add(boundPawn);
                                boundButNotAssigned = true;
                            }
                        }
                    }
                }
                if (!__instance.ForPrisoners && !__instance.Medical && xAttrs.IsAssignedToCommunity) {
                    if (showCommunalGUIOverlayInsteadOfBlankUnderBed && !hideDisplayStringForNonHumanlikeBeds) {
                        GenMapUI.DrawThingLabel(__instance, "BedOwnershipTools.CommunalAbbrevBrackets".Translate(), defaultThingLabelColor);
                    }
                } else if (!assignedPawns.Any()) {
                    GenMapUI.DrawThingLabel(__instance, "Unowned".Translate(), defaultThingLabelColor);
                }
                else if (assignedPawns.Count == 1) {
                    Pawn pawn = assignedPawns[0];
                    if ((!pawn.InBed() || pawn.CurrentBed() != __instance) && (!pawn.RaceProps.Animal || Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn))) {
                        if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                            bool active = __instance.CompAssignableToPawn.AssignedPawnsForReading.Contains(pawn);
                            GenMapUI.DrawThingLabel(__instance, pawn.LabelShort, boundButNotAssigned ? lightGrey : active ? defaultThingLabelColor : grey);
                        } else {
                            GenMapUI.DrawThingLabel(__instance, pawn.LabelShort, defaultThingLabelColor);
                        }
                    }
                } else {
                    for (int i = 0; i < assignedPawns.Count; i++) {
                        Pawn pawn2 = assignedPawns[i];
                        if (!pawn2.InBed() || assignedPawns[i].CurrentBed() != __instance || !(pawn2.Position == __instance.GetSleepingSlotPos(i))) {
                            if (pawn2.RaceProps.Animal && !Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn2)) {
                                break;
                            }
                            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                                bool active = __instance.CompAssignableToPawn.AssignedPawnsForReading.Contains(pawn2);
                                GenMapUI.DrawThingLabel(GetMultiOwnersLabelScreenPosFor(__instance, i), pawn2.LabelShort, boundButNotAssigned ? lightGrey : active ? defaultThingLabelColor : grey);
                            } else {
                                GenMapUI.DrawThingLabel(HarmonyPatches.DelegatesAndRefs.Building_Bed_GetMultiOwnersLabelScreenPosFor(__instance, i), pawn2.LabelShort, defaultThingLabelColor);
                            }
                        }
                    }
                }

                if (!__instance.ForPrisoners && !__instance.Medical) {
                    if (!xAttrs.IsAssignedToCommunity && !hideDisplayStringForNonHumanlikeBeds) {
                        xAttrs.DrawPinnedAGLabel();
                    }
                }
                return false;
            }

            public static Vector3 GetMultiOwnersLabelScreenPosFor(Building_Bed thiss, int slotIndex) {
                IntVec3 sleepingSlotPos = thiss.GetSleepingSlotPos(slotIndex);
                Vector3 drawPos = thiss.DrawPos;
                if (thiss.Rotation.IsHorizontal)
                {
                    drawPos.z = (float)sleepingSlotPos.z + 0.6f;
                }
                else
                {
                    drawPos.x = (float)sleepingSlotPos.x + 0.5f;
                    drawPos.z += -0.4f;
                }
                Vector2 vector = drawPos.MapToUIPosition();
                if (!thiss.Rotation.IsHorizontal && thiss.SleepingSlotsCount == 2)
                {
                    vector = AdjustOwnerLabelPosToAvoidOverlapping(thiss, vector, slotIndex);
                }
                return vector;
            }

            public static Vector3 AdjustOwnerLabelPosToAvoidOverlapping(Building_Bed thiss, Vector3 screenPos, int slotIndex) {
                // xattrs null check already performed earlier
                CompBuilding_BedXAttrs xAttrs = thiss.GetComp<CompBuilding_BedXAttrs>();
                Text.Font = GameFont.Tiny;
                float num = Text.CalcSize(xAttrs.assignedPawnsOverlay[slotIndex].LabelShort).x + 1f;
                Vector2 vector = thiss.DrawPos.MapToUIPosition();
                float num2 = Mathf.Abs(screenPos.x - vector.x);
                IntVec3 sleepingSlotPos = thiss.GetSleepingSlotPos(slotIndex);
                if (num > num2 * 2f) {
                    float num3 = 0f;
                    num3 = ((slotIndex != 0) ? ((float)thiss.GetSleepingSlotPos(0).x) : ((float)thiss.GetSleepingSlotPos(1).x));
                    if ((float)sleepingSlotPos.x < num3) {
                        screenPos.x -= (num - num2 * 2f) / 2f;
                    }
                    else {
                        screenPos.x += (num - num2 * 2f) / 2f;
                    }
                }
                return screenPos;
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "AssigningCandidates", MethodType.Getter)]
        public class Patch_CompAssignableToPawn_Bed_AssigningCandidatesGetterImpl {
            static bool MyBypassIfTrueCondition(CompAssignableToPawn_Bed thiss) {
                if (!BedOwnershipTools.Singleton.settings.showColonistsAcrossAllMapsInAssignmentDialog) {
                    return true;
                }

                // for compatibility with other mods that touch AssigningCandidates on non-colonist beds
                // e.g. Set Owner for Prisoner Bed
                Building_Bed bed = (Building_Bed)thiss.parent;
                if (bed.ForPrisoners || bed.Medical) {
                    return true;
                }

                return false;
            }
            // +call MyBypassIfTrueCondition
            // +stloc myBypassIfTrueConditionLocal
            // ...
            // // return from p in parent.Map.mapPawns.PawnsInFaction(Faction.OfPlayer)
            // // where p.IsAnimal && !p.RaceProps.Dryad
            // // orderby p.kindDef.label, p.Label
            // // select p;
            // +<firstOriginalInstructionLabel>
            // +ldloc myBypassIfTrueConditionLocal
            // +brtrue <currentInstructionLabel>
            // +call PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
            // +br <currentInstructionLabel>
            // IL_002d: ldarg.0
            // IL_002e: ldfld class Verse.ThingWithComps Verse.ThingComp::parent
            // IL_0033: callvirt instance class Verse.Map Verse.Thing::get_Map()
            // IL_0038: ldfld class Verse.MapPawns Verse.Map::mapPawns
            // IL_003d: call class RimWorld.Faction RimWorld.Faction::get_OfPlayer()
            // IL_0042: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class Verse.Pawn> Verse.MapPawns::PawnsInFaction(class RimWorld.Faction) <- ScanForPawnsInFactionCall
            // +<currentInstructionLabel>:
            // IL_0047: ldsfld class [mscorlib]System.Func`2<class Verse.Pawn, bool> RimWorld.CompAssignableToPawn_Bed/'<>c'::'<>9__1_0' <- InsertAlternativePawnsInFactionCall
            // ...
            // // return parent.Map.mapPawns.FreeColonists.OrderByDescending(delegate(Pawn p)
            // // {...}).ThenBy((Pawn p) => p.LabelShort);
            // +<firstOriginalInstructionLabel2>
            // +ldloc myBypassIfTrueConditionLocal
            // +brtrue <currentInstructionLabel2>
            // +call PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
            // +br <currentInstructionLabel2>
            // IL_00b4: ldarg.0
            // IL_00b5: ldfld class Verse.ThingWithComps Verse.ThingComp::parent
            // IL_00ba: callvirt instance class Verse.Map Verse.Thing::get_Map()
            // IL_00bf: ldfld class Verse.MapPawns Verse.Map::mapPawns
            // IL_00c4: callvirt instance class [mscorlib]System.Collections.Generic.List`1<class Verse.Pawn> Verse.MapPawns::get_FreeColonists() <- ScanForFreeColonistsCall
            // +<currentInstructionLabel2>:
            // IL_00c9: ldarg.0 <- InsertAlternativeFreeColonistsCall
            // IL_00ca: ldftn instance int32 RimWorld.CompAssignableToPawn_Bed::'<get_AssigningCandidates>b__1_3'(class Verse.Pawn)
            // ...
            enum TState {
                ScanForPawnsInFactionCall,
                InsertAlternativePawnsInFactionCall,
                ScanForFreeColonistsCall,
                InsertAlternativeFreeColonistsCall,
                Terminal
            }
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
                // hilariously overcomplicated
                // too much pride sunk to not give it a chance to exist
                TState state = TState.ScanForPawnsInFactionCall;

                int maxLookbehindWindow = 6;
                List<CodeInstruction> editBuffer = new(maxLookbehindWindow);

                LocalBuilder myBypassIfTrueConditionLocal = generator.DeclareLocal(typeof(bool));

                editBuffer.Add(new CodeInstruction(OpCodes.Ldarg_0));
                editBuffer.Add(new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(
                        typeof(Patch_CompAssignableToPawn_Bed_AssigningCandidatesGetterImpl),
                        nameof(Patch_CompAssignableToPawn_Bed_AssigningCandidatesGetterImpl.MyBypassIfTrueCondition
                    )
                )));
                editBuffer.Add(new CodeInstruction(OpCodes.Stloc, myBypassIfTrueConditionLocal));

                foreach (CodeInstruction instruction in instructions) {
                    switch (state) {
                        case TState.ScanForPawnsInFactionCall:
                            if (instruction.Calls(AccessTools.Method(typeof(MapPawns), nameof(MapPawns.PawnsInFaction)))) {
                                state = TState.InsertAlternativePawnsInFactionCall;
                            }
                            editBuffer.Add(instruction);
                            break;
                        case TState.InsertAlternativePawnsInFactionCall:
                            if (
                                editBuffer.Count >= 6 &&
                                /*editBuffer[editBuffer.Count - 1].opcode == OpCodes.Call &&*/ editBuffer[editBuffer.Count - 1].labels.Empty() &&
                                editBuffer[editBuffer.Count - 2].opcode == OpCodes.Call && editBuffer[editBuffer.Count - 2].labels.Empty() &&
                                editBuffer[editBuffer.Count - 3].opcode == OpCodes.Ldfld && editBuffer[editBuffer.Count - 3].labels.Empty() &&
                                editBuffer[editBuffer.Count - 4].opcode == OpCodes.Callvirt && editBuffer[editBuffer.Count - 4].labels.Empty() &&
                                editBuffer[editBuffer.Count - 5].opcode == OpCodes.Ldfld && editBuffer[editBuffer.Count - 5].labels.Empty() &&
                                editBuffer[editBuffer.Count - 6].opcode == OpCodes.Ldarg_0
                            ) {
                                Label firstOriginalInstructionLabel = generator.DefineLabel();
                                Label currentInstructionLabel = generator.DefineLabel();

                                int insertionCursor = editBuffer.Count - 6;
                                CodeInstruction firstOriginalInstruction = editBuffer[insertionCursor];

                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, myBypassIfTrueConditionLocal).MoveLabelsFrom(firstOriginalInstruction));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Brtrue_S, firstOriginalInstructionLabel));

                                CodeInstruction myReplacementAnimalListCall = new(OpCodes.Call, AccessTools.PropertyGetter(typeof(PawnsFinder),
#if RIMWORLD__1_6
                                    nameof(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)
#else
                                    nameof(PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction)
#endif
                                ));

                                editBuffer.Insert(insertionCursor++, myReplacementAnimalListCall);
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Br, currentInstructionLabel));
                                // existing instructions implicitly start here
                                firstOriginalInstruction.WithLabels(firstOriginalInstructionLabel);
                                // existing instructions implicitly end here
                                editBuffer.Add(instruction.WithLabels(currentInstructionLabel));
                                state = TState.ScanForFreeColonistsCall;
                            } else {
                                Log.Error("[BOT] Transpiler matched call to PawnsInFaction but past tokens didn't match expected sequence.");
                                yield break;
                            }
                            break;
                        case TState.ScanForFreeColonistsCall:
                            if (instruction.Calls(AccessTools.PropertyGetter(typeof(MapPawns), nameof(MapPawns.FreeColonists)))) {
                                state = TState.InsertAlternativeFreeColonistsCall;
                            }
                            editBuffer.Add(instruction);
                            break;
                        case TState.InsertAlternativeFreeColonistsCall:
                            if (
                                editBuffer.Count >= 5 &&
                                /*editBuffer[editBuffer.Count - 1].opcode == OpCodes.Call &&*/ editBuffer[editBuffer.Count - 1].labels.Empty() &&
                                editBuffer[editBuffer.Count - 2].opcode == OpCodes.Ldfld && editBuffer[editBuffer.Count - 2].labels.Empty() &&
                                editBuffer[editBuffer.Count - 3].opcode == OpCodes.Callvirt && editBuffer[editBuffer.Count - 3].labels.Empty() &&
                                editBuffer[editBuffer.Count - 4].opcode == OpCodes.Ldfld && editBuffer[editBuffer.Count - 4].labels.Empty() &&
                                editBuffer[editBuffer.Count - 5].opcode == OpCodes.Ldarg_0
                            ) {
                                Label firstOriginalInstructionLabel = generator.DefineLabel();
                                Label currentInstructionLabel = generator.DefineLabel();

                                int insertionCursor = editBuffer.Count - 5;
                                CodeInstruction firstOriginalInstruction = editBuffer[insertionCursor];

                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Ldloc, myBypassIfTrueConditionLocal).MoveLabelsFrom(firstOriginalInstruction));
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Brtrue_S, firstOriginalInstructionLabel));

                                CodeInstruction myReplacementHumanListCall = new(OpCodes.Call, AccessTools.PropertyGetter(typeof(PawnsFinder),
#if RIMWORLD__1_6
                                    nameof(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
#else
                                    nameof(PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists)
#endif
                                ));

                                editBuffer.Insert(insertionCursor++, myReplacementHumanListCall);
                                editBuffer.Insert(insertionCursor++, new CodeInstruction(OpCodes.Br, currentInstructionLabel));
                                // existing instructions implicitly start here
                                firstOriginalInstruction.WithLabels(firstOriginalInstructionLabel);
                                // existing instructions implicitly end here
                                editBuffer.Add(instruction.WithLabels(currentInstructionLabel));
                                state = TState.Terminal;
                            } else {
                                Log.Error("[BOT] Transpiler matched call to FreeColonists but past tokens didn't match expected sequence.");
                                yield break;
                            }
                            break;
                        case TState.Terminal:
                            editBuffer.Add(instruction);
                            break;
                        default:
                            Log.Error($"[BOT] Transpiler reached illegal state {state}.");
                            yield break;
                    }
                    while (editBuffer.Count > maxLookbehindWindow) {
                        yield return editBuffer.PopFront();
                    }
                }
                if (state != TState.Terminal) {
                    Log.Error($"[BOT] Transpiler did not reach its terminal state. It only reached state {state}.");
                }
                while (editBuffer.Count > 0) {
                    yield return editBuffer.PopFront();
                }
            }

            [HarmonyPriority(Priority.Last)]
            static IEnumerable<Pawn> Postfix(IEnumerable<Pawn> __result, CompAssignableToPawn_Bed __instance) {
                if (MyBypassIfTrueCondition(__instance)) {
                    foreach (Pawn pawn in __result) {
                        yield return pawn;
                    }
                } else {
                    // for compatibility with other mods that insert additional Pawns into the colonist list
                    // e.g. MultiFloors
                    // we'll take a deduplicated superset if we also touched the list
                    HashSet<Pawn> seenPawns = new();
                    foreach (Pawn pawn in __result) {
                        if (seenPawns.Add(pawn)) {
                            yield return pawn;
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(CompAssignableToPawn), nameof(CompAssignableToPawn.CompGetGizmosExtra))]
        public class Patch_CompAssignableToPawn_CompGetGizmosExtra {
            static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, CompAssignableToPawn __instance) {
                foreach (Gizmo x in __result) {
                    yield return x;
                }
                if (__instance.parent is Building_Bed) {
                    CompBuilding_BedXAttrs bedXAttrs = __instance.parent.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs != null) {
                        foreach (Gizmo x in bedXAttrs.CompGetGizmosExtraImpl()) {
                            yield return x;
                        }
                    }
                }
            }
        }


        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.GetGizmos))]
        public class Patch_Gene_Deathrest_GetGizmos {
            static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Gene_Deathrest __instance) {
                bool removedAutoWake = false;
                foreach (Gizmo x in __result) {
                    if (BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                        if (x is Command_Toggle command_Toggle) {
                            if (command_Toggle.defaultLabel == "AutoWake".Translate().CapitalizeFirst()) {
                                removedAutoWake = true;
                                continue;
                            }
                        }
                    }
                    yield return x;
                }
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    if (!BedOwnershipTools.Singleton.settings.hideDeathrestAutoControlsOnPawnWhileAwake || pawnXAttrs.parentPawn.Deathresting) {
                        foreach (Gizmo x in pawnXAttrs.automaticDeathrestTracker.CompGetGizmosExtraImpl(!BedOwnershipTools.Singleton.settings.hideDeathrestAutoControlsOnPawnWhileAwake || removedAutoWake)) {
                            yield return x;
                        }
                    }
                }
            }
        }
    }
}
