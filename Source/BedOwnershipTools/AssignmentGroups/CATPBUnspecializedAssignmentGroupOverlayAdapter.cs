using System;
using System.Collections.Generic;
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
// updat3: actually surprised how well this idea has extended even to modded children of Building_Bed/CATP_Bed

namespace BedOwnershipTools {
    public class CATPBUnspecializedAssignmentGroupOverlayAdapter : CompAssignableToPawn_Bed {
        public CompAssignableToPawn_Bed inner = null;

        private Predicate<CATPBUnspecializedAssignmentGroupOverlayAdapter, Pawn> assignedAnythingImpl;
        private Func<CATPBUnspecializedAssignmentGroupOverlayAdapter, IEnumerable<Pawn>> assigningCandidatesGetterImpl;

        public CATPBUnspecializedAssignmentGroupOverlayAdapter(
            CompAssignableToPawn_Bed inner,
            Predicate<CATPBUnspecializedAssignmentGroupOverlayAdapter, Pawn> assignedAnythingImpl,
            Func<CATPBUnspecializedAssignmentGroupOverlayAdapter, IEnumerable<Pawn>> assigningCandidatesGetterImpl
        ) {
            this.inner = inner;
            this.assignedAnythingImpl = assignedAnythingImpl;
            this.assigningCandidatesGetterImpl = assigningCandidatesGetterImpl;
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

        public override IEnumerable<Pawn> AssigningCandidates {
            get {
                return assigningCandidatesGetterImpl(this);
            }
        }

        public override bool AssignedAnything(Pawn pawn) {
            return assignedAnythingImpl(this, pawn);
        }

        public override void TryAssignPawn(Pawn pawn) {
            // Virtual calling will call the most derived implementation applicable to inner.
            inner.TryAssignPawn(pawn);
        }

        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false) {
            // Virtual calling will call the most derived implementation applicable to inner.
            inner.TryUnassignPawn(pawn, sort, uninstall);
        }
    }
}
