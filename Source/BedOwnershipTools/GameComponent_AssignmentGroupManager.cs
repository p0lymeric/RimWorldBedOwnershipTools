using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public class GameComponent_AssignmentGroupManager : GameComponent {
        public static GameComponent_AssignmentGroupManager Singleton;

        // All assignment groups indexed by priority. Pawns can own one building of a kind per assignment group.
        public List<AssignmentGroup> allAssignmentGroupsByPriority = null;

        // The default assignment group (to which a special symbol like null can map)
        // Currently cannot be changed.
        // We store the default assignment group because we want to stabilize the data model for future.
        public AssignmentGroup defaultAssignmentGroup = null;

        // Used to store the last persisted value of enableBedAssignmentGroups so that a toggle during
        // Notify_WriteSettings would cause bed assignments to be reset
        public bool isTheSystemActive = false;

        // Used to track all pawns and beds so that assignment groups can be deleted
        public HashSet<CompPawnXAttrs> compPawnXAttrsRegistry = new HashSet<CompPawnXAttrs>();
        public HashSet<CompBuilding_BedXAttrs> compBuilding_BedXAttrsRegistry = new HashSet<CompBuilding_BedXAttrs>();

        // We will restrict custom assignment group IDs to be handed out in the range [1, MAXIMUM_NONDEFAULT_GROUPS]
        // TODO mod setting
        public const int MAXIMUM_NONDEFAULT_GROUPS = 9;

        // Observed execution order
        // New game
        // GameComponent ctor ->
        // Pawn ctor ->
        // GC FinalizeInit ->
        // GC StartedNewGame
        //
        // Loaded game (mod newly added)
        // GameComponent ctor ->
        // (Pawn ctor -> Pawn PostExposeDataLoadingVars) ->
        // Pawn PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataPostLoadInit ->
        // GC FinalizeInit ->
        // GC LoadedGame
        //
        // Loaded game (mod was previously initialized)
        // GameComponent ctor ->
        // GC ExposeDataLoadingVars ->
        // (Pawn ctor -> Pawn PostExposeDataLoadingVars) ->
        // GC PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataPostLoadInit ->
        // GC PostExposeDataPostLoadInit ->
        // GC FinalizeInit ->
        // GC LoadedGame

        public GameComponent_AssignmentGroupManager(Game game) {
            Singleton = this;
        }

        public void Notify_WriteSettings() {
            if (isTheSystemActive && !BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                // TODO flush overlay to internal
                HardDeinit();
                FinalizeInit();
            }
            if (!isTheSystemActive && BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                HardDeinit();
                FinalizeInit();
            }
        }

        public void HardDeinit() {
            foreach (CompPawnXAttrs pawnXAttrs in compPawnXAttrsRegistry) {
                Pawn pawn = (Pawn)pawnXAttrs.parent;
                pawnXAttrs.assignmentGroupToOwnedBedMap.Clear();
            }
            foreach (CompBuilding_BedXAttrs bedXAttrs in compBuilding_BedXAttrsRegistry) {
                Building_Bed bed = (Building_Bed)bedXAttrs.parent;
                bedXAttrs.MyAssignmentGroup = defaultAssignmentGroup;
                bedXAttrs.assignedPawnsOverlay.Clear();
                bedXAttrs.uninstalledAssignedPawnsOverlay.Clear();
            }
        }

        public override void FinalizeInit() {
            isTheSystemActive = BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups;
            // allAssignmentGroupsByPriority.Count == 0 shouldn't ever be triggered unless save is corrupted
            if (allAssignmentGroupsByPriority == null || allAssignmentGroupsByPriority.Count == 0) {
                allAssignmentGroupsByPriority = new List<AssignmentGroup>();
                // These are starter custom groups created on first init
                defaultAssignmentGroup = new AssignmentGroup(0, "BedOwnershipTools.Default".Translate(), false);
                allAssignmentGroupsByPriority.Add(defaultAssignmentGroup);
                allAssignmentGroupsByPriority.Add(new AssignmentGroup(1, "BedOwnershipTools.Home".Translate(), true));
                allAssignmentGroupsByPriority.Add(new AssignmentGroup(2, "BedOwnershipTools.Ship".Translate(), true));
            }

            // shouldn't ever be triggered unless save is corrupted
            if (defaultAssignmentGroup == null) {
                defaultAssignmentGroup = allAssignmentGroupsByPriority[0];
            }

            // could also use PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
            foreach (CompPawnXAttrs pawnXAttrs in compPawnXAttrsRegistry) {
                Pawn pawn = (Pawn)pawnXAttrs.parent;
                Building_Bed ownedBed = pawn.ownership.OwnedBed;
                if (ownedBed != null) {
                    CompBuilding_BedXAttrs bedXAttrs = ownedBed.GetComp<CompBuilding_BedXAttrs>();
                    if (pawnXAttrs.assignmentGroupToOwnedBedMap.TryGetValue(bedXAttrs.MyAssignmentGroup, out Building_Bed otherBed)) {
                        // just out of paranoia
                        if (ownedBed != otherBed) {
                            Log.Warning("[BOT] Pawn has inconsistent beds stored in internal and overlay ownership fields.");
                        }
                    } else {
                        if (isTheSystemActive) {
                            // needed to reconcile upon first introducing mod to a save
                            // should technically be redundant with the TryAssignPawn call, but in the case of a desync,
                            // TryAssignPawn won't resynchronize the map because of an IsOwner check that only checks the bed's overlay
                            pawnXAttrs.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup] = ownedBed;
                            // needed during same save reinit
                            CATPBAndPOMethodReplacements.TryAssignPawn(ownedBed.CompAssignableToPawn, pawn);
                        }
                    }
                }
                // just out of paranoia
                // actually the paranoia is real
                // abandoning a map tile will not trigger ownership unassignment routines
                // we'll encounter null references to the former settlement's beds after the next load
                List<AssignmentGroup> assignmentGroupsToRemove = new List<AssignmentGroup>();
                foreach (var (assignmentGroup, bed) in pawnXAttrs.assignmentGroupToOwnedBedMap) {
                    if (bed == null) {
                        Log.Warning("[BOT] Pawn has a null bed reference stored in overlay ownership field. This is expected if you've recently abandoned a settlement before your last save.");
                        assignmentGroupsToRemove.Add(assignmentGroup);
                    }
                }
                foreach (AssignmentGroup assignmentGroup in assignmentGroupsToRemove) {
                    pawnXAttrs.assignmentGroupToOwnedBedMap.Remove(assignmentGroup);
                }
            }

            foreach (CompBuilding_BedXAttrs bedXAttrs in compBuilding_BedXAttrsRegistry) {
                Building_Bed bed = (Building_Bed)bedXAttrs.parent;
                if (isTheSystemActive) {
                    CompAssignableToPawn catp = bed.GetComp<CompAssignableToPawn>();
                    if(!ModsConfig.BiotechActive || bed.def != ThingDefOf.DeathrestCasket) {
                        // bedXAttrs.assignedPawnsOverlay.AddRange(catp.AssignedPawns.Except(assignedPawnsOverlay)); // done by TryAssignPawn
                        bedXAttrs.uninstalledAssignedPawnsOverlay.AddRange(Traverse.Create(catp).Field("uninstalledAssignedPawns").GetValue<List<Pawn>>().Except(bedXAttrs.uninstalledAssignedPawnsOverlay));
                        foreach (Pawn pawn in bed.CompAssignableToPawn.AssignedPawnsForReading) {
                            if (!bedXAttrs.assignedPawnsOverlay.Contains(pawn)) {
                                Log.Warning($"[BOT] A bed ({bed.GetUniqueLoadID()}) has a Pawn ({pawn.Label}) in its internal ownership list but not its overlay list.");
                            }
                        }
                        // if I am initted and my owner doesn't actually have a map entry for me, unlink me from them
                        List<Pawn> pawnsToRemove = new List<Pawn>();
                        foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay) {
                            CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                            if (!pawnXAttrs.assignmentGroupToOwnedBedMap.ContainsKey(bedXAttrs.MyAssignmentGroup) || pawnXAttrs.assignmentGroupToOwnedBedMap[bedXAttrs.MyAssignmentGroup] != bed) {
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
            foreach (CompPawnXAttrs pawnXAttrs in compPawnXAttrsRegistry) {
                if (pawnXAttrs.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed bed)) {
                    // to generate the message that a pawn has become unlinked from a bed
                    Traverse.Create(bed).Method("RemoveAllOwners", false).GetValue();
                }
            }
            foreach (CompBuilding_BedXAttrs bedXAttrs in compBuilding_BedXAttrsRegistry) {
                bedXAttrs.MyAssignmentGroup = defaultAssignmentGroup;
                // bedXAttrs.assignedPawnsOverlay.Clear();
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

        public override void ExposeData() {
		    base.ExposeData();
            Scribe_Collections.Look(ref this.allAssignmentGroupsByPriority, "allAssignmentGroupsByPriority", LookMode.Deep);
            Scribe_References.Look(ref this.defaultAssignmentGroup, "defaultAssignmentGroup");
	    }
    }
}
