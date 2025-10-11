using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

// Aggregates ModInterop objects, which contain a set of Harmony patches against an external mod
// and provides a remote call interface to that mod's internal methods.

// Inside each ModInterop implementation, we perform field lookups and safety checks
// to ensure patches and remote calls can be applied to the current installed version of the mod.

namespace BedOwnershipTools {
    public class ModInteropMarshal {
        public List<ModInterop> modInteropList;

        public ModInterop_Hospitality modInterop_Hospitality;
        public ModInterop_OneBedToSleepWithAll modInterop_OneBedToSleepWithAll;
        public ModInterop_LoftBed modInterop_LoftBed;
        public ModInterop_BunkBeds modInterop_BunkBeds;
        public ModInterop_LoftBedBunkBeds modInterop_LoftBedBunkBeds;
        public ModInterop_MultiFloors modInterop_MultiFloors;
        public ModInterop_VanillaRacesExpandedAndroid modInterop_VanillaRacesExpandedAndroid;

        public ModInteropMarshal(ModSettingsImpl settings) {
            modInteropList = new();

            modInterop_Hospitality = new(settings.enableHospitalityModCompatPatches);
            modInteropList.Add(modInterop_Hospitality);

            modInterop_OneBedToSleepWithAll = new(settings.enableOneBedToSleepWithAllModCompatPatches);
            modInteropList.Add(modInterop_OneBedToSleepWithAll);

            modInterop_LoftBed = new(settings.enableLoftBedModCompatPatches);
            modInteropList.Add(modInterop_LoftBed);

            modInterop_BunkBeds = new(settings.enableBunkBedsModCompatPatches);
            modInteropList.Add(modInterop_BunkBeds);

            modInterop_LoftBedBunkBeds = new(modInterop_LoftBed.qualified || modInterop_BunkBeds.qualified);
            modInteropList.Add(modInterop_LoftBedBunkBeds);

            modInterop_MultiFloors = new(settings.enableMultiFloorsModCompatPatches);
            modInteropList.Add(modInterop_MultiFloors);

            modInterop_VanillaRacesExpandedAndroid = new(settings.enableVanillaRacesExpandedAndroidPatches);
            modInteropList.Add(modInterop_VanillaRacesExpandedAndroid);
        }

        public string EmitReport() {
            StringBuilder stringBuilder = new();
            stringBuilder.Append("[BOT] Mod compatibility patching report:");
            foreach (ModInterop modInterop in modInteropList) {
                stringBuilder.AppendInNewLine(modInterop.GetType().Name);
                stringBuilder.Append(": ");
                stringBuilder.Append(modInterop.enabled ? "E" : "e");
                stringBuilder.Append(modInterop.detected ? "D" : "d");
                stringBuilder.Append(modInterop.qualified ? "Q" : "q");
                stringBuilder.Append(modInterop.active ? "A" : "a");
            }
            return stringBuilder.ToString();
        }
    }
}
