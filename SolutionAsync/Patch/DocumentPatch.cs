using System.Collections.Generic;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using HarmonyLib;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(GH_Document))]
internal class DocumentPatch
{
    internal static readonly List<GH_Document> _addedDocuments = new();

    private static readonly List<GH_Document> _runningDocs = new();
    private static readonly List<GH_Document> _calculatingDocs = new();

    [HarmonyPatch(nameof(GH_Document.NewSolution), typeof(bool), typeof(GH_SolutionMode))]
    private static bool Prefix(GH_Document __instance, bool expireAllObjects, GH_SolutionMode mode)
    {
        if (_addedDocuments.Contains(__instance))
        {
            _addedDocuments.Remove(__instance);
            return false;
        }

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
                    Instances.ActiveCanvas.Invoke(() => { Instances.ActiveCanvas.ModifiersEnabled = true; });
                _calculatingDocs.Remove(__instance);
                _runningDocs.Remove(__instance);
            }
        });

        return false;
    }

    [HarmonyPatch(nameof(GH_Document.NewSolution), typeof(bool), typeof(GH_SolutionMode))]
    private static void Postfix(GH_Document __instance, ref GH_ProcessStep ____state)
    {
        if (__instance.AbortRequested) ____state = GH_ProcessStep.Aborted;
    }

    [HarmonyPatch("SolveAllObjects")]
    private static bool Prefix(GH_Document __instance, GH_SolutionMode mode, ref GH_ProcessStep ____state,
        ref int ___m_solutionIndex)
    {
        if (!Data.UseSolutionAsync) return true;

        ____state = GH_ProcessStep.PostProcess;

        ___m_solutionIndex = -1;
        foreach (var item in CalculateItem.Create(__instance))
        {
            if (GH_Document.IsEscapeKeyDown()) __instance.RequestAbortSolution();
            if (__instance.AbortRequested) break;
            ___m_solutionIndex = __instance.Objects.IndexOf(item.Items[0]);
            item.Solve(mode);
        }

        return false;
    }
}