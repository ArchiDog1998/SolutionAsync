using Grasshopper;
using HarmonyLib;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(Instances))]
internal class InstancesPatch
{
    [HarmonyPatch(nameof(Instances.RedrawAll))]
    static bool Prefix()
    {
        Instances.DocumentEditor.Invoke(() =>
        {
            Instances.RedrawCanvas();
            Instances.ActiveRhinoDoc?.Views.Redraw();
        });
        return false;
    }
}
