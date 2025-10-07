using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    // NOTE while this class was initially named the AGM/AssignmentGroupManager, it now serves as a general supervisor of submanagers (AGMCompartments).
    // The logic that manages assignment groups has been compartmentalized into "AGMCompartment_AssignmentGroups".
    // We won't rename or introduce more GameComponents to avoid creating problems associated with save data migration.
    public class GameComponent_AssignmentGroupManager : GameComponent {
        public static GameComponent_AssignmentGroupManager Singleton;

        // Tracks the version of the mod used when the game was last saved.
        // Currently unused but could be useful for future save data migration routines
        // Only meant to be used during FinalizeInit, after which the string is replaced with the current mod version string
        // If the game is new, the version string is empty
        // If the game was loaded from a pre-1.1.0 save, mod version is represented with a fallback version of 0.1.0
        private string modVersionOnSave;

        // Tracks all live ThingComps introduced by this mod for enumeration purposes
        public HashSet<CompPawnXAttrs> compPawnXAttrsRegistry;
        public HashSet<CompBuilding_BedXAttrs> compBuilding_BedXAttrsRegistry;
        public HashSet<CompDeathrestBindableXAttrs> compDeathrestBindableXAttrsRegistry;

        // Management compartments
        public AGMCompartment_AssignmentGroups agmCompartment_AssignmentGroups;
        public AGMCompartment_HarmonyPatchState agmCompartment_HarmonyPatchState;
        public AGMCompartment_SpareDeathrestBindings agmCompartment_SpareDeathrestBindings;
        public AGMCompartment_AutomaticDeathrest agmCompartment_AutomaticDeathrest;
        public AGMCompartment_JobDriverTargetBedLUT agmCompartment_JobDriverTargetBedLUT;

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

            modVersionOnSave = "";

            compPawnXAttrsRegistry = new();
            compBuilding_BedXAttrsRegistry = new();
            compDeathrestBindableXAttrsRegistry = new();

            this.agmCompartment_AssignmentGroups = new(game, this);
            this.agmCompartment_HarmonyPatchState = new(game, this);
            this.agmCompartment_SpareDeathrestBindings = new(game, this);
            this.agmCompartment_AutomaticDeathrest = new(game, this);
            this.agmCompartment_JobDriverTargetBedLUT = new(game, this);
        }

        public void Notify_WriteSettings() {
            agmCompartment_AssignmentGroups.Notify_WriteSettings();
            agmCompartment_SpareDeathrestBindings.Notify_WriteSettings();
            agmCompartment_AutomaticDeathrest.Notify_WriteSettings();
        }

        public override void FinalizeInit() {
            // if (modVersionOnSave == "") {
            //     Log.Message($"[BOT] Bed Ownership Tools save data was freshly initialized.");
            // } else {
            //     Log.Message($"[BOT] Loaded save from Bed Ownership Tools version {modVersionOnSave}.");
            //     Log.Message($"[BOT] Current Bed Ownership Tools version is {BedOwnershipTools.Singleton.Content.ModMetaData.ModVersion}.");
            // }

            agmCompartment_AssignmentGroups.FinalizeInit();
            agmCompartment_SpareDeathrestBindings.FinalizeInit();
            agmCompartment_AutomaticDeathrest.FinalizeInit();

            modVersionOnSave = BedOwnershipTools.Singleton.Content.ModMetaData.ModVersion;
        }

        public override void ExposeData() {
            base.ExposeData();
            Scribe_Values.Look(ref this.modVersionOnSave, "modVersionOnSave", "0.1.0");
            agmCompartment_AssignmentGroups.ShallowExposeData();
        }
    }
}
