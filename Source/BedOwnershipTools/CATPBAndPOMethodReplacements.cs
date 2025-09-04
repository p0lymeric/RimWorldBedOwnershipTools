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
            if (pawnXAttrs == null) {
                return pawnXAttrs.assignmentGroupToOwnedBedMap.ContainsKey(bedXAttrs.MyAssignmentGroup);
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
            ClaimBedIfNotMedical(pawn, building_Bed);
            // building_Bed.NotifyRoomAssignedPawnsChanged();
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
            // Building_Bed ownedBed = pawnXAttrs.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup]; // need null check
            UnclaimBedDirected(pawn, bedXAttrs.MyAssignmentGroup);
            // ownedBed?.NotifyRoomAssignedPawnsChanged();
            if (uninstall && !bedXAttrs.uninstalledAssignedPawnsOverlay.Contains(pawn)) {
                bedXAttrs.uninstalledAssignedPawnsOverlay.Add(pawn);
            }
        }

        public static bool ClaimBedIfNotMedical(Pawn pawn, Building_Bed newBed) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            CompBuilding_BedXAttrs newBedXAttrs = newBed.GetComp<CompBuilding_BedXAttrs>();
            if (newBedXAttrs == null) {
                return false;
            }
            if (GameComponent_AssignmentGroupManager.Singleton.defaultAssignmentGroup == null) {
                // the AGM is set up during FinalizeInit when the mod is freshly added
                // if that's the case, any bed assignments that occur prior to this point must necessarily target the default group (which will be registered during FinalizeInit)
                // when the mod is already present in the save, AGM state will be loaded before PostLoadInit so there won't be a problem then
                Log.Warning("[BOT] Something tried to claim a bed but the AssigmentGroupManager hasn't been set up in this save yet. (This should be harmless if Bed Ownership Tools was newly added, in which case the claim will be placed in the default group.)");
                return false;
            }
            if (IsOwner(newBed, pawn) || newBed.Medical) {
                return false;
            }
            // no support for deathrest caskets
            if (ModsConfig.BiotechActive && newBed.def == ThingDefOf.DeathrestCasket) {
            //     UnclaimDeathrestCasket();
            //     newBed.CompAssignableToPawn.ForceAddPawn(pawn);
            //     AssignedDeathrestCasket = newBed;
            //     return true;
                return false;
            }
            UnclaimBedDirected(pawn, newBedXAttrs.MyAssignmentGroup);
            if (newBedXAttrs.assignedPawnsOverlay.Count == newBed.SleepingSlotsCount) {
                Pawn pawnToEvict = newBedXAttrs.assignedPawnsOverlay[newBedXAttrs.assignedPawnsOverlay.Count - 1];
                CompPawnXAttrs pawnToEvictXAttrs = pawnToEvict.GetComp<CompPawnXAttrs>();
                if (pawnToEvictXAttrs != null) {
                    UnclaimBedDirected(pawnToEvict, newBedXAttrs.MyAssignmentGroup);
                }
            }
            ForceAddPawn(newBed.CompAssignableToPawn, pawn); // newBed.CompAssignableToPawn.ForceAddPawn(pawn);
            pawnXAttrs.assignmentGroupToOwnedBedMap[newBedXAttrs.MyAssignmentGroup] = newBed; //OwnedBed = newBed;
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

        public static bool UnclaimBedAll(Pawn pawn) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            bool unassignedAtLeastOneBed = false;
            foreach(var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupToOwnedBedMap) {
                ForceRemovePawn(bed.CompAssignableToPawn, pawn);
                // pawnXAttrs.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
                unassignedAtLeastOneBed = true;
            }
            pawnXAttrs.assignmentGroupToOwnedBedMap.Clear();
            return unassignedAtLeastOneBed;
        }

        public static bool UnclaimBedDirected(Pawn pawn, AssignmentGroup assignmentGroup) {
            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
            if (pawnXAttrs == null) {
                return false;
            }
            if (pawnXAttrs.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed oldBed)) {
                ForceRemovePawn(oldBed.CompAssignableToPawn, pawn);
                pawnXAttrs.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
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
            // xattrs null checked by caller
            CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
            foreach (Pawn owner in bedXAttrs.assignedPawnsOverlay) {
                if (LovePartnerRelationUtility.LovePartnerRelationExists(sleeper, owner)) {
                    return true;
                }
            }
            return false;
        }

        public static bool BedOwnerWillShare(Building_Bed bed, Pawn sleeper, GuestStatus? guestStatus) {
            // xattrs null checked by caller
            CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
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
                    return false;
                }
            }
            return true;
        }
    }
}
