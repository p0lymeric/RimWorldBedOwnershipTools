using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BedOwnershipTools {
    public class PawnXAttrs_RelationshipAwareTracker {
        public readonly CompPawnXAttrs parent;

        public int tickGameLastSleptInCommunalBed = -1;
        public int tickGameLastSleptWithPartner = -1;
        public int tickGameLastSleptWithNonPartner = -1;
        public float sharedBedMoodOffset = 0.0f;

        public PawnXAttrs_RelationshipAwareTracker(CompPawnXAttrs parent) {
            this.parent = parent;
        }

        public bool MyActiveOwnedBedIsOnThisMap() {
            return parent.parentPawn.Map == parent.parentPawn.ownership.OwnedBed?.Map;
        }

        // so that we don't affect vanilla bed behaviour, this tracker only affects thoughts
        // if the Pawn has slept in a communal bed in the past 2 days
        // and if they do not own a bed in their current map
        public bool IRecentlySleptInACommunalBed() {
            return this.tickGameLastSleptInCommunalBed >= 0 &&
                this.tickGameLastSleptInCommunalBed + GenDate.TicksPerDay * 2 > Find.TickManager.TicksGame;
        }

        public bool UseRelationshipAwareTrackerThoughts() {
            return !MyActiveOwnedBedIsOnThisMap() && IRecentlySleptInACommunalBed();
        }

        public float CalculateSharedBedMoodOffset(Pawn otherPawn) {
            float result = 0.05f * this.parent.parentPawn.relations.OpinionOf(otherPawn) - 5f;
            if (result < 0.0f) {
                return result;
            } else {
                return 0.0f;
            }
        }

        public static bool WouldWantToSleepWithSpouseOrLover(Pawn sleeper) {
            DirectPawnRelation myMostLikeLovePartnerRel = LovePartnerRelationUtility.ExistingMostLikedLovePartnerRel(sleeper, allowDead: false);
            bool iHaveRestNeed = sleeper.needs?.TryGetNeed<Need_Rest>() != null;
            bool myMostLikedLovePartnerHasRestNeed = myMostLikeLovePartnerRel?.otherPawn.needs?.TryGetNeed<Need_Rest>() != null;
            // Log.Message($"{sleeper.LabelShort} {myMostLikeLovePartnerRel?.otherPawn.LabelShort}.");
            return iHaveRestNeed && myMostLikedLovePartnerHasRestNeed &&
                // null check is bundled with myMostLikedLovePartnerHasRestNeed
                myMostLikeLovePartnerRel.otherPawn.IsColonist &&
                !myMostLikeLovePartnerRel.otherPawn.IsWorldPawn() &&
                myMostLikeLovePartnerRel.otherPawn.relations.everSeenByPlayer &&
                sleeper.IsSlaveOfColony == myMostLikeLovePartnerRel.otherPawn.IsSlaveOfColony;
        }

        public bool ThinkWantToSleepWithSpouseOrLover() {
            return WouldWantToSleepWithSpouseOrLover(this.parent.parentPawn) &&
                (
                    this.tickGameLastSleptWithPartner < 0 ||
                    this.tickGameLastSleptWithPartner + GenDate.TicksPerDay * 2 < Find.TickManager.TicksGame
                )
            ;
        }

        public bool ThinkSharedBed() {
            return this.tickGameLastSleptWithNonPartner >= 0 &&
                this.tickGameLastSleptWithNonPartner + GenDate.TicksPerDay > Find.TickManager.TicksGame;
        }

        public void Notify_SleptInCommunalBed() {
            this.tickGameLastSleptInCommunalBed = Find.TickManager.TicksGame;
        }

        public void Notify_SleptWithSpouseOrLover(Pawn otherPawn = null) {
            this.tickGameLastSleptWithPartner = Find.TickManager.TicksGame;
        }

        public void Notify_SleptWithNonPartner(Pawn otherPawn = null) {
            this.tickGameLastSleptWithNonPartner = Find.TickManager.TicksGame;
            if (otherPawn != null) {
                this.sharedBedMoodOffset = CalculateSharedBedMoodOffset(otherPawn);
            }
        }

        public void ShallowExposeData() {
            Scribe_Values.Look(ref tickGameLastSleptInCommunalBed, "BedOwnershipTools_tickGameLastSleptInCommunalBed", -1);
            Scribe_Values.Look(ref tickGameLastSleptWithPartner, "BedOwnershipTools_tickGameLastSleptWithPartner", -1);
            Scribe_Values.Look(ref tickGameLastSleptWithNonPartner, "BedOwnershipTools_tickGameLastSleptWithNonPartner", -1);
            Scribe_Values.Look(ref sharedBedMoodOffset, "BedOwnershipTools_sharedBedMoodOffset", 0.0f);
        }
    }
}
