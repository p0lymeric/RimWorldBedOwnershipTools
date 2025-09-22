using System.Text;
using RimWorld;
using Verse;

// New state attached to CompDeathrestBindable

namespace BedOwnershipTools {
    public class CompDeathrestBindableXAttrs : ThingComp {
        // Overlays CompDeathrestBindable.boundPawn
        public Pawn boundPawnOverlay;

        public override void Initialize(CompProperties props) {
            GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry.Remove(this);
        }

        public override void PostExposeData() {
		    base.PostExposeData();
            if (BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                Scribe_References.Look(ref this.boundPawnOverlay, "BedOwnershipTools_boundPawnOverlay");
            }
            // if (Scribe.mode == LoadSaveMode.PostLoadInit) {
            //     // rest of init is done in AGMCompartment_EnhancedDeathrest
            // }
	    }

        public override string CompInspectStringExtra() {
            if (!Prefs.DevMode || !BedOwnershipTools.Singleton.settings.devEnableDebugInspectStringListings) {
                return "";
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (this.parent is not Building_Bed) {
                // CompBuilding_BedXAttrs will print this
                stringBuilder.AppendInNewLine("LoadID: " + this.parent.GetUniqueLoadID());
            }
            CompDeathrestBindable cdb = this.parent.GetComp<CompDeathrestBindable>();
            stringBuilder.AppendInNewLine($"BoundPawn: {cdb.BoundPawn?.Label ?? "null"}");
            stringBuilder.AppendInNewLine($"boundPawnOverlay: {this.boundPawnOverlay?.Label ?? "null"}");
            return stringBuilder.ToString();
        }
    }
}
