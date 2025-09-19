using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

// New state attached to Building_Bed

namespace BedOwnershipTools {
    public class CompBuilding_BedXAttrs : ThingComp {
        // Whether this bed is owned by the community -- i.e. no assigned owner and anyone may use it
        private bool isAssignedToCommunity = false;
        public bool IsAssignedToCommunity {
            get {
                return BedOwnershipTools.Singleton.settings.enableCommunalBeds && isAssignedToCommunity;
            }
            set {
                isAssignedToCommunity = value;
            }
        }
        // Whether a pawn is allowed to claim or relinquish an assignment to this bed outside of player action
        private bool isAssignmentPinned = false;
        public bool IsAssignmentPinned {
            get {
                return BedOwnershipTools.Singleton.settings.enableBedAssignmentPinning && isAssignmentPinned;
            }
            set {
                isAssignmentPinned = value;
            }
        }
        // Group for the multiple bed assignment system
        // To avoid a need to synchronize with the AssignmentGroupManager during game initialization
        // we'll strictly represent null references as references to the default assignment group through this accessor
        private AssignmentGroup myAssignmentGroup = null;
        public AssignmentGroup MyAssignmentGroup {
            get {
                if (myAssignmentGroup == null) {
                    return GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.defaultAssignmentGroup;
                } else {
                    return myAssignmentGroup;
                }
            }
            set {
                if (value == GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.defaultAssignmentGroup) {
                    myAssignmentGroup = null;
                } else {
                    myAssignmentGroup = value;
                }
            }
        }
        // Overlays the assignedPawns and uninstalledAssignedPawns Lists in CompAssignableToPawn
        public List<Pawn> assignedPawnsOverlay = new List<Pawn>();
        public List<Pawn> uninstalledAssignedPawnsOverlay = new List<Pawn>();

        // Last frame when a dispatched action via a Gizmo called back to a ByInterface method of an instance
        private static int LastInterfaceActionFrame = -1;

        public override void Initialize(CompProperties props) {
            if (this.parent is not Building_Bed) {
                Log.Error("[BOT] Tried to create CompBuilding_BedXAttrs under a non-Building_Bed parent ThingWithComps.");
            }
            GameComponent_AssignmentGroupManager.Singleton.compBuilding_BedXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compBuilding_BedXAttrsRegistry.Remove(this);
        }

        public override void PostExposeData() {
		    base.PostExposeData();
            Scribe_Values.Look(ref this.isAssignedToCommunity, "BedOwnershipTools_isAssignedToCommunity", false);
            Scribe_Values.Look(ref this.isAssignmentPinned, "BedOwnershipTools_isAssignmentPinned", false);
            Scribe_References.Look(ref this.myAssignmentGroup, "BedOwnershipTools_myAssignmentGroup");

            if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                Scribe_Collections.Look(ref this.assignedPawnsOverlay, "BedOwnershipTools_assignedPawnsOverlay", LookMode.Reference);
                Scribe_Collections.Look(ref this.uninstalledAssignedPawnsOverlay, "BedOwnershipTools_uninstalledAssignedPawnsOverlay", LookMode.Reference);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit) {
                // copied from CompAssignableToPawn.PostExposeData
                // possibly to clean up any references to missing Pawns after a savegame load
			    assignedPawnsOverlay.RemoveAll((Pawn x) => x == null);
                uninstalledAssignedPawnsOverlay.RemoveAll((Pawn x) => x == null);

                // rest of init is done in the AGM
		    }
	    }

