using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public class Command_SetAssignmentGroup : Command {
        private static readonly CachedTexture SetAssignmentGroupTex = new("BedOwnershipTools/UI/Commands/SetAssignmentGroup");

        private CompBuilding_BedXAttrs xAttrs;

        public Command_SetAssignmentGroup(CompBuilding_BedXAttrs xAttrs, bool disable) {
            this.xAttrs = xAttrs;
            this.defaultLabel = "BedOwnershipTools.CommandSetAssignmentGroup".Translate();
            this.defaultDesc = "BedOwnershipTools.CommandSetAssignmentGroupDesc".Translate();
            this.icon = SetAssignmentGroupTex.Texture;
            if (disable) {
                this.Disable();
            }
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            foreach (AssignmentGroup assignmentGroup in GameComponent_AssignmentGroupManager.Singleton.agmCompartment_AssignmentGroups.allAssignmentGroupsByPriority) {
                list.Add(new FloatMenuOption(assignmentGroup.name, delegate {
                    xAttrs.SetAssignmentGroupByInterface(assignmentGroup);
                }, (Texture2D)null, Color.white));
            }
            list.Add(new FloatMenuOption("BedOwnershipTools.Edit".Translate(), delegate {
                Find.WindowStack.Add(new Dialog_EditAssignmentGroups());
            }, (Texture2D)null, Color.white));
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
