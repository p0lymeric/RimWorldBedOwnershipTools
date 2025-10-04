using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches

namespace BedOwnershipTools {
    public class ModInterop_Hospitality : ModInterop {
        public Assembly assemblyHospitality;
        public Type typeHospitalityBuilding_GuestBed;

        public ModInterop_Hospitality(bool enabled) : base(enabled) {
            if (enabled) {
                this.assemblyHospitality = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assy => assy.GetName().Name == "Hospitality");
                if (assemblyHospitality != null) {
                    this.detected = true;
                    this.typeHospitalityBuilding_GuestBed = assemblyHospitality.GetType("Hospitality.Building_GuestBed");
                    this.qualified =
                        this.typeHospitalityBuilding_GuestBed != null;
                }
            }
        }

        public override void ApplyHarmonyPatches(Harmony harmony) {
            if (this.qualified) {
                HarmonyPatches.PatchInClassShallow(harmony, typeof(ModInteropHarmonyPatches));
                this.active = true;
            }
        }

        public override void Notify_AGMCompartment_HarmonyPatchState_Constructed() {
        }

        public bool RemoteCall_IsGuestBed(ThingWithComps thing) {
            if (this.active) {
                return this.typeHospitalityBuilding_GuestBed.IsInstanceOfType(thing);
            }
            return false;
        }

        public static class ModInteropHarmonyPatches {
            [HarmonyPatch("Hospitality.Patches.Toils_LayDown_Patch+ApplyBedThoughts", "AddedBedIsOwned")]
            public class DoublePatch_Toils_LayDown_ApplyBedThoughts_AddedBedIsOwned {
                // Hospitality prefixes ApplyBedThoughts and detours out immediately afterwards
                // But they introduce a similar patch helper as us in CommunalBeds.cs
                // We'll add our check as an OR'd condition to their patch helper
                static void Postfix(ref bool __result, Pawn pawn, Building_Bed buildingBed) {
                    CompBuilding_BedXAttrs bedXAttrs = buildingBed.GetComp<CompBuilding_BedXAttrs>();
                    if (bedXAttrs == null) {
                        return;
                    }
                    __result = __result || bedXAttrs.IsAssignedToCommunity;
                }
            }
        }
    }
}
