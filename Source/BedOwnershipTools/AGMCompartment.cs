using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public abstract class AGMCompartment {
        public GameComponent_AssignmentGroupManager parent;

        public AGMCompartment(Game game, GameComponent_AssignmentGroupManager parent) {
            this.parent = parent;
        }
    }
}
