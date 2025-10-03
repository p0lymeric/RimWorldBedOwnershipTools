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
        public Type typeHospitalityBuilding_GuestBed;

        public bool modOneBedToSleepWithAllLoadedForCompatPatching = false;
        public Type typeOneBedToSleepWithAll_CompPolygamyMode;
        public Type typeOneBedToSleepWithAll_PolygamyModeUtility;

        public bool modLoftBedLoadedForCompatPatching = false;
        public Type typeLoftBed_Building_LoftBed;

        public bool modBunkBedsLoadedForCompatPatching = false;
        public Type typeBunkBeds_Utils;
        public Type typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch;
        public Type typeBunkBeds_CompBunkBed;

        public bool modMultiFloorsLoadedForCompatPatching = false;
        public Type typeMultiFloors_StairPathFinderUtility;
        public Type typeMultiFloors_LevelUtility;

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
            if (settings.enableLoftBedModCompatPatches) {
                typeLoftBed_Building_LoftBed = Type.GetType("Nekoemi.LoftBed.Building_LoftBed, LoftBed");
                if (typeLoftBed_Building_LoftBed != null) {
                    modLoftBedLoadedForCompatPatching = true;
                }
            }
            if (settings.enableBunkBedsModCompatPatches) {
                typeBunkBeds_Utils = Type.GetType("BunkBeds.Utils, BunkBeds");
                typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch = Type.GetType("BunkBeds.Building_Bed_DrawGUIOverlay_Patch, BunkBeds");
                typeBunkBeds_CompBunkBed = Type.GetType("BunkBeds.CompBunkBed, BunkBeds");
                if (typeBunkBeds_Utils != null && typeBunkBeds_Building_Bed_DrawGUIOverlay_Patch != null && typeBunkBeds_CompBunkBed != null) {
                    modBunkBedsLoadedForCompatPatching = true;
                }
            }
            if (settings.enableMultiFloorsModCompatPatches) {
                typeMultiFloors_StairPathFinderUtility = Type.GetType("MultiFloors.StairPathFinderUtility, MultiFloors");
                typeMultiFloors_LevelUtility = Type.GetType("MultiFloors.LevelUtility, MultiFloors");
                if (typeMultiFloors_StairPathFinderUtility != null && typeMultiFloors_LevelUtility != null) {
                    modMultiFloorsLoadedForCompatPatching = true;
                }
            }
        }
    }
}
