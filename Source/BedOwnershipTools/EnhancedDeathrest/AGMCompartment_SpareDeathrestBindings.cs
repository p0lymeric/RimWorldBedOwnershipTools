using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class AGMCompartment_SpareDeathrestBindings : AGMCompartment {
        // Stores the last persisted value of enableSpareDeathrestBindings so that a toggle during
        // Notify_WriteSettings would cause deathrest bindings to be reset
        public bool isSubsystemActive = false;

        public AGMCompartment_SpareDeathrestBindings(Game game, GameComponent_AssignmentGroupManager parent) : base(game, parent) {}

        public void Notify_WriteSettings() {
            if (isSubsystemActive ^ BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                foreach (CompDeathrestBindableXAttrs cdbXAttrs in parent.compDeathrestBindableXAttrsRegistry) {
                    cdbXAttrs.boundPawnOverlay = null;
                }
                FinalizeInit();
            }
        }

        public void FinalizeInit() {
            isSubsystemActive = BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings;

            if (isSubsystemActive) {
                foreach (CompDeathrestBindableXAttrs cdbXAttrs in parent.compDeathrestBindableXAttrsRegistry) {
                    CompDeathrestBindable cdb = cdbXAttrs.parent.GetComp<CompDeathrestBindable>();
                    if (cdb == null) {
                        Log.Warning($"[BOT] A building ({cdbXAttrs.parent.GetUniqueLoadID()}) with a CompDeathrestBindableXAttrs component doesn't have a CompDeathrestBindable component.");
                    } else if (cdbXAttrs.boundPawnOverlay == null) {
                        cdbXAttrs.boundPawnOverlay = cdb.BoundPawn;
                    }
                }
            }
        }
    }
}
