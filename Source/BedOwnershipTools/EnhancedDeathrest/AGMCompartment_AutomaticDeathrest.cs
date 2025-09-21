using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class AGMCompartment_AutomaticDeathrest : AGMCompartment {
        // Stores the last persisted value of enableAutomaticDeathrest so that an enable during
        // Notify_WriteSettings would fix auto-wake settings to required values
        public bool isSubsystemActive = false;

        public AGMCompartment_AutomaticDeathrest(Game game, GameComponent_AssignmentGroupManager parent) : base(game, parent) {}

        public void Notify_WriteSettings() {
            if (isSubsystemActive ^ BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                FinalizeInit();
            }
        }

        public void FinalizeInit() {
            isSubsystemActive = BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest;
            if (isSubsystemActive) {
                // after the subsystem is disabled, we'll remember the Pawn's autoschedule but will allow the player to modify auto-wake settings
                // if the subsystem is enabled again, we'll force auto-wake to the required value
                foreach (CompPawnXAttrs pawnXAttrs in parent.compPawnXAttrsRegistry) {
                    if (pawnXAttrs.automaticDeathrestTracker.automaticDeathrestMode.Discipline() == AutomaticDeathrestScheduleDiscipline.Calendar) {
                        Gene_Deathrest gene_Deathrest = pawnXAttrs.parentPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                        if (gene_Deathrest != null && gene_Deathrest.Active) {
                            gene_Deathrest.autoWake = true;
                        }
                    }
                }
            }
        }
    }
}
