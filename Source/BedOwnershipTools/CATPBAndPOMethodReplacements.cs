using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public static class CATPBAndPOMethodReplacements {
        public static bool AssignedAnything(CompAssignableToPawn thiss, Pawn pawn) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs != null) {
                return thiss switch {
                    CompAssignableToPawn_DeathrestCasket => pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.ContainsKey(bedXAttrs.MyAssignmentGroup),
                    _ => pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.ContainsKey(bedXAttrs.MyAssignmentGroup)
                };
            }
            return false;
        }
        public static void ForceAddPawn(CompAssignableToPawn thiss, Pawn pawn) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            if (!bedXAttrs.assignedPawnsOverlay.Contains(pawn))
            {
                bedXAttrs.assignedPawnsOverlay.Add(pawn);
            }
            SortAssignedPawns(thiss);
        }
        public static void ForceRemovePawn(CompAssignableToPawn thiss, Pawn pawn) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            if (bedXAttrs.assignedPawnsOverlay.Contains(pawn))
            {
                bedXAttrs.assignedPawnsOverlay.Remove(pawn);
            }
            bedXAttrs.uninstalledAssignedPawnsOverlay.Remove(pawn);
            SortAssignedPawns(thiss);
        }
        public static void SortAssignedPawns(CompAssignableToPawn thiss) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            bedXAttrs.assignedPawnsOverlay.RemoveAll((Pawn x) => x == null);
            bedXAttrs.assignedPawnsOverlay.SortBy((Pawn x) => x.thingIDNumber);
        }

        public static void PostSpawnSetup(CompAssignableToPawn thiss, bool respawningAfterLoad) {
            // base.PostSpawnSetup(respawningAfterLoad);
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            for (int num = bedXAttrs.uninstalledAssignedPawnsOverlay.Count - 1; num >= 0; num--) {
                Pawn pawn = bedXAttrs.uninstalledAssignedPawnsOverlay[num];
                if (CanSetUninstallAssignedPawn(thiss, pawn)) {
                    TryAssignPawn(thiss, pawn);
                }
            }
            bedXAttrs.uninstalledAssignedPawnsOverlay.Clear();
        }

        // need to override so we use the same overridden AssignedAnything
        public static bool CanSetUninstallAssignedPawn(CompAssignableToPawn thiss, Pawn pawn) {
            if (pawn != null && !AssignedAnything(thiss, pawn) && (bool)thiss.CanAssignTo(pawn)) {
                if (!pawn.IsPrisonerOfColony) {
                    return pawn.IsColonist;
                }
                return true;
            }
            return false;
        }

        public static void TryAssignPawn(CompAssignableToPawn thiss, Pawn pawn) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            Building_Bed building_Bed = (Building_Bed)thiss.parent;
            switch (thiss) {
                case CompAssignableToPawn_DeathrestCasket:
                    ClaimDeathrestCasket(pawn, building_Bed);
                    break;
                default:
                    ClaimBedIfNonMedical(pawn, building_Bed);
                    break;
            }
            // building_Bed.NotifyRoomAssignedPawnsChanged();
            // NOTE base game implementation doesn't call this for deathrest caskets
            bedXAttrs.uninstalledAssignedPawnsOverlay.Remove(pawn);
        }
        public static void TryUnassignPawn(CompAssignableToPawn thiss, Pawn pawn, bool sort = true, bool uninstall = false) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.parent.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return;
            }
            // Building_Bed ownedBed = pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup]; // need null check
            switch (thiss) {
                case CompAssignableToPawn_DeathrestCasket:
                    UnclaimDeathrestCasketDirected(pawn, bedXAttrs.MyAssignmentGroup);
                    break;
                default:
                    UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                    break;
            }
            // ownedBed?.NotifyRoomAssignedPawnsChanged();
            // NOTE base game implementation doesn't call this for deathrest caskets
            if (uninstall && !bedXAttrs.uninstalledAssignedPawnsOverlay.Contains(pawn)) {
                bedXAttrs.uninstalledAssignedPawnsOverlay.Add(pawn);
            }
        }

        public static bool ClaimBedIfNonMedical(Pawn pawn, Building_Bed newBed) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            CompBuilding_BedXAttrs newBedXAttrs = newBed.GetComp<CompBuilding_BedXAttrs>();
            if (newBedXAttrs == null) {
                return false;
            }
            if (GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.defaultAssignmentGroup == null) {
                // When the mod is freshly added, AGMCompartment_AssignmentGroups state is invalid until FinalizeInit.
                // If that is the case when the function is called, we return and trust that the claim will be handled during FinalizeInit.
                // When the mod is already present in the save, AGM state will be loaded before PostLoadInit, so the claim would would be processed normally.
                // Hospitality is an example of a mod that can claim beds before FinalizeInit.
                Log.Warning($"[BOT] A Pawn ({pawn.Label}) tried to claim a bed but the AssigmentGroupManager hasn't been set up in this save yet. (This is harmless if Bed Ownership Tools was newly added, in which case the claim will be placed in the default group.)");
                return false;
            }
            if (IsOwner(newBed, pawn) || newBed.Medical) {
                return false;
            }
            if (IsDefOfDeathrestCasket(newBed.def)) {
                for (int num = newBedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                    Pawn pawnToEvict = newBedXAttrs.assignedPawnsOverlay[num];
                    CompPawnXAttrs pawnToEvictXAttrs = pawnToEvict.GetComp<CompPawnXAttrs>();
                    if (pawnToEvictXAttrs != null) {
                        UnclaimDeathrestCasketDirected(pawnToEvict, newBedXAttrs.MyAssignmentGroup);
                    }
                }
                UnclaimDeathrestCasketDirected(pawn, newBedXAttrs.MyAssignmentGroup);
                ForceAddPawn(newBed.CompAssignableToPawn, pawn);
                pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap[newBedXAttrs.MyAssignmentGroup] = newBed;
                return true;
            }
            UnclaimBedDirected(pawn, newBedXAttrs.MyAssignmentGroup);
            if (newBedXAttrs.assignedPawnsOverlay.Count == newBed.SleepingSlotsCount) {
                Pawn pawnToEvict = newBedXAttrs.assignedPawnsOverlay[newBedXAttrs.assignedPawnsOverlay.Count - 1];
                CompPawnXAttrs pawnToEvictXAttrs = pawnToEvict.GetComp<CompPawnXAttrs>();
                if (pawnToEvictXAttrs != null) {
                    UnclaimBedDirected(pawnToEvict, newBedXAttrs.MyAssignmentGroup);
                }
            }
            ForceAddPawn(newBed.CompAssignableToPawn, pawn);
            pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap[newBedXAttrs.MyAssignmentGroup] = newBed;
            // if (pawn.IsFreeman && newBed.CompAssignableToPawn.IdeoligionForbids(pawn)) {
            //     Log.Error("Assigned " + pawn.GetUniqueLoadID() + " to a bed against their or occupants' ideo.");
            // }
            // tautologically unreachable by return if false above
            // if (newBed.Medical) {
            //     Log.Warning(pawn.LabelCap + " claimed medical bed.");
            //     UnclaimBedDirected(pawn, newBedRDA.innerParentXAttrs.MyAssignmentGroup);
            // }
            return true;
        }

        public static bool ClaimDeathrestCasket(Pawn pawn, Building_Bed deathrestCasket) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            CompBuilding_BedXAttrs bedXAttrs = deathrestCasket.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            if (GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.defaultAssignmentGroup == null) {
                Log.Warning($"[BOT] A Pawn ({pawn.Label}) tried to claim a deathrest casket but the AssigmentGroupManager hasn't been set up in this save yet. (This is harmless if Bed Ownership Tools was newly added, in which case the claim will be placed in the default group.)");
                return false;
            }
            if (!ModsConfig.BiotechActive) {
                return false;
            }
            if (bedXAttrs.assignedPawnsOverlay.Contains(pawn))
            {
                return false;
            }
            UnclaimDeathrestCasketDirected(pawn, bedXAttrs.MyAssignmentGroup);
            if (bedXAttrs.assignedPawnsOverlay.Count > 0) {
                List<Pawn> pawnsToRemove = new();
                foreach (Pawn oldPawn in bedXAttrs.assignedPawnsOverlay) {
                    pawnsToRemove.Add(oldPawn);
                }
                foreach (Pawn oldPawn in pawnsToRemove) {
                    UnclaimDeathrestCasketDirected(oldPawn, bedXAttrs.MyAssignmentGroup);
                }
            }
            ForceAddPawn(deathrestCasket.CompAssignableToPawn, pawn);
            pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap[bedXAttrs.MyAssignmentGroup] = deathrestCasket;
            return true;
        }

        public static bool UnclaimBedAll(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            bool unassignedAtLeastOneBed = false;
            foreach(var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                ForceRemovePawn(bed.CompAssignableToPawn, pawn);
                // pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
                unassignedAtLeastOneBed = true;
            }
            pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Clear();
            return unassignedAtLeastOneBed;
        }

        public static bool UnclaimBedDirected(Pawn pawn, AssignmentGroup assignmentGroup) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed oldBed)) {
                ForceRemovePawn(oldBed.CompAssignableToPawn, pawn);
                pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
                return true;
            }
            return false;
        }

        public static bool UnclaimDeathrestCasketAll(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            bool unassignedAtLeastOneBed = false;
            foreach(var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap) {
                ForceRemovePawn(bed.CompAssignableToPawn, pawn);
                // pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.Remove(assignmentGroup);
                unassignedAtLeastOneBed = true;
            }
            pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.Clear();
            return unassignedAtLeastOneBed;
        }

        public static bool UnclaimDeathrestCasketDirected(Pawn pawn, AssignmentGroup assignmentGroup) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            if (!ModsConfig.BiotechActive) {
                return false;
            }
            if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(assignmentGroup, out Building_Bed oldBed)) {
                ForceRemovePawn(oldBed.CompAssignableToPawn, pawn);
                pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.Remove(assignmentGroup);
                return true;
            }
            return false;
        }

        public static bool IsOwner(Building_Bed thiss, Pawn p) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            int num = bedXAttrs.assignedPawnsOverlay.IndexOf(p);
            if (num >= 0) {
                return true;
            }
            return false;
        }

        public static void RemoveAllOwners(Building_Bed thiss, bool destroyed = false) {
            CompBuilding_BedXAttrs bedXAttrs = thiss.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return;
            }
            for (int num = bedXAttrs.assignedPawnsOverlay.Count - 1; num >= 0; num--) {
                Pawn pawn = bedXAttrs.assignedPawnsOverlay[num];
                UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
                // string key = "MessageBedLostAssignment";
                // if (destroyed) {
                //     key = "MessageBedDestroyed";
                // }
                // Messages.Message(key.Translate(def, pawn), new LookTargets(this, pawn), MessageTypeDefOf.CautionInput, historical: false);
            }
        }

        public static bool IsAnyOwnerLovePartnerOf(Building_Bed bed, Pawn sleeper) {
            CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            foreach (Pawn owner in bedXAttrs.assignedPawnsOverlay) {
                if (LovePartnerRelationUtility.LovePartnerRelationExists(sleeper, owner)) {
                    return true;
                }
            }
            return false;
        }

        public static bool BedOwnerWillShare(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus) {
            CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            if (!bedXAttrs.assignedPawnsOverlay.Any()) {
                return true;
            }
            if (sleeper.IsPrisoner || guestStatus == GuestStatus.Prisoner || sleeper.IsSlave || guestStatus == GuestStatus.Slave) {
                if (!bed.AnyUnownedSleepingSlot) {
                    return false;
                }
            } else {
                if (!bedXAttrs.assignedPawnsOverlay.Any()) {
                    return false;
                }
                if (!IsAnyOwnerLovePartnerOf(bed, sleeper)) {
                    if (BedOwnershipTools.Singleton.runtimeHandles.modBunkBedsLoadedForCompatPatching) {
                        // so that Pawns will self-assign to bunk beds with non-lovers
                        return HarmonyPatches.ModCompatPatches_BunkBeds.RemoteCall_IsBunkBed(bed);
                    } else {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool IsDefOfDeathrestCasket(ThingDef thingDef) {
            return ModsConfig.BiotechActive && thingDef == ThingDefOf.DeathrestCasket;
        }

        public static bool HasFreeSlot(Building_Bed bed) {
            CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
            if (bedXAttrs == null) {
                return false;
            }
            return bedXAttrs.assignedPawnsOverlay.Count < bed.CompAssignableToPawn.Props.maxAssignedPawnsCount;
        }
    }
}
