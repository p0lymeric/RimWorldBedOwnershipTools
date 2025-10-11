using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace BedOwnershipTools {
    public class BedOwnershipTools : Mod {
        public static BedOwnershipTools Singleton = null;
        public ModSettingsImpl settings = null;
        public ModInteropMarshal modInteropMarshal = null;
        public Harmony harmony = null;

        // RimWorld's "Assembly-CSharp" assembly is loaded and entered.
        //   - All of RimWorld's statically compiled code have linked into the CLR past this point
        // Control reaches Verse.PlayDataLoader.DoPlayLoad()
        // Control reaches Verse.LoadedModManager.LoadAllActiveMods()
        //   1. Lists of assembly files for enabled mods are collected by InitializeMods()
        //   2. All mod assemblies are loaded by LoadModContent()
        //     - All loaded mods' static code are linked into the CLR
        //     * including BedOwnershipTools.dll *
        //   3. All "Mod" subclasses are instantiated by CreateModClasses()
        //     - All loaded mods' Mod subclass constructors are executed in UI load order, defined by LoadedModManager.RunningMods
        //     * The bulk of Bed Ownership Tools is initialized in this phase *
        //         a) mod settings are retrieved
        //         b) ModInteropMarshal queries mod settings and the game's environment to decide which interop patch sets to qualify for application.
        //         c) HarmonyPatches applies all patches against "Assembly-CSharp" and scans ModInteropMarshal to apply all qualified patch sets.
        //   4. New defs are loaded by LoadModXML()/CombineIntoUnifiedXML()
        //   5. Patches against the def database are applied by ApplyPatches()
        //     - XML-defined patches are applied
        //         - Application order is defined by LoadedModManager.RunningMods (though unsure about intra/inter-file ordering within one mod)
        //         * Bed Ownership Tools' XML patches are applied in this phase *
        // Control returns to Verse.PlayDataLoader.DoPlayLoad()
        //   1. An initial phase of code-driven def generation/modification is applied during GenerateImpliedDefs_PreResolve()
        //     * Bed Ownership Tools installs hooks on this phase to apply def patches that depend on ModInteropMarshal qualification *

        public BedOwnershipTools(ModContentPack content) : base(content) {
            if (BedOwnershipTools.Singleton is not null) {
                Log.Error("[BOT] Game tried to initialize mod multiple times!");
            }
            BedOwnershipTools.Singleton = this;

            this.settings = GetSettings<ModSettingsImpl>();

            this.modInteropMarshal = new ModInteropMarshal(settings);

            harmony = new("polymeric.bedownershiptools");
            HarmonyPatches.ApplyHarmonyPatches(this);

            if (Prefs.DevMode && settings.devEnableUnaccountedCaseLogging) {
                Log.Message(this.modInteropMarshal.EmitReport());
            }
        }

        public override void DoSettingsWindowContents(Rect inRect) {
            const float MIDDLE_MARGIN = 8f;
            Rect rectLeftCol = new(inRect.x, inRect.y, inRect.width / 2 - MIDDLE_MARGIN, inRect.height);
            Rect rectRightCol = new(inRect.x + inRect.width / 2 + MIDDLE_MARGIN, inRect.y, inRect.width / 2 - MIDDLE_MARGIN, inRect.height);

            Listing_Standard listingStandard = new();

            listingStandard.Begin(rectLeftCol);
            listingStandard.Label("BedOwnershipTools.CommunalBedsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableCommunalBeds".Translate(),
                ref settings.enableCommunalBeds,
                "BedOwnershipTools.EnableCommunalBeds_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.CommunalBedsSupportOrderedMedicalSleep".Translate(),
                ref settings.communalBedsSupportOrderedMedicalSleep,
                "BedOwnershipTools.CommunalBedsSupportOrderedMedicalSleep_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.CommunalBedsAreRelationshipAware".Translate(),
                ref settings.communalBedsAreRelationshipAware,
                "BedOwnershipTools.CommunalBedsAreRelationshipAware_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AssignmentPinningHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableBedAssignmentPinning".Translate(),
                ref settings.enableBedAssignmentPinning,
                "BedOwnershipTools.EnableBedAssignmentPinning_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.PawnsMaySelfAssignToUnownedPinnedBeds".Translate(),
                ref settings.pawnsMaySelfAssignToUnownedPinnedBeds,
                "BedOwnershipTools.PawnsMaySelfAssignToUnownedPinnedBeds_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AssignmentGroupsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableBedAssignmentGroups".Translate(),
                ref settings.enableBedAssignmentGroups,
                "BedOwnershipTools.EnableBedAssignmentGroups_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.DeathrestBindingsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableSpareDeathrestBindings".Translate(),
                ref settings.enableSpareDeathrestBindings,
                "BedOwnershipTools.EnableSpareDeathrestBindings_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.DeathrestBindingsArePermanent".Translate(),
                ref settings.deathrestBindingsArePermanent,
                "BedOwnershipTools.DeathrestBindingsArePermanent_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.AutomaticDeathrestHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableAutomaticDeathrest".Translate(),
                ref settings.enableAutomaticDeathrest,
                "BedOwnershipTools.EnableAutomaticDeathrest_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.IgnoreBedsForAutomaticDeathrest".Translate(),
                ref settings.ignoreBedsForAutomaticDeathrest,
                "BedOwnershipTools.IgnoreBedsForAutomaticDeathrest_Tooltip".Translate()
            );
            listingStandard.End();

            listingStandard = new();
            listingStandard.Begin(rectRightCol);
            listingStandard.Label("BedOwnershipTools.UICustomizationsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowCommunalGUIOverlayInsteadOfBlankUnderBed".Translate(),
                ref settings.showCommunalGUIOverlayInsteadOfBlankUnderBed,
                "BedOwnershipTools.ShowCommunalGUIOverlayInsteadOfBlankUnderBed_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.HideGUIOverlayOnNonHumanlikeBeds".Translate(),
                ref settings.hideGUIOverlayOnNonHumanlikeBeds,
                "BedOwnershipTools.HideGUIOverlayOnNonHumanlikeBeds_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowColonistsAcrossAllMapsInAssignmentDialog".Translate(),
                ref settings.showColonistsAcrossAllMapsInAssignmentDialog,
                "BedOwnershipTools.ShowColonistsAcrossAllMapsInAssignmentDialog_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.HideDeathrestAutoControlsOnPawnWhileAwake".Translate(),
                ref settings.hideDeathrestAutoControlsOnPawnWhileAwake,
                "BedOwnershipTools.HideDeathrestAutoControlsOnPawnWhileAwake_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.ShowDeathrestAutoControlsOnCasket".Translate(),
                ref settings.showDeathrestAutoControlsOnCasket,
                "BedOwnershipTools.ShowDeathrestAutoControlsOnCasket_Tooltip".Translate()
            );

            listingStandard.GapLine();
            listingStandard.Label("BedOwnershipTools.ModCompatibilityHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Hospitality"),
                ref settings.enableHospitalityModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("One bed to sleep with all - Polycule Edition"),
                ref settings.enableOneBedToSleepWithAllModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Loft Bed"),
                ref settings.enableLoftBedModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Bunk Beds"),
                ref settings.enableBunkBedsModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("MultiFloors"),
                ref settings.enableMultiFloorsModCompatPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );
            listingStandard.CheckboxLabeled(
                "BedOwnershipTools.EnableModCompatPatches".Translate("Vanilla Races Expanded - Android"),
                ref settings.enableVanillaRacesExpandedAndroidPatches,
                "BedOwnershipTools.EnableModCompatPatches_Tooltip".Translate()
            );

            listingStandard.GapLine();
            if (listingStandard.ButtonText("BedOwnershipTools.ResetSettingsButton".Translate())) {
                settings.Reset();
            }

            if (Prefs.DevMode) {
                listingStandard.GapLine();
                listingStandard.Label("BedOwnershipTools.DeveloperSettingsHeading".Translate().Colorize(ColoredText.SubtleGrayColor));
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableDebugInspectStringListings".Translate(),
                    ref settings.devEnableDebugInspectStringListings,
                    "BedOwnershipTools.DevEnableDebugInspectStringListings_Tooltip".Translate()
                );
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableUnaccountedCaseLogging".Translate(),
                    ref settings.devEnableUnaccountedCaseLogging,
                    "BedOwnershipTools.DevEnableUnaccountedCaseLogging_Tooltip".Translate()
                );
                listingStandard.CheckboxLabeled(
                    "BedOwnershipTools.DevEnableExtraMenusAndGizmos".Translate(),
                    ref settings.devEnableExtraMenusAndGizmos,
                    "BedOwnershipTools.DevEnableExtraMenusAndGizmos_Tooltip".Translate()
                );
                if (listingStandard.ButtonText("BedOwnershipTools.ResetDeveloperSettingsButton".Translate())) {
                    settings.ResetDev();
                }
            }
            listingStandard.End();
        }

        public override string SettingsCategory() {
            return "Bed Ownership Tools";
        }

        public override void WriteSettings() {
            base.WriteSettings();
            // TODO rerun Harmony patches, but need to first check if repatching will cause any issues wrt patch ordering
            if (Current.ProgramState == ProgramState.Playing && GameComponent_AssignmentGroupManager.Singleton != null) {
                GameComponent_AssignmentGroupManager.Singleton.Notify_WriteSettings();
            }
        }
    }
}
