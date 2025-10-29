using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using BedOwnershipTools.Whathecode.System;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_BunkBeds : ModInterop {
        public Assembly assemblyBunkBeds;
        public Type typeBunkBeds_Utils;
        public Type typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch;
        public Type typeBunkBeds_CompBunkBed;

        public ModInterop_BunkBeds(bool enabled) : base(enabled) {
            if (enabled) {
                this.assemblyBunkBeds = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assy => assy.GetName().Name == "BunkBeds");
                if (this.assemblyBunkBeds != null) {
                    this.detected = true;
                    this.typeBunkBeds_Utils = assemblyBunkBeds.GetType("BunkBeds.Utils");
                    this.typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch = assemblyBunkBeds.GetType("BunkBeds.Building_Bed_DrawGUIOverlay_Patch");
                    this.typeBunkBeds_CompBunkBed = assemblyBunkBeds.GetType("BunkBeds.CompBunkBed");
                    this.qualified =
                        this.typeBunkBeds_Utils != null &&
                        this.typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch != null &&
                        this.typeBunkBeds_CompBunkBed != null;
                }
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                ModInteropHarmonyPatches.Patch_Unpatches.ApplyHarmonyPatches(this, harmony);
                ModInteropDelegatesAndRefs.Resolve(this);
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
        }

        public bool RemoteCall_IsBunkBed(Building_Bed bed) {
            if (this.active) {
                return ModInteropDelegatesAndRefs.BunkBed_Utils_IsBunkBed(bed);
            }
            return false;
        }

        public static class ModInteropDelegatesAndRefs {
            // BunkBeds.Utils.IsBunkBed()
            public delegate bool MethodDelegate_BunkBed_Utils_IsBunkBed(ThingWithComps bed);
            public static MethodDelegate_BunkBed_Utils_IsBunkBed BunkBed_Utils_IsBunkBed =
                (ThingWithComps bed) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            // Building_Bed_DrawGUIOverlay_Patch.guestBedType
            public static AccessTools.FieldRef<Type> Building_Bed_DrawGUIOverlay_Patch_guestBedType =
                () => throw new NotImplementedException("[BOT] Tried to call a field ref access delegate stub");

            // CompBunkBed.GetMultiOwnersLabelScreenPosFor()
            public delegate Vector3 MethodDelegate_CompBunkBed_GetMultiOwnersLabelScreenPosFor(object thiss, int slotIndex);
            public static MethodDelegate_CompBunkBed_GetMultiOwnersLabelScreenPosFor CompBunkBed_GetMultiOwnersLabelScreenPosFor =
                (object thiss, int slotIndex) => throw new NotImplementedException("[BOT] Tried to call a method delegate stub");

            public static void Resolve(ModInterop_BunkBeds modInterop) {
                Type typeUtils = modInterop.typeBunkBeds_Utils;
                Type typeBuilding_Bed_DrawGUIOverlay_Patch = modInterop.typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch;
                Type typeCompBunkBed = modInterop.typeBunkBeds_CompBunkBed;

                BunkBed_Utils_IsBunkBed =
                    AccessTools.MethodDelegate<MethodDelegate_BunkBed_Utils_IsBunkBed>(
                        AccessTools.Method(typeUtils, "IsBunkBed", new Type[] { typeof(ThingWithComps) })
                    );

                Building_Bed_DrawGUIOverlay_Patch_guestBedType =
                    AccessTools.StaticFieldRefAccess<Type>(AccessTools.Field(typeBuilding_Bed_DrawGUIOverlay_Patch, "guestBedType"));

                CompBunkBed_GetMultiOwnersLabelScreenPosFor =
                    DelegateHelper.CreateOpenInstanceDelegate<MethodDelegate_CompBunkBed_GetMultiOwnersLabelScreenPosFor>(
                        AccessTools.Method(typeCompBunkBed, "GetMultiOwnersLabelScreenPosFor"),
                        DelegateHelper.CreateOptions.Downcasting
                    );
            }
        }

        public static class ModInteropHarmonyPatches {
            public static class Patch_Unpatches {
                public static void ApplyHarmonyPatches(ModInterop_BunkBeds modInterop, Harmony harmony) {
                    harmony.Patch(
                        AccessTools.Method("BunkBeds.Building_Bed_GetCurOccupant_Patch:Prefix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubPushI4OneRetTranspiler)
                    );
                    harmony.Patch(
                        AccessTools.Method("BunkBeds.DrawPawnGUIOverlay_DrawPawnGUIOverlay_Patch:Prefix"),
                        transpiler: new HarmonyMethod(HarmonyPatches.TranspilerTemplates.StubPushI4OneRetTranspiler)
                    );
                }
            }

            // Replacement for GetCurOccupant is defined in ModInterop_LoftBedBunkBeds.cs

            [HarmonyPatch(typeof(GenMapUI), nameof(GenMapUI.LabelDrawPosFor))]
            [HarmonyPatch(new Type[] { typeof(Thing), typeof(float) })]
            public class Patch_GenMapUI_LabelDrawPosFor {
                static void Prefix(ref Vector2 __result, Thing thing, ref float worldOffsetZ) {
                    if (thing is Pawn pawn) {
                        int? sleepingSlot;
                        Building_Bed bed = pawn.CurrentBed(out sleepingSlot);
                        if (bed != null && sleepingSlot != null && ModInteropDelegatesAndRefs.BunkBed_Utils_IsBunkBed(bed)) {
                            if (!bed.Rotation.IsHorizontal) {
                                switch (sleepingSlot) {
                                    case 0:
                                        worldOffsetZ += -0.7f;
                                        break;
                                    case 1:
                                        worldOffsetZ += -0.1f;
                                        break;
                                    case 2:
                                        worldOffsetZ += 0.55f;
                                        break;
                                }
                            } else {
                                worldOffsetZ += 0.4f;
                                switch (sleepingSlot) {
                                    case 0:
                                        worldOffsetZ += -0.2f;
                                        break;
                                    case 1:
                                        worldOffsetZ += 0.5f;
                                        break;
                                    case 2:
                                        worldOffsetZ += 1.2f;
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            // mmm don't like this mingling and mangling of edits
            // that's the story of UserInterface.cs too...
            [HarmonyPatch("BunkBeds.CompBunkBed", "DrawGUIOverlay")]
            public class Patch_CompBunkBed_DrawGUIOverlay {
                static bool Prefix(object __instance, ThingWithComps ___parent) {
                    Building_Bed building_Bed = ___parent as Building_Bed;

                    CompBuilding_BedXAttrs xAttrs = building_Bed.GetComp<CompBuilding_BedXAttrs>();
                    if (xAttrs == null) {
                        return true;
                    }

                    bool showCommunalGUIOverlayInsteadOfBlankUnderBed = BedOwnershipTools.Singleton.settings.showCommunalGUIOverlayInsteadOfBlankUnderBed;
                    bool hideDisplayStringForNonHumanlikeBeds = !building_Bed.def.building.bed_humanlike && BedOwnershipTools.Singleton.settings.hideGUIOverlayOnNonHumanlikeBeds;

                    // base.DrawGUIOverlay();

                    if (building_Bed.Medical || Find.CameraDriver.CurrentZoom != CameraZoomRange.Closest || !building_Bed.CompAssignableToPawn.PlayerCanSeeAssignments) {
                        return false;
                    }
                    Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
                    Color grey = new Color(0.5f, 0.5f, 0.5f, 1f);
                    List<Pawn> assignedPawns = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups ? xAttrs.assignedPawnsOverlay : building_Bed.CompAssignableToPawn.AssignedPawnsForReading;
                    Type Building_Bed_DrawGUIOverlay_Patch_guestBedType = ModInteropDelegatesAndRefs.Building_Bed_DrawGUIOverlay_Patch_guestBedType();
                    if (!building_Bed.ForPrisoners && !building_Bed.Medical && xAttrs.IsAssignedToCommunity) {
                        if (showCommunalGUIOverlayInsteadOfBlankUnderBed && !hideDisplayStringForNonHumanlikeBeds) {
                            GenMapUI.DrawThingLabel(building_Bed, "BedOwnershipTools.CommunalAbbrevBrackets".Translate(), defaultThingLabelColor);
                        }
                    } else if (!building_Bed.OwnersForReading.Any() && ((object)Building_Bed_DrawGUIOverlay_Patch_guestBedType == null || !Building_Bed_DrawGUIOverlay_Patch_guestBedType.IsAssignableFrom(___parent.def.thingClass))) {
                        GenMapUI.DrawThingLabel(building_Bed, "Unowned".Translate(), defaultThingLabelColor);
                    }
                    else if (building_Bed.OwnersForReading.Count == 1) {
                        Pawn pawn = building_Bed.OwnersForReading[0];
                        if ((!pawn.InBed() || pawn.CurrentBed() != building_Bed) && (!pawn.RaceProps.Animal || Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn))) {
                            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                                bool active = building_Bed.CompAssignableToPawn.AssignedPawnsForReading.Contains(pawn);
                                GenMapUI.DrawThingLabel(building_Bed, pawn.LabelShort, active ? defaultThingLabelColor : grey);
                            } else {
                                GenMapUI.DrawThingLabel(building_Bed, pawn.LabelShort, defaultThingLabelColor);
                            }
                        }
                    } else {
                        for (int i = 0; i < assignedPawns.Count; i++) {
                            Pawn pawn2 = assignedPawns[i];
                            if (!pawn2.InBed() || assignedPawns[i].CurrentBed() != building_Bed || !(pawn2.Position == building_Bed.Position)) {
                                if (pawn2.RaceProps.Animal && !Prefs.AnimalNameMode.ShouldDisplayAnimalName(pawn2)) {
                                    break;
                                }
                                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                                    bool active = building_Bed.CompAssignableToPawn.AssignedPawnsForReading.Contains(pawn2);
                                    GenMapUI.DrawThingLabel(ModInteropDelegatesAndRefs.CompBunkBed_GetMultiOwnersLabelScreenPosFor(__instance, i), pawn2.LabelShort, active ? defaultThingLabelColor : grey);
                                } else {
                                    GenMapUI.DrawThingLabel(ModInteropDelegatesAndRefs.CompBunkBed_GetMultiOwnersLabelScreenPosFor(__instance, i), pawn2.LabelShort, defaultThingLabelColor);
                                }
                            }
                        }
                    }

                    if (!building_Bed.ForPrisoners && !building_Bed.Medical) {
                        if (!xAttrs.IsAssignedToCommunity && !hideDisplayStringForNonHumanlikeBeds) {
                            xAttrs.DrawPinnedAGLabel();
                        }
                    }

                    return false;
                }
            }

        }
    }
}
