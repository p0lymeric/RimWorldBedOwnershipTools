using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class AGMCompartment_AssignmentGroups : AGMCompartment {
        // All assignment groups indexed by priority. Pawns can own one building of a kind per assignment group.
        public List<AssignmentGroup> allAssignmentGroupsByPriority = null;

        // The default assignment group (to which a special symbol like null can map)
        // Currently cannot be changed.
        // We store the default assignment group because we want to stabilize the data model for future.
        public AssignmentGroup defaultAssignmentGroup = null;

        // Stores the last persisted value of enableBedAssignmentGroups so that a toggle during
        // Notify_WriteSettings would cause bed assignments to be reset
        public bool isSubsystemActive = false;

        // We will restrict custom assignment group IDs to be handed out in the range [1, MAXIMUM_NONDEFAULT_GROUPS]
        // TODO mod setting
        public const int MAXIMUM_NONDEFAULT_GROUPS = 9;

        public AGMCompartment_AssignmentGroups(Game game, GameComponent_AssignmentGroupManager parent) : base(game, parent) {}

        public void Notify_WriteSettings() {
            if (isSubsystemActive ^ BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                foreach (CompPawnXAttrs pawnXAttrs in parent.compPawnXAttrsRegistry) {
                    pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Clear();
                    pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.Clear();
                }
                foreach (CompBuilding_BedXAttrs bedXAttrs in parent.compBuilding_BedXAttrsRegistry) {
                    Building_Bed bed = (Building_Bed)bedXAttrs.parent;
                    bedXAttrs.MyAssignmentGroup = defaultAssignmentGroup;
                    bedXAttrs.assignedPawnsOverlay.Clear();
                    bedXAttrs.uninstalledAssignedPawnsOverlay.Clear();
                }
                FinalizeInit();
            }
        }

        public void FinalizeInit() {
            isSubsystemActive = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;

            // allAssignmentGroupsByPriority == null occurs in a new game or when the mod is newly added to the save
            // allAssignmentGroupsByPriority.Count == 0 should never happen but is checked out of prudence
            if (allAssignmentGroupsByPriority == null || allAssignmentGroupsByPriority.Count == 0) {
                allAssignmentGroupsByPriority = new List<AssignmentGroup>();
                // Starter custom groups created on first init
                defaultAssignmentGroup = new AssignmentGroup(0, "BedOwnershipTools.Default".Translate(), false);
                allAssignmentGroupsByPriority.Add(defaultAssignmentGroup);
                allAssignmentGroupsByPriority.Add(new AssignmentGroup(1, "BedOwnershipTools.Home".Translate(), true));
                allAssignmentGroupsByPriority.Add(new AssignmentGroup(2, "BedOwnershipTools.Ship".Translate(), true));
            }

            // Should never happen since we don't allow deletion of the default assignment group
            if (defaultAssignmentGroup == null) {
                defaultAssignmentGroup = allAssignmentGroupsByPriority[0];
            }

            // None of the following fixups are necessary if the subsystem is deactivated as:
            // 1) on a save reload past a disable, relevant structures will not be loaded and will be initialized as empty
            // 2) on a settings toggle during the game, Notify_WriteSettings will clear the relevant structures
            if (isSubsystemActive) {
                foreach (CompPawnXAttrs pawnXAttrs in parent.compPawnXAttrsRegistry) {
                    Pawn pawn = pawnXAttrs.parentPawn;
                    // The game does not always call destroy routines on "practically" destroyed Things
                    // e.g. abandoning a map tile will not trigger ownership unassignment routines
                    // Null refs are saved and then removed after the next load, following the game's actual handling
                    List<AssignmentGroup> assignmentGroupsToRemove = new List<AssignmentGroup>();
                    foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap) {
                        if (bed == null) {
                            Log.Warning($"[BOT] A Pawn ({pawn.Label}) has a null bed reference stored in its overlay ownership field. This may occur if you've recently abandoned a settlement before your last save.");
                            assignmentGroupsToRemove.Add(assignmentGroup);
                        }
                    }
                    foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                        pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
                    }

                    assignmentGroupsToRemove.Clear();
                    foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap) {
                        if (bed == null) {
                            Log.Warning($"[BOT] A Pawn ({pawn.Label}) has a null deathrest casket reference stored in its overlay ownership field. This may occur if you've recently abandoned a settlement before your last save.");
                            assignmentGroupsToRemove.Add(assignmentGroup);
                        }
                    }
                    foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                        pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.Remove(assignmentGroup);
                    }

                    Building_Bed ownedBed = pawn.ownership.OwnedBed;
                    if (ownedBed != null) {
                        CompBuilding_BedXAttrs bedXAttrs = ownedBed.GetComp<CompBuilding_BedXAttrs>();
                        if (bedXAttrs == null) {
                            // Should never happen since we patch CompBuilding_BedXAttrs to be added onto Building_Bed instances
                            Log.Warning($"[BOT] A Pawn ({pawn.Label}) owns a bed ({ownedBed.GetUniqueLoadID()}) in its internal ownership list that doesn't have a CompBuilding_BedXAttrs component.");
                        } else if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(bedXAttrs.MyAssignmentGroup, out Building_Bed otherBed)) {
                            // Should never happen since we ensure that if a Pawn owns a bed in the overlay,
                            // its internally assigned bed will either be null or one of the beds in the overlay
                            if (ownedBed != otherBed) {
                                Log.Warning($"[BOT] A Pawn has inconsistent beds stored in internal ({ownedBed.GetUniqueLoadID()}) and overlay ({otherBed.GetUniqueLoadID()}) ownership fields.");
                            }
                        } else {
                            // Setting assignmentGroupToOwnedBedMap is technically redundant with the TryAssignPawn call
                            // (however, corrects save data from mod versions before and not including 1.0.0 RC where some logic did not apply to prisoners or babies, "tri86" for repro)
                            pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup] = ownedBed;
                            // Needed to initialize overlays on existing saves where the mod is newly added, or when the subsystem is activated via settings toggle
                            CATPBAndPOMethodReplacements.TryAssignPawn(ownedBed.CompAssignableToPawn, pawn);
                        }
                    }

                    ownedBed = pawn.ownership.AssignedDeathrestCasket;
                    if (ownedBed != null) {
                        CompBuilding_BedXAttrs bedXAttrs = ownedBed.GetComp<CompBuilding_BedXAttrs>();
                        if (bedXAttrs == null) {
                            // Should never happen since we patch CompBuilding_BedXAttrs to be added onto Building_Bed instances
                            Log.Warning($"[BOT] A Pawn ({pawn.Label}) owns a deathrest casket ({ownedBed.GetUniqueLoadID()}) in its internal ownership list that doesn't have a CompBuilding_BedXAttrs component.");
                        } else if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(bedXAttrs.MyAssignmentGroup, out Building_Bed otherBed)) {
                            // Should never happen since we ensure that if a Pawn owns a bed in the overlay,
                            // its internally assigned bed will either be null or one of the beds in the overlay
                            if (ownedBed != otherBed) {
                                Log.Warning($"[BOT] A Pawn has inconsistent deathrest caskets stored in internal ({ownedBed.GetUniqueLoadID()}) and overlay ({otherBed.GetUniqueLoadID()}) ownership fields.");
                            }
                        } else {
                            // Setting assignmentGroupToAssignedDeathrestCasketMap is technically redundant with the TryAssignPawn call
                            // (however, corrects save data from mod versions before and not including 1.0.0 RC where some logic did not apply to prisoners or babies, "tri86" for repro)
                            pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap[bedXAttrs.MyAssignmentGroup] = ownedBed;
                            // Needed to initialize overlays on existing saves where the mod is newly added, or when the subsystem is activated via settings toggle
                            CATPBAndPOMethodReplacements.TryAssignPawn(ownedBed.CompAssignableToPawn, pawn);
                        }
                    }
                }
            }

            if (isSubsystemActive) {
                foreach (CompBuilding_BedXAttrs bedXAttrs in parent.compBuilding_BedXAttrsRegistry) {
                    Building_Bed bed = (Building_Bed)bedXAttrs.parent;
                    CompAssignableToPawn catp = bed.GetComp<CompAssignableToPawn>();
                    if (catp == null) {
                        // Should never happen since Building_Bed's base implementation fundamentally depends on having a CompAssignableToPawn instance
                        Log.Warning($"[BOT] A bed ({bed.GetUniqueLoadID()}) doesn't have a CompAssignableToPawn component.");
                    } else {
                        // Needed to initialize overlays on existing saves where the mod is newly added, or when the subsystem is activated via settings toggle
                        bedXAttrs.uninstalledAssignedPawnsOverlay.AddRange(DelegatesAndRefs.CompAssignableToPawn_uninstalledAssignedPawns(catp).Except(bedXAttrs.uninstalledAssignedPawnsOverlay));
                        // assignedPawnsOverlay is initialized by save data load or by calling TryAssignPawn through the compPawnXAttrsRegistry loop above
                        foreach (Pawn pawn in bed.CompAssignableToPawn.AssignedPawnsForReading) {
                            if (!bedXAttrs.assignedPawnsOverlay.Contains(pawn)) {
                                // Should never happen because we ensure that the overlay is a superset of the internal ownership list
                                Log.Warning($"[BOT] A bed ({bed.GetUniqueLoadID()}) has a Pawn ({pawn.Label}) in its internal ownership list but not its overlay list.");
                            }
                        }
                        // Remove any pawns from assignedPawnsOverlay who don't have the bed assigned in their overlay assignedPawns tracker
                        if (CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                            List<Pawn> pawnsToRemove = new List<Pawn>();
                            foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay) {
                                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                                if (pawnXAttrs == null) {
                                    continue;
                                }
                                if (!pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.ContainsKey(bedXAttrs.MyAssignmentGroup) || pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap[bedXAttrs.MyAssignmentGroup] != bed) {
                                    // (corrects save data from mod versions before and not including 1.0.0 RC where some logic did not apply to babies becoming children, "tri88" for repro)
                                    // sometimes occurs with Hospitality too--seems to be harmless, "tri96" for repro
                                    Log.Warning($"[BOT] A deathrest casket ({bed.GetUniqueLoadID()}) has a Pawn ({pawn.Label}) stored in its overlay ownership field, but that Pawn doesn't own it.");
                                    pawnsToRemove.Add(pawn);
                                }
                            }
                            foreach (Pawn pawn in pawnsToRemove) {
                                bedXAttrs.assignedPawnsOverlay.Remove(pawn);
                            }
                        } else {
                            List<Pawn> pawnsToRemove = new List<Pawn>();
                            foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay) {
                                CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                                if (pawnXAttrs == null) {
                                    continue;
                                }
                                if (!pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.ContainsKey(bedXAttrs.MyAssignmentGroup) || pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup] != bed) {
                                    // (corrects save data from mod versions before and not including 1.0.0 RC where some logic did not apply to babies becoming children, "tri88" for repro)
                                    // sometimes occurs with Hospitality too--seems to be harmless, "tri96" for repro
                                    Log.Warning($"[BOT] A bed ({bed.GetUniqueLoadID()}) has a Pawn ({pawn.Label}) stored in its overlay ownership field, but that Pawn doesn't own it.");
                                    pawnsToRemove.Add(pawn);
                                }
                            }
                            foreach (Pawn pawn in pawnsToRemove) {
                                bedXAttrs.assignedPawnsOverlay.Remove(pawn);
                            }
                        }
                    }
                }
            }
        }

        public void ExchangeByIdx(int idx1, int idx2) {
            (allAssignmentGroupsByPriority[idx1], allAssignmentGroupsByPriority[idx2]) = (allAssignmentGroupsByPriority[idx2], allAssignmentGroupsByPriority[idx1]);
        }

        public void DeleteByIdx(int idx) {
            if (allAssignmentGroupsByPriority[idx] == defaultAssignmentGroup) {
                return;
            }
            UnlinkAllRefsTo(allAssignmentGroupsByPriority[idx]);
            allAssignmentGroupsByPriority.RemoveAt(idx);
        }

        public void UnlinkAllRefsTo(AssignmentGroup assignmentGroup) {
            foreach (CompPawnXAttrs pawnXAttrs in parent.compPawnXAttrsRegistry) {
                if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                    // to generate the message that a pawn has become unlinked from a bed
                    DelegatesAndRefs.Building_Bed_RemoveAllOwners(bed, false);
                }
                if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToAssignedDeathrestCasketMap.TryGetValue(assignmentGroup, out Building_Bed bed2)) {
                    CATPBAndPOMethodReplacements.UnclaimDeathrestCasketDirected(pawnXAttrs.parentPawn, assignmentGroup);
                }
            }
            foreach (CompBuilding_BedXAttrs bedXAttrs in parent.compBuilding_BedXAttrsRegistry) {
                if (bedXAttrs.MyAssignmentGroup == assignmentGroup) {
                    bedXAttrs.MyAssignmentGroup = defaultAssignmentGroup;
                    // should've been cleared by RemoveAllOwners above
                    bedXAttrs.assignedPawnsOverlay.Clear();
                    // not cleared so do that
                    bedXAttrs.uninstalledAssignedPawnsOverlay.Clear();
                }
            }
        }

        public AssignmentGroup NewAtEnd() {
            if (allAssignmentGroupsByPriority.Count > MAXIMUM_NONDEFAULT_GROUPS) { // including default so > instead of >=
                return null;
            }
            // Prioritize filling lower IDs
            List<AssignmentGroup> tmpAllAssignmentGroupsByIdx = allAssignmentGroupsByPriority.OrderBy(x => x.id).ToList();
            int newId = 0;
            foreach (AssignmentGroup assignmentGroup in tmpAllAssignmentGroupsByIdx) {
                if(newId == assignmentGroup.id) {
                    newId++;
                } else {
                    break;
                }
            }
            // Name is not used for keying by code and can be renamed by the user to be non-unique,
            // but it is nice to have an assured unique name on creation
            AssignmentGroup newAG = new AssignmentGroup(newId, "BedOwnershipTools.Untitled".Translate() + " " + (newId + 1).ToString(), true);
            allAssignmentGroupsByPriority.Add(newAG);
            return newAG;
        }

        public void ShallowExposeData() {
            Scribe_Collections.Look(ref this.allAssignmentGroupsByPriority, "allAssignmentGroupsByPriority", LookMode.Deep);
            Scribe_References.Look(ref this.defaultAssignmentGroup, "defaultAssignmentGroup");
        }
    }
}
