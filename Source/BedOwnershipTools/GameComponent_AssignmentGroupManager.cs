using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    // NOTE while this class was initially named the AGM/AssignmentGroupManager, it now serves as a general supervisor of submanagers (AGMCompartments).
    // The logic that manages assignment groups has been compartmentalized into "AGMCompartment_AssignmentGroups".
    // We won't rename or introduce more GameComponents to avoid creating problems associated with save data migration.
    public class GameComponent_AssignmentGroupManager : GameComponent {
        public static GameComponent_AssignmentGroupManager Singleton;

        // Tracks all live ThingComps introduced by this mod for enumeration purposes
        public HashSet<CompPawnXAttrs> compPawnXAttrsRegistry;
        public HashSet<CompBuilding_BedXAttrs> compBuilding_BedXAttrsRegistry;

        // Management compartments
        public AGMCompartment_AssignmentGroups agmCompartment_AssignmentGroups;
        public AGMCompartment_HarmonyPatchState agmCompartment_HarmonyPatchState;

        // Observed execution order
        // New game
        // GameComponent ctor ->
        // Pawn ctor ->
        // GC FinalizeInit ->
        // GC StartedNewGame
        //
        // Loaded game (mod newly added)
        // GameComponent ctor ->
        // (Pawn ctor -> Pawn PostExposeDataLoadingVars) ->
        // Pawn PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataPostLoadInit ->
        // GC FinalizeInit ->
        // GC LoadedGame
        //
        // Loaded game (mod was previously initialized)
        // GameComponent ctor ->
        // GC ExposeDataLoadingVars ->
        // (Pawn ctor -> Pawn PostExposeDataLoadingVars) ->
        // GC PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataResolvingCrossRefs ->
        // Pawn PostExposeDataPostLoadInit ->
        // GC PostExposeDataPostLoadInit ->
        // GC FinalizeInit ->
        // GC LoadedGame

        public GameComponent_AssignmentGroupManager(Game game) {
            Singleton = this;

            compPawnXAttrsRegistry = new();
            compBuilding_BedXAttrsRegistry = new();

            this.agmCompartment_AssignmentGroups = new(game, this);
            this.agmCompartment_HarmonyPatchState = new(game, this);
        }

        public void Notify_WriteSettings() {
            agmCompartment_AssignmentGroups.Notify_WriteSettings();
        }

        public override void FinalizeInit() {
            agmCompartment_AssignmentGroups.FinalizeInit();
        }

        public override void ExposeData() {
		    base.ExposeData();
            agmCompartment_AssignmentGroups.ShallowExposeData();
	    }
    }
}
