using System.Collections.Generic;
using RimWorld;
using Verse;
using HarmonyLib;
using RimWorld.Planet;

// Summary of patches
// Automatic deathrest
// - Notifies a Pawn's automatic deathrest tracker when deathrest has finished (Hediff_Deathrest.PostRemoved)
// - Notifies a Pawn's automatic deathrest tracker if a Pawn loses their deathrest gene (Gene_Deathrest.Reset)
// - Modifies time until the low deathrest alert when an automatic deathrest schedule is chosen

namespace BedOwnershipTools {
    public static partial class HarmonyPatches {
        [HarmonyPatch(typeof(Hediff_Deathrest), nameof(Hediff_Deathrest.PostRemoved))]
        public class Patch_Hediff_Deathrest_PostRemoved {
            static void Postfix(Hediff_Deathrest __instance) {
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    pawnXAttrs.automaticDeathrestTracker.Notify_DeathrestEnded();
                }
            }
        }

        [HarmonyPatch(typeof(Gene_Deathrest), nameof(Gene_Deathrest.Reset))]
        public class Patch_Gene_Deathrest_Reset {
            static void Postfix(Gene_Deathrest __instance) {
                CompPawnXAttrs pawnXAttrs = __instance.pawn.GetComp<CompPawnXAttrs>();
                if (pawnXAttrs != null) {
                    pawnXAttrs.automaticDeathrestTracker.Notify_DeathrestGeneRemoved();
                }
            }
        }

        [HarmonyPatch(typeof(Alert_LowDeathrest), "CalculateTargets")]
        public class Patch_Alert_LowDeathrest_CalculateTargets {
            static bool Prefix(Alert_LowDeathrest __instance, List<GlobalTargetInfo> ___targets, List<string> ___targetLabels) {
                if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                    return true;
                }
                ___targets.Clear();
                ___targetLabels.Clear();
#if RIMWORLD__1_6
                foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned) {
#else
                foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive) {
#endif
                    if (item.RaceProps.Humanlike && item.Faction == Faction.OfPlayer) { // ordering this check first significantly limits cost with farm animals
                        Need_Deathrest need_Deathrest = item.needs?.TryGetNeed<Need_Deathrest>();
                        if (need_Deathrest != null) {
                            CompPawnXAttrs pawnXAttrs = item.GetComp<CompPawnXAttrs>();
                            float ticksToLowDeathrestAlert = 0.1f;
                            if (pawnXAttrs != null) {
                                ticksToLowDeathrestAlert = pawnXAttrs.automaticDeathrestTracker.automaticDeathrestMode.LowDeathrestAlertLevel();
                            }
                            if (need_Deathrest.CurLevel <= ticksToLowDeathrestAlert && !item.Deathresting) {
                                ___targets.Add(item);
                                ___targetLabels.Add(item.NameShortColored.Resolve());
                            }
                        }
                    }
                }
                return false;
            }
        }
    }
}
