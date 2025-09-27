using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

// New state attached to CompDeathrestBindable

namespace BedOwnershipTools {
    public class CompDeathrestBindableXAttrs : ThingComp {
        public CompDeathrestBindable sibling;

        // Overlays CompDeathrestBindable.boundPawn
        public Pawn boundPawnOverlay;

        public override void Initialize(CompProperties props) {
            sibling = this.parent.GetComp<CompDeathrestBindable>();
            if (sibling == null) {
                Log.Error($"[BOT] A building ({parent.GetUniqueLoadID()}) with a CompDeathrestBindableXAttrs component doesn't have a CompDeathrestBindable component.");
            }
            GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry.Add(this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap) {
            GameComponent_AssignmentGroupManager.Singleton.compDeathrestBindableXAttrsRegistry.Remove(this);
        }

        public void UnbindAsSpare() {
            Gene_Deathrest gene_Deathrest = sibling.BoundPawn?.genes?.GetFirstGeneOfType<Gene_Deathrest>();
            if (gene_Deathrest != null) {
                gene_Deathrest.Notify_BoundBuildingDeSpawned(this.parent);
            }
            HarmonyPatches.Patch_CompDeathrestBindable_Notify_DeathrestGeneRemoved.HintDontClearOverlayBindee();
            sibling.Notify_DeathrestGeneRemoved();
        }

        public void UnbindPermanent() {
            Gene_Deathrest gene_Deathrest = sibling.BoundPawn?.genes?.GetFirstGeneOfType<Gene_Deathrest>();
            if (gene_Deathrest != null) {
                gene_Deathrest.Notify_BoundBuildingDeSpawned(this.parent);
            }
            sibling.Notify_DeathrestGeneRemoved();
        }

        public IEnumerable<Gizmo> CompGetGizmosExtraImpl() {
            // TODO make accessible to user?
            // if (BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings && !BedOwnershipTools.Singleton.settings.deathrestBindingsArePermanent) {
            if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableExtraMenusAndGizmos) {
                yield return new Command_UnbindDeathrestBindable(this);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            return CompGetGizmosExtraImpl();
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
            stringBuilder.AppendInNewLine($"BoundPawn: {sibling.BoundPawn?.Label ?? "null"}");
            stringBuilder.AppendInNewLine($"boundPawnOverlay: {this.boundPawnOverlay?.Label ?? "null"}");
            return stringBuilder.ToString();
        }
    }
}
