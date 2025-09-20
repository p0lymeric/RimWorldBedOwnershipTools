using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public class Command_SetAutomaticDeathrestMode : Command {
        private CompPawnXAttrs xAttrs;

        public Command_SetAutomaticDeathrestMode(CompPawnXAttrs xAttrs) {
            this.xAttrs = xAttrs;
            this.defaultLabel = "BedOwnershipTools.Command_SetAutomaticDeathrestMode".Translate();
            this.defaultDesc = "BedOwnershipTools.Command_SetAutomaticDeathrestModeDesc".Translate();
            this.icon = ContentFinder<Texture2D>.Get("BedOwnershipTools/UI/Commands/DeathrestAutoSchedule");
            // TODO want to display the current level on the gizmo
            // TODO want to display the current discipline's icon on the top right of the gizmo
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            List<FloatMenuOption> list = new();
            foreach (AutomaticDeathrestMode mode in AutomaticDeathrestModeExtensions.GetValues()) {
                list.Add(new FloatMenuOption(mode.LabelString(), delegate {
                    Gene_Deathrest gene_Deathrest = xAttrs.parentPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                    xAttrs.automaticDeathrestTracker.automaticDeathrestMode = mode;
                    if (mode.Discipline() == AutomaticDeathrestScheduleDiscpline.Calendar) {
                        gene_Deathrest.autoWake = true;
                    }
                }, mode.Texture(), Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
