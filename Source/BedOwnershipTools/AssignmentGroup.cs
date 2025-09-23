using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class AssignmentGroup : IExposable, ILoadReferenceable, IRenameable {
        public int id = -1;
        public string name = "";
        public bool showDisplay = false;
        // public Color colour = Color.black;

        public string RenamableLabel {
            get {
                return name;
            }
            set {
                name = value;
            }
        }
	    public string BaseLabel => RenamableLabel;
	    public string InspectLabel => RenamableLabel;

        public AssignmentGroup() {
        }

        public AssignmentGroup(int id, string name, bool showDisplay) {
            this.id = id;
            this.name = name;
            this.showDisplay = showDisplay;
            // this.colour = colour;
        }

        // TODO should apply a caching trick here and invalidate the cached entry when assignment groups are deleted, created, or reordered
        public int Priority() {
            int priority = GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority.IndexOf(this);
            return priority >= 0 ? priority : int.MaxValue;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref id, "id", -1);
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref showDisplay, "showDisplay");
            // Scribe_Values.Look(ref colour, "colour");
	    }

        public string GetUniqueLoadID() {
            return $"AssignmentGroup_{id}";
        }
    }
}
