using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public enum AutomaticDeathrestMode {
        Manual,
        Exhaustion1Hour,
        Exhaustion3Hours,
        Exhaustion1Day,
        Exhaustion3Days,
        CalendarAprimaySeptober1To5,
        CalendarAprimaySeptober6To10,
        CalendarAprimaySeptober11To15,
        CalendarJugustDecembary1To5,
        CalendarJugustDecembary6To10,
        CalendarJugustDecembary11To15,
    }

    public enum AutomaticDeathrestScheduleDiscipline {
        Manual,
        Exhaustion,
        Calendar,
    }

    public static class AutomaticDeathrestModeExtensions {
        private static readonly CachedTexture AnytimeRitualTex = new("BedOwnershipTools/UI/Icons/AnytimeRitual");
        private static readonly CachedTexture DateRitualTex = new("BedOwnershipTools/UI/Icons/DateRitual");

        private static string BiannualDateRangeStringAt(int day1, int day2, Quadrum quadrum1, Quadrum quadrum2) {
            string stringDay1 = Find.ActiveLanguageWorker.OrdinalNumber(day1);
            string stringDay2 = Find.ActiveLanguageWorker.OrdinalNumber(day2);
            return "BedOwnershipTools.ScheduleBiannualDateRange".Translate(stringDay1, stringDay2, quadrum1.Label(), quadrum2.Label(), day1, day2);
        }

        public static IEnumerable<AutomaticDeathrestMode> GetValues() {
            yield return AutomaticDeathrestMode.Manual;
            yield return AutomaticDeathrestMode.Exhaustion3Hours;
            yield return AutomaticDeathrestMode.Exhaustion1Day;
            yield return AutomaticDeathrestMode.CalendarAprimaySeptober1To5;
            yield return AutomaticDeathrestMode.CalendarAprimaySeptober6To10;
            yield return AutomaticDeathrestMode.CalendarAprimaySeptober11To15;
            yield return AutomaticDeathrestMode.CalendarJugustDecembary1To5;
            yield return AutomaticDeathrestMode.CalendarJugustDecembary6To10;
            yield return AutomaticDeathrestMode.CalendarJugustDecembary11To15;

            if (Prefs.DevMode && BedOwnershipTools.Singleton.settings.devEnableExtraMenusAndGizmos) {
                yield return AutomaticDeathrestMode.Exhaustion1Hour;
                yield return AutomaticDeathrestMode.Exhaustion3Days;
            }
        }

        public static string LabelString(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => "BedOwnershipTools.ScheduleManual".Translate(),
                AutomaticDeathrestMode.Exhaustion1Hour => "[DEV] " + "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("Period1Hour".Translate()),
                AutomaticDeathrestMode.Exhaustion3Hours => "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("PeriodHours".Translate(3)),
                AutomaticDeathrestMode.Exhaustion1Day => "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("Period1Day".Translate()),
                AutomaticDeathrestMode.Exhaustion3Days => "[DEV] " + "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("PeriodDays".Translate(3)),
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => $"{BiannualDateRangeStringAt(1, 5, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => $"{BiannualDateRangeStringAt(6, 10, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => $"{BiannualDateRangeStringAt(11, 15, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => $"{BiannualDateRangeStringAt(1, 5, Quadrum.Jugust, Quadrum.Decembary)}",
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => $"{BiannualDateRangeStringAt(6, 10, Quadrum.Jugust, Quadrum.Decembary)}",
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => $"{BiannualDateRangeStringAt(11, 15, Quadrum.Jugust, Quadrum.Decembary)}",
                _ => "INVALID"
            };
        }

        public static AutomaticDeathrestScheduleDiscipline Discipline(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => AutomaticDeathrestScheduleDiscipline.Manual,
                AutomaticDeathrestMode.Exhaustion1Hour => AutomaticDeathrestScheduleDiscipline.Exhaustion,
                AutomaticDeathrestMode.Exhaustion3Hours => AutomaticDeathrestScheduleDiscipline.Exhaustion,
                AutomaticDeathrestMode.Exhaustion1Day => AutomaticDeathrestScheduleDiscipline.Exhaustion,
                AutomaticDeathrestMode.Exhaustion3Days => AutomaticDeathrestScheduleDiscipline.Exhaustion,
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => AutomaticDeathrestScheduleDiscipline.Calendar,
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => AutomaticDeathrestScheduleDiscipline.Calendar,
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => AutomaticDeathrestScheduleDiscipline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => AutomaticDeathrestScheduleDiscipline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => AutomaticDeathrestScheduleDiscipline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => AutomaticDeathrestScheduleDiscipline.Calendar,
                _ => AutomaticDeathrestScheduleDiscipline.Manual
            };
        }

        public static float LowDeathrestAlertLevel(this AutomaticDeathrestMode automaticDeathrestMode) {
            const float DAYS_PER_DEATHREST_PERIOD = 30.0f;
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => 3.0f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.Exhaustion1Hour => 0f,
                AutomaticDeathrestMode.Exhaustion3Hours => 0f,
                AutomaticDeathrestMode.Exhaustion1Day => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.Exhaustion3Days => 1.0f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => 0.5f / DAYS_PER_DEATHREST_PERIOD,
                _ => 3.0f / DAYS_PER_DEATHREST_PERIOD
            };
        }

        public static float IgnoreScheduleActivityAssignmentsBelowLevel(this AutomaticDeathrestMode automaticDeathrestMode) {
            const float DAYS_PER_DEATHREST_PERIOD = 30.0f;
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Exhaustion1Hour => 1.0f / 24.0f / DAYS_PER_DEATHREST_PERIOD,
                AutomaticDeathrestMode.Exhaustion3Hours => 3.0f / 24.0f / DAYS_PER_DEATHREST_PERIOD,
                _ => automaticDeathrestMode.LowDeathrestAlertLevel()
            };
        }

        public static string LabelStringWithColour(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode.Discipline() switch {
                AutomaticDeathrestScheduleDiscipline.Manual => automaticDeathrestMode.LabelString().Colorize(ColoredText.DateTimeColor),
                AutomaticDeathrestScheduleDiscipline.Exhaustion => automaticDeathrestMode.LabelString().Colorize(ColoredText.DateTimeColor),
                AutomaticDeathrestScheduleDiscipline.Calendar => automaticDeathrestMode.LabelString().Colorize(ColoredText.DateTimeColor),
                _ => "INVALID"
            };
        }

        public static string LabelStringDisciplineDescriptionTranslationKey(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode.Discipline() switch {
                AutomaticDeathrestScheduleDiscipline.Manual => "BedOwnershipTools.Command_SetAutomaticDeathrestModeManualScheduleDesc",
                AutomaticDeathrestScheduleDiscipline.Exhaustion => "BedOwnershipTools.Command_SetAutomaticDeathrestModeExhaustionScheduleDesc",
                AutomaticDeathrestScheduleDiscipline.Calendar => "BedOwnershipTools.Command_SetAutomaticDeathrestModeCalendarScheduleDesc",
                _ => "INVALID"
            };
        }

        public static Texture2D Texture(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode.Discipline() switch {
                AutomaticDeathrestScheduleDiscipline.Manual => Widgets.CheckboxOffTex,
                AutomaticDeathrestScheduleDiscipline.Exhaustion => AnytimeRitualTex.Texture,
                AutomaticDeathrestScheduleDiscipline.Calendar => DateRitualTex.Texture,
                _ => null
            };
        }
    }
}
