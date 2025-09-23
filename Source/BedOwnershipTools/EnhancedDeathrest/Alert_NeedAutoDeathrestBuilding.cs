using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BedOwnershipTools {
    public class Alert_NeedAutoDeathrestBuilding : Alert {
        public List<GlobalTargetInfo> targets = new();
        public List<string> targetLabels = new();

        // public long lastCalculatedTick = -1L;

        // public const int CALCULATION_INTERVAL = 250;

        public Alert_NeedAutoDeathrestBuilding() {
            requireBiotech = true;
        }

        public override string GetLabel() {
            if (targets.Count == 1) {
                return "BedOwnershipTools.AlertNeedAutoDeathrestBuildingPawn".Translate(targetLabels[0].Named("PAWN"));
            }
            return "BedOwnershipTools.AlertNeedAutoDeathrestBuildingPawns".Translate(targetLabels.Count.ToStringCached().Named("NUMCULPRITS"));
        }

        private void CalculateTargets() {
            targets.Clear();
            targetLabels.Clear();
#if RIMWORLD__1_6
                foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned) {
#else
                foreach (Pawn item in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive) {
#endif
                if (item.RaceProps.Humanlike && item.Faction == Faction.OfPlayer) { // ordering this check first significantly limits cost with farm animals
                    Need_Deathrest need_Deathrest = item.needs?.TryGetNeed<Need_Deathrest>();
                    if (need_Deathrest != null && !item.Deathresting) {
                        CompPawnXAttrs pawnXAttrs = item.GetComp<CompPawnXAttrs>();
                        if (pawnXAttrs != null && pawnXAttrs.automaticDeathrestTracker.ScheduleTest()) {
                            if (JobGiver_AutomaticDeathrest.TryFindBedOrDeathrestCasket(item) == null) {
                                targets.Add(item);
                                targetLabels.Add(item.NameShortColored.Resolve());
                            }
                        }
                    }
                }
            }
        }

        public override TaggedString GetExplanation() {
            NamedArgument anAcceptableDeathrestBuilding = (BedOwnershipTools.Singleton.settings.ignoreBedsForAutomaticDeathrest ?
                "BedOwnershipTools.AnAssignedDeathrestCasket" :
                "BedOwnershipTools.AnAssignedBedOrDeathrestCasket"
            ).Translate().Named("ANACCEPTABLEDEATHRESTBUILDING");
            NamedArgument culprits = targetLabels.ToLineList("  - ").Named("CULPRITS");
            return "BedOwnershipTools.AlertNeedAutoDeathrestBuildingDesc".Translate(anAcceptableDeathrestBuilding, culprits);
        }

        public override AlertReport GetReport() {
            if (!BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                return false;
            }
            // long currentTick = Find.TickManager.TicksGame;
            // if (lastCalculatedTick < 0 || (currentTick - lastCalculatedTick) > CALCULATION_INTERVAL) {
                CalculateTargets();
                // lastCalculatedTick = currentTick;
            // }
            return AlertReport.CulpritsAre(targets);
        }
    }
}