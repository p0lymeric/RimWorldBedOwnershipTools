using RimWorld;
using Verse;
using HarmonyLib;

namespace BedOwnershipTools {
    public abstract class ModInterop {
        public bool enabled;
        public bool detected;
        public bool qualified;
        public bool active;

        public ModInterop(bool enabled) {
            this.enabled = enabled;
        }

        public abstract void ApplyHarmonyPatches(Harmony harmony);

        public abstract void Notify_AGMCompartment_HarmonyPatchState_Constructed();
    }
}
