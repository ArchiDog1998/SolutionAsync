using Grasshopper;
using HarmonyLib;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(Instances))]
internal class InstancesPatch
{
    [HarmonyPatch(nameof(Instances.RedrawAll))]
    private static bool Prefix()
    {
        Instances.DocumentEditor.Invoke(() =>
        {
            Instances.RedrawCanvas();
            Instances.ActiveRhinoDoc?.Views.Redraw();
        });
        return false;
    }
}