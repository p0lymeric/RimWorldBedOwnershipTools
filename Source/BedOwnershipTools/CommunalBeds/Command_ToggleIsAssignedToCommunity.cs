using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class Command_ToggleIsAssignedToCommunity : Command_Toggle {
        private static readonly CachedTexture CommunalOwnerTex = new("BedOwnershipTools/UI/Commands/CommunalOwner");

        public Command_ToggleIsAssignedToCommunity(CompBuilding_BedXAttrs xAttrs, bool disable) {
            this.defaultLabel = "BedOwnershipTools.CommandToggleIsAssignedToCommunity".Translate();
            this.defaultDesc = xAttrs.IsAssignedToCommunity ?
                "BedOwnershipTools.CommandToggleIsAssignedToCommunityCommunalDesc".Translate() :
                "BedOwnershipTools.CommandToggleIsAssignedToCommunityNonCommunalDesc".Translate();
            this.icon = CommunalOwnerTex.Texture;
            this.isActive = () => xAttrs.IsAssignedToCommunity;
            this.toggleAction = delegate {
                if (!xAttrs.IsAssignedToCommunity) {
                    // cast compatibility already checked once before ctor call
                    DelegatesAndRefs.Building_Bed_RemoveAllOwners((Building_Bed)xAttrs.parent, false);
                }
                xAttrs.IsAssignedToCommunity = !xAttrs.IsAssignedToCommunity;
            };
            if (disable) {
                this.Disable();
            }
        }
    }
}
