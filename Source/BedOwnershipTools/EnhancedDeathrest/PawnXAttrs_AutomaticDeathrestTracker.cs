using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BedOwnershipTools {
    public class PawnXAttrs_AutomaticDeathrestTracker {
        public readonly CompPawnXAttrs parent;
        public AutomaticDeathrestMode automaticDeathrestMode = AutomaticDeathrestMode.Manual;
        // set when a Pawn finishes deathrest
        public long tickCompletedLastDeathrest = -1L;

        public PawnXAttrs_AutomaticDeathrestTracker(CompPawnXAttrs parent) {
            this.parent = parent;
        }

        public bool ScheduleTest() {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => false,
                AutomaticDeathrestMode.Exhaustion1Hour => ExhaustionScheduleTest(GenDate.TicksPerHour),
                AutomaticDeathrestMode.Exhaustion3Hours => ExhaustionScheduleTest(GenDate.TicksPerHour * 3),
                AutomaticDeathrestMode.Exhaustion1Day => ExhaustionScheduleTest(GenDate.TicksPerDay),
                AutomaticDeathrestMode.Exhaustion3Days => ExhaustionScheduleTest(GenDate.TicksPerDay * 3),
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => CalendarScheduleTest(Twelfth.First, Twelfth.Seventh),
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => CalendarScheduleTest(Twelfth.Second, Twelfth.Eighth),
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => CalendarScheduleTest(Twelfth.Third, Twelfth.Ninth),
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => CalendarScheduleTest(Twelfth.Fourth, Twelfth.Tenth),
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => CalendarScheduleTest(Twelfth.Fifth, Twelfth.Eleventh),
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => CalendarScheduleTest(Twelfth.Sixth, Twelfth.Twelfth),
                _ => false
            };
        }

        public bool ExhaustionScheduleTest(int watermarkTicksToExhaustion) {
            Pawn pawn = parent.parentPawn;
			if (pawn.needs != null) {
                Need_Deathrest need_Deathrest = pawn.needs.TryGetNeed<Need_Deathrest>();
                if (need_Deathrest != null) {
                    const int TICKS_PER_DEATHREST_PERIOD = GenDate.TicksPerDay * 30;
                    float ticksToExhaustion = need_Deathrest.CurLevel * TICKS_PER_DEATHREST_PERIOD;
                    if (ticksToExhaustion <= watermarkTicksToExhaustion) {
                        // Log.Message("[BOT] eepy due to exhaustion");
                        return true;
                    }
                }
			}
            return false;
        }

        public bool CalendarScheduleTest(Twelfth twelfth1, Twelfth twelfth2) {
            // first check for imminent deathrest exhaustion
            if (ExhaustionScheduleTest(GenDate.TicksPerDay)) {
                return true;
            }

            long currentTick = Find.TickManager.TicksGame;

            Twelfth localTwelfthOfYear = GenLocalDate.Twelfth(parent.parent);
            // don't invoke the calendar scheduler if it's the wrong time of year
            if (localTwelfthOfYear != twelfth1 && localTwelfthOfYear != twelfth2) {
                return false;
            }

            // immediately deathrest if the Pawn has never deathrested before
            if (tickCompletedLastDeathrest < 0) {
                // Log.Message("[BOT] eepy due to calendar, never deathrested before");
                return true;
            }

            // now we need to ask "did I already deathrest during this scheduling trigger period in any time zone?"
            // we won't deathrest if we've already deathrested in some time zone that covers the current twelfth

            long tickStartOfTwelfthAnywhereOnPlanet = currentTick / GenDate.TicksPerTwelfth * GenDate.TicksPerTwelfth - GenDate.TicksPerHour * 12L;

            if (tickCompletedLastDeathrest < tickStartOfTwelfthAnywhereOnPlanet) {
                // Log.Message("[BOT] eepy due to calendar");
                return true;
            }
            return false;
        }

        public long estimateNextDeathrestTick() {
            return -1L;
        }

        public void Notify_DeathrestEnded() {
            this.tickCompletedLastDeathrest = Find.TickManager.TicksGame;
        }

        public void Notify_DeathrestGeneRemoved() {
            // because if the Pawn was governed by a calendar scheduler and regains the gene, Auto-wake will not be synchronized anymore
            this.automaticDeathrestMode = AutomaticDeathrestMode.Manual;
            // not necessary but prudent to reset
            this.tickCompletedLastDeathrest = -1L;
        }

        public static IEnumerable<Gizmo> MockCompGetGizmosExtraImpl(bool showAutoWakeControl) {
            if (BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                yield return new Command_SetAutomaticDeathrestMode(null);
            }
            if (showAutoWakeControl) {
                yield return new Command_ToggleAutoWake(null);
            }
        }

        public IEnumerable<Gizmo> CompGetGizmosExtraImpl(bool showAutoWakeControl) {
            Gene_Deathrest gene_Deathrest = parent.parentPawn.genes?.GetFirstGeneOfType<Gene_Deathrest>();
            if (gene_Deathrest != null && gene_Deathrest.Active) {
                if (BedOwnershipTools.Singleton.settings.enableAutomaticDeathrest) {
                    yield return new Command_SetAutomaticDeathrestMode(this.parent);
                }
                if (showAutoWakeControl) {
                    yield return new Command_ToggleAutoWake(this.parent);
                }
            } else {
                // if the Pawn lost their gene and a bound casket wishes to call this by proxy
                foreach (Gizmo x in MockCompGetGizmosExtraImpl(showAutoWakeControl)) {
                    yield return x;
                }
            }
        }

        public void ShallowExposeData() {
            Scribe_Values.Look(ref automaticDeathrestMode, "BedOwnershipTools_automaticDeathrestMode", AutomaticDeathrestMode.Manual);
            Scribe_Values.Look(ref tickCompletedLastDeathrest, "BedOwnershipTools_tickCompletedLastDeathrest", -1L);
	    }
    }
}