        public void SetAssignmentGroupByInterface(AssignmentGroup assignmentGroup) {
            // lol I went through the stages of grief searching for how the game handles
            // Gizmo action dispatch on multiple targets
            // here I was thinking it'd be fancy and elegant and possibly
            // handled by methods like GroupsWith or MergeWith
            // but noooo
            if (LastInterfaceActionFrame == Time.frameCount) {
                return;
            }
            LastInterfaceActionFrame = Time.frameCount;
            foreach (Building_Bed bed in Find.Selector.SelectedObjects.OfType<Building_Bed>()) {
                if(CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                    continue;
                }
                CompBuilding_BedXAttrs bedXAttrs = bed.GetComp<CompBuilding_BedXAttrs>();
                if (bedXAttrs == null) {
                    continue;
                }
                AssignmentGroup oldAssignmentGroup = bedXAttrs.MyAssignmentGroup;
                if(assignmentGroup != oldAssignmentGroup) {
                    // get my Pawn owners and transfer ownership to the new group
                    foreach (Pawn pawn in bedXAttrs.assignedPawnsOverlay) {
                        CompPawnXAttrs pawnXAttrs = pawn.GetComp<CompPawnXAttrs>();
                        if (pawnXAttrs == null) {
                            continue;
                        }
                        if (pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed oldBed)) {
                            if (pawn.ownership.OwnedBed == oldBed) {
                                oldBed.CompAssignableToPawn.ForceRemovePawn(pawn);
                                bed.CompAssignableToPawn.ForceAddPawn(pawn);
                                HarmonyPatches.DelegatesAndRefs.Pawn_Ownership_intOwnedBed(pawn.ownership) = bed;
                            }
                        }
                        CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, assignmentGroup);
                        pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap.Remove(oldAssignmentGroup);
                        pawnXAttrs.assignmentGroupTracker.assignmentGroupToOwnedBedMap[assignmentGroup] = bed;
                    }
                    bedXAttrs.MyAssignmentGroup = assignmentGroup;
                }
            }
        }

        // called every frame when user has the bed open to respond to settings toggles from other mods
        // so we don't need to hook into those mods' gizmos to listen to their events
        private void SettingsFixupTasks() {
            if (this.parent is Building_Bed bed) {
                if (
                    BedOwnershipTools.Singleton.runtimeHandles.modOneBedToSleepWithAllLoadedForCompatPatching &&
                    HarmonyPatches.ModCompatPatches_OneBedToSleepWithAll.RemoteCall_IsPolygamy(bed) &&
                    this.isAssignedToCommunity
                ) {
                    this.isAssignedToCommunity = false;
                }
            }
        }

        // NOTE Building_Bed has a TickerType of Never so none of TickInterval, Tick, TickRare, or TickLong will actually execute
        // public override void CompTickLong() {
        //     SettingsFixupTasks();
        // }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            bool disableGizmos = false;
            bool disableToggleIsAssignedToCommunity = false;
            if (
                BedOwnershipTools.Singleton.runtimeHandles.modHospitalityLoadedForCompatPatching &&
                BedOwnershipTools.Singleton.runtimeHandles.typeHospitalityBuilding_GuestBed.IsInstanceOfType(this.parent)
            ) {
                disableGizmos = true;
            }

            if (this.parent is Building_Bed bed) {
                // technically need to schedule these fixups inside the gizmo delegates or some time following the
                // last pause-independent frame when a toggle could've occured
                // possible race condition: player manages to toggle a gizmo and close the inspector on the same frame
                // let's ignore handling this race condition for now
                SettingsFixupTasks();

                if (
                    BedOwnershipTools.Singleton.runtimeHandles.modOneBedToSleepWithAllLoadedForCompatPatching &&
                    HarmonyPatches.ModCompatPatches_OneBedToSleepWithAll.RemoteCall_IsPolygamy(bed)
                ) {
                    disableToggleIsAssignedToCommunity = true;
                }

                Command_Toggle toggleIsAssignmentPinned = new Command_Toggle();
                toggleIsAssignmentPinned.defaultLabel = "BedOwnershipTools.CommandToggleIsAssignmentPinned".Translate();
                toggleIsAssignmentPinned.defaultDesc = "BedOwnershipTools.CommandToggleIsAssignmentPinnedDesc".Translate();
                toggleIsAssignmentPinned.icon = ContentFinder<Texture2D>.Get("BedOwnershipTools/UI/Commands/PinOwner");
                toggleIsAssignmentPinned.isActive = () => this.isAssignmentPinned;
                toggleIsAssignmentPinned.toggleAction = delegate {
                    this.isAssignmentPinned = !this.isAssignmentPinned;
                };
                if (disableGizmos) {
                    toggleIsAssignmentPinned.Disable();
                }
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentPinning) {
                    if (bed.Faction == Faction.OfPlayer && !bed.ForPrisoners && !bed.Medical) {
                        if(!CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                            if(!this.isAssignedToCommunity) {
                                yield return toggleIsAssignmentPinned;
                            }
                        }
                    }
                }

                Command_SetAssignmentGroup selectAssignmentGroup = new Command_SetAssignmentGroup(this);
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                    if (bed.Faction == Faction.OfPlayer && !bed.ForPrisoners && !bed.Medical) {
                        if(!CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                            if (disableGizmos) {
                                selectAssignmentGroup.Disable();
                            }
                            if(!this.isAssignedToCommunity) {
                                yield return selectAssignmentGroup;
                            }
                        }
                    }
                }

                Command_Toggle toggleIsAssignedToCommunity = new Command_Toggle();
                toggleIsAssignedToCommunity.defaultLabel = "BedOwnershipTools.CommandToggleIsAssignedToCommunity".Translate();
                toggleIsAssignedToCommunity.defaultDesc = this.isAssignedToCommunity ?
                    "BedOwnershipTools.CommandToggleIsAssignedToCommunityCommunalDesc".Translate() :
                    "BedOwnershipTools.CommandToggleIsAssignedToCommunityNonCommunalDesc".Translate();
                toggleIsAssignedToCommunity.icon = ContentFinder<Texture2D>.Get("BedOwnershipTools/UI/Commands/CommunalOwner");
                toggleIsAssignedToCommunity.isActive = () => this.isAssignedToCommunity;
                toggleIsAssignedToCommunity.toggleAction = delegate {
                    if (!this.isAssignedToCommunity) {
                        HarmonyPatches.DelegatesAndRefs.Building_Bed_RemoveAllOwners(bed, false);
                    }
                    this.isAssignedToCommunity = !this.isAssignedToCommunity;
                };
                if (disableGizmos || disableToggleIsAssignedToCommunity) {
                    toggleIsAssignedToCommunity.Disable();
                }
                if (BedOwnershipTools.Singleton.settings.enableCommunalBeds) {
                    if (bed.Faction == Faction.OfPlayer && !bed.ForPrisoners && !bed.Medical) {
                        if(!CATPBAndPOMethodReplacements.IsDefOfDeathrestCasket(bed.def)) {
                            yield return toggleIsAssignedToCommunity;
                        }
                    }
                }
            }
        }

        static void StringBuilder_PrintOwnersList(StringBuilder stringBuilder, string prefix, List<Pawn> ownersList) {
            if (ownersList.Count == 0) {
                stringBuilder.AppendInNewLine(prefix + "Owner".Translate() + ": " + "Nobody".Translate());
            }
            else if (ownersList.Count == 1) {
                stringBuilder.AppendInNewLine(prefix + "Owner".Translate() + ": " + ownersList[0].Label);
            }
            else {
                stringBuilder.AppendInNewLine(prefix + "Owners".Translate() + ": ");
                bool flag = false;
                for (int i = 0; i < ownersList.Count; i++) {
                    if (flag) {
                        stringBuilder.Append(", ");
                    }
                    flag = true;
                    stringBuilder.Append(ownersList[i].LabelShort);
                }
            }

        }

        public override string CompInspectStringExtra() {
            if (!Prefs.DevMode || !BedOwnershipTools.Singleton.settings.devEnableDebugInspectStringListings) {
                return "";
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (this.parent is Building_Bed bed) {
                stringBuilder.AppendInNewLine("LoadID: " + bed.GetUniqueLoadID());
                for (int sleepingSlot = 0; sleepingSlot < bed.SleepingSlotsCount; sleepingSlot++) {
                    stringBuilder.AppendInNewLine($"CurOccupant[{sleepingSlot}]: ");
                    Pawn curOccupant = bed.GetCurOccupant(sleepingSlot);
                    if (curOccupant != null) {
                        stringBuilder.Append(curOccupant.Label);
                    } else {
                        stringBuilder.Append("null");
                    }
                }
                StringBuilder_PrintOwnersList(stringBuilder, "assignedPawns", bed.CompAssignableToPawn.AssignedPawnsForReading);
                StringBuilder_PrintOwnersList(stringBuilder, "uninstalledAssignedPawns", HarmonyPatches.DelegatesAndRefs.CompAssignableToPawn_uninstalledAssignedPawns(bed.CompAssignableToPawn));
                StringBuilder_PrintOwnersList(stringBuilder, "assignedPawnsOverlay", this.assignedPawnsOverlay);
                StringBuilder_PrintOwnersList(stringBuilder, "uninstalledAssignedPawnsOverlay", this.uninstalledAssignedPawnsOverlay);
            }
            return stringBuilder.ToString();
        }

        // NOTE Building_Bed does not call ThingWithComps.DrawGUIOverlay so Comp labels need to be drawn in a patch

        public void DrawPinnedAGLabel() {
            if (this.parent is Building_Bed bed) {
                string displayString = "(";
                bool insertComma = false;
                bool displayMe = false;
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups && this.MyAssignmentGroup.showDisplay) {
                    displayString += this.MyAssignmentGroup.name;
                    insertComma = true;
                    displayMe = true;
                }
                if (this.IsAssignmentPinned) {
                    if (insertComma) {
                        displayString += ", ";
                    }
                    displayString += "BedOwnershipTools.PinnedAbbrev".Translate();
                    displayMe = true;
                }
                displayString += ")";
                if (displayMe) {
                    Vector2 labelPos = GenMapUI.LabelDrawPosFor(bed, -0.4f);
                    labelPos.y += 13f;
                    GenMapUI.DrawThingLabel(labelPos, displayString, GenMapUI.DefaultThingLabelColor);
                }
            }
        }

    }
}
