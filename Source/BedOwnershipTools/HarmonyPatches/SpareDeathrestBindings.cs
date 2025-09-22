using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;

// Summary of patches
// Spare deathrest bindings

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.BindTo))]
        public class Patch_CompDeathrestBindable_BindTo {
            static void Postfix(CompDeathrestBindable __instance, Pawn pawn) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return;
                }
                CompDeathrestBindableXAttrs cdbXAttrs = __instance.parent.GetComp<CompDeathrestBindableXAttrs>();
                cdbXAttrs.boundPawnOverlay = pawn;
            }
        }

        // TODO unify with Gene_Deathrest.Reset patch
        [HarmonyPatch(typeof(CompDeathrestBindable), nameof(CompDeathrestBindable.Notify_DeathrestGeneRemoved))]
        public class Patch_CompDeathrestBindable_Notify_DeathrestGeneRemoved {
            static bool setBeforeCallingToNotClearOverlayBindee = false;

            public static void HintDontClearOverlayBindee() {
                setBeforeCallingToNotClearOverlayBindee = true;
            }
            public static void ClearHints() {
                setBeforeCallingToNotClearOverlayBindee = false;
            }
            static void Postfix(CompDeathrestBindable __instance) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    ClearHints();
                    return;
                }
                if (!setBeforeCallingToNotClearOverlayBindee) {
                    CompDeathrestBindableXAttrs cdbXAttrs = __instance.parent.GetComp<CompDeathrestBindableXAttrs>();
                    cdbXAttrs.boundPawnOverlay = null;
                }
                ClearHints();
            }
        }

#if RIMWORLD__1_6
        [HarmonyPatch(typeof(FloatMenuOptionProvider_Deathrest), "GetSingleOptionFor")]
        public class Patch_FloatMenuOptionProvider_Deathrest_GetSingleOptionFor {
            static void Postfix(FloatMenuOptionProvider_Deathrest __instance, ref FloatMenuOption __result, Thing clickedThing, FloatMenuContext context) {
                if (!BedOwnershipTools.Singleton.settings.enableSpareDeathrestBindings) {
                    return;
                }
                Building_Bed bed = clickedThing as Building_Bed;
                if (bed == null || !bed.def.building.bed_humanlike) {
                    return;
                }
		        CompDeathrestBindable compDeathrestBindable = bed.GetComp<CompDeathrestBindable>();
                Gene_Deathrest gene_Deathrest = context.FirstSelectedPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
                CompDeathrestBindableXAttrs cdbXAttrs = bed.GetComp<CompDeathrestBindableXAttrs>();
                if (compDeathrestBindable == null || gene_Deathrest == null || cdbXAttrs == null) {
                    return;
                } else if (cdbXAttrs.boundPawnOverlay != null && cdbXAttrs.boundPawnOverlay != context.FirstSelectedPawn) {
                    __result = new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "CannotAssignAlreadyBound".Translate(cdbXAttrs.boundPawnOverlay), null);
                    return;
                } else if (compDeathrestBindable.BoundPawn == null && (cdbXAttrs.boundPawnOverlay == null || cdbXAttrs.boundPawnOverlay == context.FirstSelectedPawn)) {
                    __result = new FloatMenuOption("StartDeathrest".Translate(), delegate {
                        // BoundComps doesn't appear to populate as expected until the pawn deathrests for the first time after a save reload
                        foreach (ThingWithComps boundBuilding in gene_Deathrest.BoundBuildings) {
                            Patch_CompDeathrestBindable_Notify_DeathrestGeneRemoved.HintDontClearOverlayBindee();
                            boundBuilding.GetComp<CompDeathrestBindable>().Notify_DeathrestGeneRemoved();
                        }
                        gene_Deathrest.BoundBuildings.Clear();
                        // gene_Deathrest.cachedBoundComps = null;
                        // if building is unbound and pawn has binds, warn before issuing job
                        // TODO unclaim other deathrest casket before claiming target deathrest casket
                        Traverse.Create(gene_Deathrest).Field("cachedBoundComps").SetValue(null);
                        Job job = JobMaker.MakeJob(JobDefOf.Deathrest, bed);
                        job.forceSleep = true;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    });
                    return;
                }
            }
        }
#else
// TODO AddHumanlikeOrders
#endif
        // TODO block pawns from taking other pawns bound helper buildings
        // TryLinkToNearbyDeathrestBuildings

        // TODO assignment dialog conflicts
    }
}
