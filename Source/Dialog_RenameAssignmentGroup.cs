using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class Dialog_RenameAssignmentGroup : Dialog_Rename<AssignmentGroup> {
	    public Dialog_RenameAssignmentGroup(AssignmentGroup assignmentGroup) : base(assignmentGroup) {
        }
        protected override AcceptanceReport NameIsValid(string name) {
            AcceptanceReport result = base.NameIsValid(name);
            if (!result.Accepted) {
                return result;
            }
            return true;
        }
    }
}
