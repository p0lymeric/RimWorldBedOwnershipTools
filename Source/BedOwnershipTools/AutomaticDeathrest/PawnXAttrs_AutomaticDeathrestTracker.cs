using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BedOwnershipTools {
    public class PawnXAttrs_AutomaticDeathrestTracker {
        public readonly CompPawnXAttrs parent;
        public AutomaticDeathrestMode automaticDeathrestMode = AutomaticDeathrestMode.Manual;
        // set when a Pawn finishes deathrest
        public int tickCompletedLastDeathrest = -1;

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

        public float LongitudeForLocalDateCalc() {
#if RIMWORLD__1_6
            PlanetTile tile = parent.parentPawn.Tile;
            if (tile.Valid) {
#else
            int tile = parent.parentPawn.Tile;
            if (tile >= 0) {
#endif
                return Find.WorldGrid.LongLatOf(tile).x;
            }
            return 0;
        }

        public bool CalendarScheduleTest(Twelfth twelfth1, Twelfth twelfth2) {
            // first check for imminent deathrest exhaustion
            if (ExhaustionScheduleTest(GenDate.TicksPerDay)) {
                return true;
            }

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

            // This scheduler operates in local time, as it's more natural for the common case when a pawn is stationed in some settlement.
            // However, we need to account for time zone changes with travelling Pawns.

            // We want to avoid retriggering deathrest at the beginning of the week in some local time zone, if the pawn recently
            // finished deathrest in the same week in another local time zone.
            // e.g. finishing deathrest on 6 Aprimay 1hT+1h in a T+1h timezone, then travelling to 5 Aprimay 23h in a T-1h timezone.
            //      The scheduler must not retrigger on 6 Aprimay 0hT-1h.

            // Note that a 5-day refractory period probably could've also worked (absTickCompletedLastDeathrest + TicksPerTwelfth < TicksAbs)
            // But we want hysteresis windows to be tight to mitigate risks of a deathrester latching onto the fallback exhaustion schedule in steady state.

            // Time zone conversions suck.
            int localTwelfthAbs = (Find.TickManager.TicksAbs + (int)GenDate.LocalTicksOffsetFromLongitude(LongitudeForLocalDateCalc())) / GenDate.TicksPerTwelfth;
            int tickStartOfLocalTwelfthAnywhereOnPlanet = localTwelfthAbs * GenDate.TicksPerTwelfth - GenDate.TicksPerHour * 12;

            // guaranteed that tickCompletedLastDeathrest contains a valid game tick due to negative check above
            if (GenDate.TickGameToAbs(tickCompletedLastDeathrest) < tickStartOfLocalTwelfthAnywhereOnPlanet) {
                // Log.Message("[BOT] eepy due to calendar");
                return true;
            }
            return false;
        }

        public float TicksToDeathrestExhaustion() {
            const int TICKS_PER_DEATHREST_PERIOD = GenDate.TicksPerDay * 30;
            Need_Deathrest need_Deathrest = parent.parentPawn.needs?.TryGetNeed<Need_Deathrest>();
            if (need_Deathrest != null) {
                return need_Deathrest.CurLevel * TICKS_PER_DEATHREST_PERIOD;
            }
            return TICKS_PER_DEATHREST_PERIOD;
        }

        public void Notify_DeathrestEnded() {
            this.tickCompletedLastDeathrest = Find.TickManager.TicksGame;
            // int tickCompletedLastDeathrest = GenDate.TickGameToAbs(this.tickCompletedLastDeathrest) + (int)GenDate.LocalTicksOffsetFromLongitude(LongitudeForLocalDateCalc());
            // Log.Warning($"tickCompletedLastDeathrest: {tickCompletedLastDeathrest}");
            // Log.Warning($"tickCompletedLastDeathrestT: {GenDate.DayOfQuadrum(tickCompletedLastDeathrest, 0) + 1} {GenDate.Quadrum(tickCompletedLastDeathrest, 0).Label()} {GenDate.Year(tickCompletedLastDeathrest, 0)} {GenDate.HourFloat(tickCompletedLastDeathrest, 0):F1}h LOC");
        }

        public void Notify_DeathrestGeneRemoved() {
            // because if the Pawn was governed by a calendar scheduler and regains the gene, Auto-wake will not be synchronized anymore
            this.automaticDeathrestMode = AutomaticDeathrestMode.Manual;
            // not necessary but prudent to reset
            this.tickCompletedLastDeathrest = -1;
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
            Scribe_Values.Look(ref tickCompletedLastDeathrest, "BedOwnershipTools_tickCompletedLastDeathrest", -1);
        }
    }
}
