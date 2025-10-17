using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class Command_ToggleAutoWake : Command_Toggle {
        public Command_ToggleAutoWake(CompPawnXAttrs xAttrs) {
            this.defaultLabel = "AutoWake".Translate().CapitalizeFirst();
            this.icon = DelegatesAndRefs.Gene_Deathrest_AutoWakeCommandTex().Texture;
            Gene_Deathrest gene_Deathrest = xAttrs?.parentPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
            if (gene_Deathrest != null && gene_Deathrest.Active) {
                this.defaultDesc = "AutoWakeDesc".Translate(xAttrs.parentPawn.Named("PAWN")).Resolve();
                this.isActive = () => gene_Deathrest.autoWake;
                this.toggleAction = delegate {
                    gene_Deathrest.autoWake = !gene_Deathrest.autoWake;
                };
                if (BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                    if (xAttrs.automaticDeathrestTracker.automaticDeathrestMode.Discipline() == AutomaticDeathrestScheduleDiscipline.Calendar) {
                        this.Disable("BedOwnershipTools.Command_AutoWakeDisabledCalendarReason".Translate());
                    }
                }
            } else {
                this.defaultDesc = "BedOwnershipTools.Command_GenericDisabledDesc".Translate();
                this.isActive = () => false;
                this.Disable("BedOwnershipTools.Command_GenericDisabledNoOwnerReason".Translate());
            }
        }
    }
}
