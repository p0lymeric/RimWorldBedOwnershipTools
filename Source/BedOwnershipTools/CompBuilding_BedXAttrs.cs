using System.Collections.Generic;
using System.Linq;
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
                    return GameComponent_AssignmentGroupManager.Singleton.defaultAssignmentGroup;
                } else {
                    return myAssignmentGroup;
                }
            }
            set {
                if (value == GameComponent_AssignmentGroupManager.Singleton.defaultAssignmentGroup) {
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
                if(ModsConfig.BiotechActive && bed.def == ThingDefOf.DeathrestCasket) {
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
                        if (pawnXAttrs.assignmentGroupToOwnedBedMap.TryGetValue(assignmentGroup, out Building_Bed oldBed)) {
                            if (pawn.ownership.OwnedBed == oldBed) {
                                oldBed.CompAssignableToPawn.ForceRemovePawn(pawn);
                                bed.CompAssignableToPawn.ForceAddPawn(pawn);
                                Traverse.Create(pawn.ownership).Field("intOwnedBed").SetValue(bed);
                            }
                        }
                        CATPBAndPOMethodReplacements.UnclaimBedDirected(pawn, assignmentGroup);
                        pawnXAttrs.assignmentGroupToOwnedBedMap.Remove(oldAssignmentGroup);
                        pawnXAttrs.assignmentGroupToOwnedBedMap[assignmentGroup] = bed;
                    }
                    bedXAttrs.MyAssignmentGroup = assignmentGroup;
                }
            }
        }

        // calls via reflection are expensive...
        // called once every TickLong ticks (2000 ticks ~ 33.3s) to enforce inter-mod setting coherency in the long term
        // also called every frame when user has the bed open so they get interactive feedback from toggling settings
        private void PeriodicInteractionSensitiveTasks() {
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

        public override void CompTickLong() {
            PeriodicInteractionSensitiveTasks();
        }

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
                        if(!ModsConfig.BiotechActive || bed.def != ThingDefOf.DeathrestCasket) {
                            if(!this.isAssignedToCommunity) {
                                yield return toggleIsAssignmentPinned;
                            }
                        }
                    }
                }

                Command_SetAssignmentGroup selectAssignmentGroup = new Command_SetAssignmentGroup(this);
                if (BedOwnershipTools.Singleton.settings.enableBedAssignmentGroups) {
                    if (bed.Faction == Faction.OfPlayer && !bed.ForPrisoners && !bed.Medical) {
                        if(!ModsConfig.BiotechActive || bed.def != ThingDefOf.DeathrestCasket) {
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
                        Traverse.Create(bed).Method("RemoveAllOwners", false).GetValue();
                    }
                    this.isAssignedToCommunity = !this.isAssignedToCommunity;
                };
                if (disableGizmos || disableToggleIsAssignedToCommunity) {
                    toggleIsAssignedToCommunity.Disable();
                }
                if (BedOwnershipTools.Singleton.settings.enableCommunalBeds) {
                    if (bed.Faction == Faction.OfPlayer && !bed.ForPrisoners && !bed.Medical) {
                        if(!ModsConfig.BiotechActive || bed.def != ThingDefOf.DeathrestCasket) {
                            yield return toggleIsAssignedToCommunity;
                        }
                    }
                }

                PeriodicInteractionSensitiveTasks();
            }
        }

    }
}
