using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using HarmonyLib;
using System.Drawing;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(GH_ComponentAttributes))]
internal class ComponentAttributePatch
{
    [HarmonyPatch("Render")]
    static void Postfix(GH_ComponentAttributes __instance, GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        if (canvas.ModifiersEnabled) return;
        if (channel != GH_CanvasChannel.Overlay) return;
        if (!Data.RefreshEveryLevelDuringAsync) return;

        var component = __instance.Owner;
        if (component == null) return;

        var color = component.Phase switch
        {
            GH_SolutionPhase.Computed or GH_SolutionPhase.Computing => Color.DarkGreen,
            GH_SolutionPhase.Failed => Color.DarkRed,
            _ => Color.DarkOrange,
        };

        string showStr = component.Phase.ToString();

        if(component.Phase == GH_SolutionPhase.Computing)
        {
            if (component.RunCount >= 0)
            {
                showStr += $" {component.RunCount} {(component.RunCount == 1 ? "time" : "times")}.";
            }
            else
            {
                showStr += "...";
            }
        }

        graphics.DrawString(showStr, GH_FontServer.StandardBold, new SolidBrush(color),
            new PointF(__instance.Bounds.Left, __instance.Bounds.Bottom));
    }
}
