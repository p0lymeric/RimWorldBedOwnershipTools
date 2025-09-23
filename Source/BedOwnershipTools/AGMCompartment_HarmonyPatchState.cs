using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class AGMCompartment_HarmonyPatchState : AGMCompartment {
        public AGMCompartment_HarmonyPatchState(Game game, GameComponent_AssignmentGroupManager parent) : base(game, parent) {
            // Clear some static structures that don't technically need to be cleared
            // (to avoid mysterious issues that could occur on subsequent new games/save reloads)
            HarmonyPatches.Patch_CompAssignableToPawn_TryUnassignPawn.ClearHints();
            HarmonyPatches.Patch_Pawn_Ownership_UnclaimBed.ClearHints();
            HarmonyPatches.Patch_Pawn_Ownership_UnclaimDeathrestCasket.ClearHints();
            HarmonyPatches.Patch_CompDeathrestBindable_Notify_DeathrestGeneRemoved.ClearHints();
            HarmonyPatches.ModCompatPatches_LoftBedBunkBeds.JobDriverToLayDownBedTargetIndexCache.Clear();
            HarmonyPatches.ModCompatPatches_LoftBedBunkBeds.JobDriverDevWarningFilter.Clear();
        }
    }
}
