using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;
using Verse.AI;

// Delegates and refs that belong to the base game and hence can be populated any time before use

namespace BedOwnershipTools {
    public static class DelegatesAndRefs {
        // Building_Bed.RemoveAllOwners()
        public delegate void MethodDelegate_Building_Bed_RemoveAllOwners(Building_Bed thiss, bool destroyed = false);
        public static MethodDelegate_Building_Bed_RemoveAllOwners Building_Bed_RemoveAllOwners =
            AccessTools.MethodDelegate<MethodDelegate_Building_Bed_RemoveAllOwners>(AccessTools.Method(typeof(Building_Bed), "RemoveAllOwners"));

        // Building_Bed.GetMultiOwnersLabelScreenPosFor()
        public delegate Vector3 MethodDelegate_Building_Bed_GetMultiOwnersLabelScreenPosFor(Building_Bed thiss, int slotIndex);
        public static MethodDelegate_Building_Bed_GetMultiOwnersLabelScreenPosFor Building_Bed_GetMultiOwnersLabelScreenPosFor =
            AccessTools.MethodDelegate<MethodDelegate_Building_Bed_GetMultiOwnersLabelScreenPosFor>(AccessTools.Method(typeof(Building_Bed), "GetMultiOwnersLabelScreenPosFor"));

        // CompAssignableToPawn.uninstalledAssignedPawns
        public static AccessTools.FieldRef<CompAssignableToPawn, List<Pawn>> CompAssignableToPawn_uninstalledAssignedPawns =
            AccessTools.FieldRefAccess<CompAssignableToPawn, List<Pawn>>("uninstalledAssignedPawns");

        // ThingWithComps.GetInspectString (non-virtual call)
        public delegate string MethodDelegateNonVirtual_ThingWithComps_GetInspectString(ThingWithComps thiss);
        public static MethodDelegateNonVirtual_ThingWithComps_GetInspectString NonVirtual_ThingWithComps_GetInspectString =
            AccessTools.MethodDelegate<MethodDelegateNonVirtual_ThingWithComps_GetInspectString>(AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.GetInspectString)), virtualCall: false);

        // Pawn_Ownership.intOwnedBed
        public static AccessTools.FieldRef<Pawn_Ownership, Building_Bed> Pawn_Ownership_intOwnedBed =
            AccessTools.FieldRefAccess<Pawn_Ownership, Building_Bed>("intOwnedBed");

        // Pawn_Ownership.AssignedDeathrestCasket (setter call)
        public delegate void MethodDelegatePropertySetter_Pawn_Ownership_AssignedDeathrestCasket(Pawn_Ownership thiss, Building_Bed value);
        public static MethodDelegatePropertySetter_Pawn_Ownership_AssignedDeathrestCasket Pawn_Ownership_AssignedDeathrestCasket_Set =
            AccessTools.MethodDelegate<MethodDelegatePropertySetter_Pawn_Ownership_AssignedDeathrestCasket>(AccessTools.PropertySetter(typeof(Pawn_Ownership), nameof(Pawn_Ownership.AssignedDeathrestCasket)));

        // Pawn_Ownership.pawn
        public static AccessTools.FieldRef<Pawn_Ownership, Pawn> Pawn_Ownership_pawn =
            AccessTools.FieldRefAccess<Pawn_Ownership, Pawn>("pawn");

        // Pawn_AgeTracker.pawn
        public static AccessTools.FieldRef<Pawn_AgeTracker, Pawn> Pawn_AgeTracker_pawn =
            AccessTools.FieldRefAccess<Pawn_AgeTracker, Pawn>("pawn");

        // Pawn_IdeoTracker.pawn
        public static AccessTools.FieldRef<Pawn_IdeoTracker, Pawn> Pawn_IdeoTracker_pawn =
            AccessTools.FieldRefAccess<Pawn_IdeoTracker, Pawn>("pawn");

        // JobDriver.curToil (getter call)
        public delegate Toil MethodDelegatePropertyGetter_JobDriver_CurToil(JobDriver thiss);
        public static MethodDelegatePropertyGetter_JobDriver_CurToil JobDriver_CurToil_Get =
            AccessTools.MethodDelegate<MethodDelegatePropertyGetter_JobDriver_CurToil>(AccessTools.PropertyGetter(typeof(JobDriver), "CurToil"));

        // Gene_Deathrest.AutoWakeCommandTex
        public static AccessTools.FieldRef<CachedTexture> Gene_Deathrest_AutoWakeCommandTex =
            AccessTools.StaticFieldRefAccess<CachedTexture>(AccessTools.Field(typeof(Gene_Deathrest), "AutoWakeCommandTex"));
    }
}
