using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace BedOwnershipTools {
    public enum AutomaticDeathrestMode {
        Manual,
        Exhaustion1Hour,
        Exhaustion1Day,
        Exhaustion3Days,
        CalendarAprimaySeptober1To5,
        CalendarAprimaySeptober6To10,
        CalendarAprimaySeptober11To15,
        CalendarJugustDecembary1To5,
        CalendarJugustDecembary6To10,
        CalendarJugustDecembary11To15,
    }

    public enum AutomaticDeathrestScheduleDiscpline {
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
            return (IEnumerable<AutomaticDeathrestMode>)Enum.GetValues(typeof(AutomaticDeathrestMode));
        }

        public static string LabelString(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => "BedOwnershipTools.ScheduleManual".Translate(),
                AutomaticDeathrestMode.Exhaustion1Hour => "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("Period1Hour".Translate()),
                AutomaticDeathrestMode.Exhaustion1Day => "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("Period1Day".Translate()),
                AutomaticDeathrestMode.Exhaustion3Days => "BedOwnershipTools.ScheduleTimePeriodBeforeExhaustion".Translate("PeriodDays".Translate(3)),
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => $"{BiannualDateRangeStringAt(1, 5, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => $"{BiannualDateRangeStringAt(6, 10, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => $"{BiannualDateRangeStringAt(11, 15, Quadrum.Aprimay, Quadrum.Septober)}",
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => $"{BiannualDateRangeStringAt(1, 5, Quadrum.Jugust, Quadrum.Decembary)}",
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => $"{BiannualDateRangeStringAt(6, 10, Quadrum.Jugust, Quadrum.Decembary)}",
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => $"{BiannualDateRangeStringAt(11, 15, Quadrum.Jugust, Quadrum.Decembary)}",
                _ => "INVALID"
            };
        }

        public static Texture2D Texture(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => Widgets.CheckboxOffTex,
                AutomaticDeathrestMode.Exhaustion1Hour => AnytimeRitualTex.Texture,
                AutomaticDeathrestMode.Exhaustion1Day => AnytimeRitualTex.Texture,
                AutomaticDeathrestMode.Exhaustion3Days => AnytimeRitualTex.Texture,
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => DateRitualTex.Texture,
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => DateRitualTex.Texture,
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => DateRitualTex.Texture,
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => DateRitualTex.Texture,
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => DateRitualTex.Texture,
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => DateRitualTex.Texture,
                _ => null
            };
        }

        public static AutomaticDeathrestScheduleDiscpline Discipline(this AutomaticDeathrestMode automaticDeathrestMode) {
            return automaticDeathrestMode switch {
                AutomaticDeathrestMode.Manual => AutomaticDeathrestScheduleDiscpline.Manual,
                AutomaticDeathrestMode.Exhaustion1Hour => AutomaticDeathrestScheduleDiscpline.Exhaustion,
                AutomaticDeathrestMode.Exhaustion1Day => AutomaticDeathrestScheduleDiscpline.Exhaustion,
                AutomaticDeathrestMode.Exhaustion3Days => AutomaticDeathrestScheduleDiscpline.Exhaustion,
                AutomaticDeathrestMode.CalendarAprimaySeptober1To5 => AutomaticDeathrestScheduleDiscpline.Calendar,
                AutomaticDeathrestMode.CalendarAprimaySeptober6To10 => AutomaticDeathrestScheduleDiscpline.Calendar,
                AutomaticDeathrestMode.CalendarAprimaySeptober11To15 => AutomaticDeathrestScheduleDiscpline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary1To5 => AutomaticDeathrestScheduleDiscpline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary6To10 => AutomaticDeathrestScheduleDiscpline.Calendar,
                AutomaticDeathrestMode.CalendarJugustDecembary11To15 => AutomaticDeathrestScheduleDiscpline.Calendar,
                _ => AutomaticDeathrestScheduleDiscpline.Manual
            };
        }
    }
}
