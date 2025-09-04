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
            // Dubs Performance Analyzer doesn't play well with reverse patches for some reason
            // this is overwritten with its stub implementation during analyzer instrumentation
            // [HarmonyReversePatch]
            // [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString))]
            // [MethodImpl(MethodImplOptions.NoInlining)]
            // static string RP_ThingWithComps_GetInspectString(ThingWithComps thiss) => throw new NotImplementedException("ReversePatch stub");

            // // StringBuilder stringBuilder = new StringBuilder();
            // IL_0000: newobj instance void [mscorlib]System.Text.StringBuilder::.ctor()
            // IL_0005: stloc.0
            // // stringBuilder.Append(base.GetInspectString());
            // IL_0006: ldloc.0
            // IL_0007: ldarg.0
            // IL_0008: call instance string Verse.ThingWithComps::GetInspectString()
            // IL_000d: callvirt instance class [mscorlib]System.Text.StringBuilder [mscorlib]System.Text.StringBuilder::Append(string) // (StringBuilder)
            // + callvirt instance string [mscorlib]System.Object::ToString() (string)
	        // + ret (string)
            // - IL_0012: pop // ()
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
                foreach (CodeInstruction instruction in instructions) {
                    if (instruction.Calls(AccessTools.Method(typeof(StringBuilder), nameof(StringBuilder.Append), new Type[] { typeof(string) }))) {
                        yield return instruction;
                        yield return new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(StringBuilder),
                            nameof(StringBuilder.ToString))
                        );
                        yield return new CodeInstruction(
                            OpCodes.Ret
                        );
                        yield break;
                    } else {
                        yield return instruction;
                    }
                }
            }

            static void Postfix(Building_Bed __instance, ref string __result) {
                if(ModsConfig.BiotechActive && __instance.def == ThingDefOf.DeathrestCasket) {
                    return;
                }
                CompBuilding_BedXAttrs xAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                if (xAttrs == null) {
                    return;
                }
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(__result);
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
                        // Before this print the overlay owners and whether assignment is active or not
                        if (xAttrs.IsAssignedToCommunity) {
                            // TODO translate
                            stringBuilder.Append(" " + "BedOwnershipTools.CommunalBrackets".Translate());
                        } else if (xAttrs.IsAssignmentPinned) {
                            // TODO pawnsMayTakeVacantPinnedBeds
                            stringBuilder.Append(" " + "BedOwnershipTools.PinnedBrackets".Translate());
                        }
                        if (!xAttrs.IsAssignedToCommunity && BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                            //stringBuilder.AppendInNewLine($"Assignment group: {xAttrs.MyAssignmentGroup.name} (ID {xAttrs.MyAssignmentGroup.id})");
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
                if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableDebugInspectStringListings) {
                    stringBuilder.AppendInNewLine("LoadID: " + __instance.GetUniqueLoadID());
                    StringBuilder_PrintOwnersList(stringBuilder, "assignedPawns", __instance.CompAssignableToPawn.AssignedPawnsForReading);
                    StringBuilder_PrintOwnersList(stringBuilder, "uninstalledAssignedPawns", Traverse.Create(__instance.GetComp<CompAssignableToPawn>()).Field("uninstalledAssignedPawns").GetValue<List<Pawn>>());
                    StringBuilder_PrintOwnersList(stringBuilder, "assignedPawnsOverlay", xAttrs.assignedPawnsOverlay);
                    StringBuilder_PrintOwnersList(stringBuilder, "uninstalledAssignedPawnsOverlay", xAttrs.uninstalledAssignedPawnsOverlay);
                }
                __result = stringBuilder.ToString();
            }

            public static void StringBuilder_PrintOwnersList(StringBuilder stringBuilder, string prefix, List<Pawn> ownersList) {
                if (ownersList.Count == 0) {
                    stringBuilder.AppendInNewLine(prefix + "Owner".Translate() + ": " + "Nobody".Translate());
                }
                else if (ownersList.Count == 1) {
                    stringBuilder.AppendInNewLine(prefix + "Owner".Translate() + ": " + ownersList[0].Label);
                }
                else {
                    stringBuilder.AppendInNewLine(prefix + "Owners".Translate() + ": ");
                    bool flag = false;
                    for (int i = 0; i < ownersList.Count; i++) {
                        if (flag) {
                            stringBuilder.Append(", ");
                        }
                        flag = true;
                        stringBuilder.Append(ownersList[i].LabelShort);
                    }
                }

            }
            // public static void StringBuilder_PrintSafeCribInfo(StringBuilder stringBuilder, string prefix, List<Pawn> ownersList) {
            //     if (ownersList.Count == 1 && ChildcareUtility.CanSuckle(ownersList[0], out var _)) {
            //         Pawn p = ownersList[0];
            //         float ambientTemperature = base.AmbientTemperature;
            //         if (!p.SafeTemperatureRange().IncludesEpsilon(ambientTemperature)) {
            //             stringBuilder.AppendInNewLine("BedUnsafeTemperature".Translate().Colorize(ColoredText.WarningColor));
            //         }
            //         else if (!p.ComfortableTemperatureRange().IncludesEpsilon(ambientTemperature)) {
            //             stringBuilder.AppendInNewLine("BedUncomfortableTemperature".Translate());
            //         }
            //     }
            // }
        }

        [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
        public class Patch_Pawn_GetInspectString {
            static void Postfix(Pawn __instance, ref string __result) {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append(__result);
                if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableDebugInspectStringListings) {
                    if (__instance.ownership.OwnedBed != null) {
                        stringBuilder.AppendInNewLine("INTERNAL " + __instance.ownership.OwnedBed.GetUniqueLoadID());
                    }
                    CompPawnXAttrs xAttrs = __instance.GetComp<CompPawnXAttrs>();
                    foreach(var (assignmentGroup, bed) in xAttrs.assignmentGroupToOwnedBedMap) {
                        stringBuilder.AppendInNewLine(assignmentGroup.name + " " + bed.GetUniqueLoadID());
                    }
                }
                __result = stringBuilder.ToString();
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

        // show overlay inactive as grey
        [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.DrawGUIOverlay))]
        public class Patch_Building_Bed_DrawGUIOverlay {
            static bool Prefix(Building_Bed __instance) {
                if (__instance.Medical || Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest || !__instance.CompAssignableToPawn.PlayerCanSeeAssignments) {
                    return false;
                }
                if(ModsConfig.BiotechActive && __instance.def == ThingDefOf.DeathrestCasket) {
                    return true;
                }

                bool showCommunalGUIOverlayInsteadOfBlankUnderBed = BedOwnershipTools.Singleton.settings.showCommunalGUIOverlayInsteadOfBlankUnderBed;
                bool hideDisplayStringForNonHumanlikeBeds = !__instance.def.building.bed_humanlike && BedOwnershipTools.Singleton.settings.hideGUIOverlayOnNonHumanlikeBeds;

                CompBuilding_BedXAttrs xAttrs = __instance.GetComp<CompBuilding_BedXAttrs>();
                if (xAttrs == null) {
                    return true;
                }

                Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
                Color grey = new Color(0.5f, 0.5f, 0.5f, 1f);
                List<Pawn> assignedPawns = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups ? xAttrs.assignedPawnsOverlay : __instance.CompAssignableToPawn.AssignedPawnsForReading;
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
                            GenMapUI.DrawThingLabel(__instance, pawn.LabelShort, active ? defaultThingLabelColor : grey);
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
                                GenMapUI.DrawThingLabel(GetMultiOwnersLabelScreenPosFor(__instance, i), pawn2.LabelShort, active ? defaultThingLabelColor : grey);
                            } else {
                                GenMapUI.DrawThingLabel(Traverse.Create(__instance).Method("GetMultiOwnersLabelScreenPosFor", i).GetValue<Vector3>(), pawn2.LabelShort, defaultThingLabelColor);
                            }
                        }
                    }
                }

                // TODO can move this to CompBuilding_BedXAttrs
                if (!__instance.ForPrisoners && !__instance.Medical) {
                    if (xAttrs.IsAssignedToCommunity || hideDisplayStringForNonHumanlikeBeds) {
                    } else {
                        string displayString = "(";
                        bool insertComma = false;
                        bool displayMe = false;
                        if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups && xAttrs.MyAssignmentGroup.showDisplay) {
                            displayString += xAttrs.MyAssignmentGroup.name;
                            insertComma = true;
                            displayMe = true;
                        }
                        if (xAttrs.IsAssignmentPinned) {
                            if (insertComma) {
                                displayString += ", ";
                            }
                            displayString += "BedOwnershipTools.PinnedAbbrev".Translate();
                            displayMe = true;
                        }
                        displayString += ")";
                        if (displayMe) {
                            Vector2 labelPos = GenMapUI.LabelDrawPosFor(__instance, -0.4f);
                            labelPos.y += 13f;
                            GenMapUI.DrawThingLabel(labelPos, displayString, defaultThingLabelColor);
                        }
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
        // TODO refactor changes into transpiler
        [HarmonyPatch(typeof(CompAssignableToPawn_Bed), "AssigningCandidates", MethodType.Getter)]
        public class Patch_CompAssignableToPawn_Bed_Bed_AssigningCandidatesGetterImpl {
            static IEnumerable<Pawn> MyAssigningCandidatesGetterImpl(CompAssignableToPawn_Bed thiss) {
                if (!thiss.parent.Spawned) {
                    return Enumerable.Empty<Pawn>();
                }
                if (!thiss.parent.def.building.bed_humanlike) {
#if RIMWORLD__1_6
                    return from p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction
                        where p.IsAnimal && !p.RaceProps.Dryad
                        orderby p.kindDef.label, p.Label
                        select p;
#else
                    return from p in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction
                        where p.IsNonMutantAnimal && !p.RaceProps.Dryad
                        orderby p.kindDef.label, p.Label
                        select p;
#endif
                }
#if RIMWORLD__1_6
                return PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.OrderByDescending(delegate(Pawn p) {
                    if (!thiss.CanAssignTo(p).Accepted) {
                        return 0;
                    }
                    return (!thiss.IdeoligionForbids(p)) ? 1 : 0;
                }).ThenBy((Pawn p) => p.LabelShort);
#else
                return PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists.OrderByDescending(delegate(Pawn p) {
                    if (!thiss.CanAssignTo(p).Accepted) {
                        return 0;
                    }
                    return (!thiss.IdeoligionForbids(p)) ? 1 : 0;
                }).ThenBy((Pawn p) => p.LabelShort);
#endif
            }
            static bool Prefix(CompAssignableToPawn_Bed __instance, ref IEnumerable<Pawn> __result) {
                if (!BedOwnershipTools.Singleton.settings.showColonistsAcrossAllMapsInAssignmentDialog) {
                    return true;
                }

                // for compatibility with other mods that touch AssigningCandidates on non-colonist beds
                // e.g. Set Owner for Prisoner Bed
                Building_Bed bed = (Building_Bed)(__instance.parent);
                if (bed.ForPrisoners || bed.Medical) {
                    return true;
                }

                __result = MyAssigningCandidatesGetterImpl(__instance);
                return false;
            }
        }
    }
}
