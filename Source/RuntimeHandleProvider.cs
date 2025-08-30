using System;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

// Tries to retrieve references to handles in foreign assemblies
// and reports whether they are valid for use

// We use runtime reference checks to implement interop with other mods,
// to avoid needing to link to them at compile time

namespace BedOwnershipTools {
    public class RuntimeHandleProvider {
        public bool modHospitalityLoadedForCompatPatching = false;
        public Type typeHospitalityBuilding_GuestBed = null;

        public RuntimeHandleProvider(ModSettingsImpl settings) {
            if (settings.enableHospitalityModCompatPatches) {
                typeHospitalityBuilding_GuestBed = Type.GetType("Hospitality.Building_GuestBed, Hospitality");
                if (typeHospitalityBuilding_GuestBed != null) {
                    modHospitalityLoadedForCompatPatching = true;
                }
            }
        }
    }
}
