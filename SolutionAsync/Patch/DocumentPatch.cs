using Grasshopper;
using Grasshopper.Kernel;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(GH_Document))]
internal class DocumentPatch
{
    private static readonly List<GH_Document> _runningDocs = new();
    private static readonly List<GH_Document> _calculatingDocs = new();


    [HarmonyPatch(nameof(GH_Document.NewSolution), typeof(bool), typeof(GH_SolutionMode))]
    static bool Prefix(GH_Document __instance, bool expireAllObjects, GH_SolutionMode mode)
    {
        if (!Data.UseSolutionAsync) return true;
        if (_calculatingDocs.Contains(__instance)) return true;

        if (_runningDocs.Contains(__instance)) return false;
        _runningDocs.Add(__instance);

        var isMain = __instance == Instances.ActiveDocument;
        if (isMain) Instances.ActiveCanvas.ModifiersEnabled = false;

        Task.Run(() =>
        {
            try
            {
                _calculatingDocs.Add(__instance);
                __instance.NewSolution(expireAllObjects, mode);
            }
            finally
            {
                if (isMain)
                {
                    Instances.ActiveCanvas.Invoke(() =>
                    {
                        Instances.ActiveCanvas.ModifiersEnabled = true;
                    });
                }
                _calculatingDocs.Remove(__instance);
                _runningDocs.Remove(__instance);
            }
        });

        return false;
    }

    [HarmonyPatch("SolveAllObjects")]
    static bool Prefix(GH_Document __instance, GH_SolutionMode mode, ref GH_ProcessStep ____state, ref int ___m_solutionIndex, ref bool ___m_abortRequested)
    {
        if (!Data.UseSolutionAsync) return true;

        ____state = GH_ProcessStep.PostProcess;

        ___m_solutionIndex = -1;
        var items = __instance.Objects.OfType<IGH_ActiveObject>();
        foreach (var item in items)
        {
            if (GH_Document.IsEscapeKeyDown())
            {
                ___m_abortRequested = true;
            }
            if (__instance.AbortRequested)
            {
                break;
            }
            ___m_solutionIndex = __instance.Objects.IndexOf(item);
            item.SolveOne(mode, __instance);
        }

        if (__instance.AbortRequested)
        {
            ____state = GH_ProcessStep.Aborted;
        }

        return false;
    }
}
