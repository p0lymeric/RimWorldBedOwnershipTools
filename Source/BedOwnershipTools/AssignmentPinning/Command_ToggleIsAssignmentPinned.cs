using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class Command_ToggleIsAssignmentPinned : Command_Toggle {
        private static readonly CachedTexture PinOwnerTex = new("BedOwnershipTools/UI/Commands/PinOwner");

        public Command_ToggleIsAssignmentPinned(CompBuilding_BedXAttrs xAttrs, bool disable) {
            this.defaultLabel = "BedOwnershipTools.CommandToggleIsAssignmentPinned".Translate();
            this.defaultDesc = "BedOwnershipTools.CommandToggleIsAssignmentPinnedDesc".Translate();
            this.icon = PinOwnerTex.Texture;
            this.isActive = () => xAttrs.IsAssignmentPinned;
            this.toggleAction = delegate {
                xAttrs.IsAssignmentPinned = !xAttrs.IsAssignmentPinned;
            };
            if (disable) {
                this.Disable();
            }
        }
    }
}
