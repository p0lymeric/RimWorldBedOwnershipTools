using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public class Command_SetAutomaticDeathrestMode : Command {
        private static readonly CachedTexture DeathrestAutoScheduleTex = new("BedOwnershipTools/UI/Commands/DeathrestAutoSchedule");

        private CompPawnXAttrs xAttrs;

        public Command_SetAutomaticDeathrestMode(CompPawnXAttrs xAttrs) {
            this.xAttrs = xAttrs;
            this.defaultLabel = "BedOwnershipTools.Command_SetAutomaticDeathrestMode".Translate();
            if (xAttrs != null) {
                NamedArgument parentPawnNA = xAttrs.parentPawn.Named("PAWN");
                this.defaultDesc = "BedOwnershipTools.Command_SetAutomaticDeathrestModeDesc".Translate(
                    parentPawnNA,
                    xAttrs.automaticDeathrestTracker.automaticDeathrestMode.LabelStringWithColour().Named("SCHEDULE"),
                    xAttrs.automaticDeathrestTracker.automaticDeathrestMode.LabelStringDisciplineDescriptionTranslationKey().Translate(parentPawnNA).Named("SCHEDULEDESC")
                ).Resolve();
            } else {
                this.defaultDesc = "BedOwnershipTools.Command_GenericDisabledDesc".Translate();
                this.Disable("BedOwnershipTools.Command_GenericDisabledNoOwnerReason".Translate());
            }
            this.icon = DeathrestAutoScheduleTex.Texture;
            // TODO want to display the current enum level on the gizmo
        }

        public override GizmoResult GizmoOnGUI(Vector2 loc, float maxWidth, GizmoRenderParms parms) {
            GizmoResult result = base.GizmoOnGUI(loc, maxWidth, parms);
            Rect rect = new(loc.x, loc.y, GetWidth(maxWidth), 75f);
            Rect position = new(rect.x + rect.width - 24f, rect.y, 24f, 24f);
            Texture2D image = xAttrs?.automaticDeathrestTracker.automaticDeathrestMode.Texture();
            image ??= AutomaticDeathrestMode.Manual.Texture();
            GUI.DrawTexture(position, image);
            return result;
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            List<FloatMenuOption> list = new();
            foreach (AutomaticDeathrestMode mode in AutomaticDeathrestModeExtensions.GetValues()) {
                list.Add(new FloatMenuOption(mode.LabelString(), delegate {
                    if (xAttrs != null) {
                        Gene_Deathrest gene_Deathrest = xAttrs.parentPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                        xAttrs.automaticDeathrestTracker.automaticDeathrestMode = mode;
                        if (mode.Discipline() == AutomaticDeathrestScheduleDiscipline.Calendar) {
                            gene_Deathrest.autoWake = true;
                        }
                    }
                }, mode.Texture(), Color.white));
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }
    }
}
