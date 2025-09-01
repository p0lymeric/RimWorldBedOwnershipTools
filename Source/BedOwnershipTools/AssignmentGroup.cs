using RimWorld;
using Verse;
using UnityEngine;

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
