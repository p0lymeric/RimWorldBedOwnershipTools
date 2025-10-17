using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public class Command_UnbindDeathrestBindable : Command {
        // private static readonly CachedTexture UnbindDeathrestBindableTex = new("BedOwnershipTools/UI/Commands/DeathrestAutoSchedule");

        private CompDeathrestBindableXAttrs xAttrs;
        public Command_UnbindDeathrestBindable(CompDeathrestBindableXAttrs xAttrs) {
            this.xAttrs = xAttrs;
            this.defaultLabel = "[DEV] " + "BedOwnershipTools.Command_UnbindDeathrestBindable".Translate();
            this.defaultDesc = "BedOwnershipTools.Command_UnbindDeathrestBindableDesc".Translate();
            if (xAttrs != null && (xAttrs.boundPawnOverlay != null || xAttrs.sibling.BoundPawn != null)) {
            } else {
                this.Disable("BedOwnershipTools.Command_UnbindDeathrestBindableDisabledNoBindeeReason".Translate());
            }
            // TODO icon
            // this.icon = UnbindDeathrestBindableTex.Texture;
        }

        public override void ProcessInput(Event ev) {
            base.ProcessInput(ev);
            this.xAttrs.UnbindPermanent();
        }
    }
}
