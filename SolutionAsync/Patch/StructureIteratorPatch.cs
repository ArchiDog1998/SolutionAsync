using System;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using HarmonyLib;

namespace SolutionAsync.Patch;

internal static class StructureIteratorPatch
{
    private static readonly TypeInfo StructureIteratorType =
        typeof(GH_Component).GetTypeInfo().GetDeclaredNestedType("GH_StructureIterator");

    private static readonly FieldInfo DocInfo = AccessTools.Field(StructureIteratorType, "m_document");

    private static DateTime lastUpdate = DateTime.MinValue;

    public static void Patch(Harmony harmony)
    {
        var method = StructureIteratorType.GetDeclaredProperty("AbortSolution").GetMethod;
        harmony.Patch(method, new HarmonyMethod(Prefix));
    }

    private static bool Prefix(IGH_DataAccess __instance, ref bool __result)
    {
        UpdateDraw();

        if (DocInfo.GetValue(__instance) is not GH_Document doc)
            return true;

        if (doc.AbortRequested)
        {
            __result = true;
            return false;
        }

        return true;
    }

    private static void UpdateDraw()
    {
        if (DateTime.Now - lastUpdate <= TimeSpan.FromMilliseconds(200)) return;
        lastUpdate = DateTime.Now;

        Instances.RedrawAll();
    }
}