using RimWorld;
using Verse;

// Cursed subclass definition for purposes of massaging data presented to
// and manipulated by Dialog_AssignBuildingOwner(CompAssignableToPawn)

// Our strategy is to overlay the assignee and ownership trackers associated with
// CompAssignableToPawn and Pawn_Ownership so that a pawn internally owns at most one bed
// at one time, keeping with the game's base implementation.

// Will it work? Does it bite? Who knows?
// update: 🤮
// update 2: you haven't seen the worst of it (actually it's mostly been moved to CATPBAndPOMethodReplacements)

namespace BedOwnershipTools {
    public class CATPBAssignmentGroupOverlayAdapter : CompAssignableToPawn_Bed {
        private CompAssignableToPawn_Bed inner = null;

        public CATPBAssignmentGroupOverlayAdapter(CompAssignableToPawn_Bed inner) {
            this.inner = inner;
            // xattrs null check performed in patch--this class shouldn't be instantiated unless
            // the inner object actually has a CompBuilding_BedXAttrs component
            CompBuilding_BedXAttrs innerParentXAttrs = inner.parent.GetComp<CompBuilding_BedXAttrs>();
            // ThingComp
            this.parent = inner.parent;
            this.props = inner.props;
            // CompAssignableToPawn
            this.uninstalledAssignedPawns = innerParentXAttrs.uninstalledAssignedPawnsOverlay;
            this.assignedPawns = innerParentXAttrs.assignedPawnsOverlay;
        }

        public override bool AssignedAnything(Pawn pawn) {
            return CATPBAndPOMethodReplacements.AssignedAnything(this, pawn);
        }

        public override void TryAssignPawn(Pawn pawn) {
            inner.TryAssignPawn(pawn);
        }

        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false) {
            inner.TryUnassignPawn(pawn, sort, uninstall);
        }
    }
}
