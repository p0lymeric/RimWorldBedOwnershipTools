using RimWorld;
using Verse;
using HarmonyLib;

// Summary of patches
// Automatic deathrest
// - Notifies a Pawn's automatic deathrest tracker when deathrest has finished (Hediff_Deathrest.PostRemoved)

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
    }
}
