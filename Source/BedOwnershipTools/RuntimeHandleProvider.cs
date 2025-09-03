using System;
using RimWorld;
using Verse;

// Tries to retrieve references to handles in foreign assemblies
// and reports whether they are valid for use

// We use runtime reference checks to implement interop with other mods,
// to avoid needing to link to them at compile time

namespace BedOwnershipTools {
    public class RuntimeHandleProvider {
        public bool modHospitalityLoadedForCompatPatching = false;
        public Type typeHospitalityBuilding_GuestBed = null;

        public bool modOneBedToSleepWithAllLoadedForCompatPatching = false;
        public Type typeOneBedToSleepWithAll_CompPolygamyMode = null;
        public Type typeOneBedToSleepWithAll_PolygamyModeUtility = null;

        public RuntimeHandleProvider(ModSettingsImpl settings) {
            if (settings.enableHospitalityModCompatPatches) {
                typeHospitalityBuilding_GuestBed = Type.GetType("Hospitality.Building_GuestBed, Hospitality");
                if (typeHospitalityBuilding_GuestBed != null) {
                    modHospitalityLoadedForCompatPatching = true;
                }
            }
            if (settings.enableOneBedToSleepWithAllModCompatPatches) {
                typeOneBedToSleepWithAll_CompPolygamyMode = Type.GetType("OneBedToSleepWithAll.CompPolygamyMode, OneBedToSleepWithAll");
                typeOneBedToSleepWithAll_PolygamyModeUtility = Type.GetType("OneBedToSleepWithAll.PolygamyModeUtility, OneBedToSleepWithAll");
                if (typeOneBedToSleepWithAll_CompPolygamyMode != null && typeOneBedToSleepWithAll_PolygamyModeUtility != null) {
                    modOneBedToSleepWithAllLoadedForCompatPatching = true;
                }
            }
        }
    }
}
