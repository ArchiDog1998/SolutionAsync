using Grasshopper;
using Grasshopper.GUI.Widgets;
using HarmonyLib;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(GH_ProfilerWidget))]
internal class ProfilerWidgetPatch
{
    [HarmonyPatch(nameof(GH_ProfilerWidget.SharedVisible), MethodType.Getter)]
    static bool Prefix(ref bool __result)
    {
        if (Instances.ActiveCanvas.ModifiersEnabled) return true;
        if (!Data.RefreshEveryLevelDuringAsync) return true;
        return __result = false;
    }
}
