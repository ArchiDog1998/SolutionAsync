using Grasshopper;
using Grasshopper.Kernel;
using HarmonyLib;
using System;
using System.Reflection;

namespace SolutionAsync.Patch;
internal static class StructureIteratorPatch
{
    static readonly TypeInfo StructureIteratorType = typeof(GH_Component).GetTypeInfo().GetDeclaredNestedType("GH_StructureIterator");
    static readonly FieldInfo DocInfo = AccessTools.Field(StructureIteratorType, "m_document");

    static DateTime lastUpdate = DateTime.MinValue;
    public static void Patch(Harmony harmony)
    {
        var method = StructureIteratorType.GetDeclaredProperty("AbortSolution").GetMethod;
        harmony.Patch(method, new HarmonyMethod(Prefix));
    }

    static bool Prefix(IGH_DataAccess __instance, ref bool __result)
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

    static void UpdateDraw()
    {
        if (DateTime.Now - lastUpdate <= TimeSpan.FromMilliseconds(200)) return;
        lastUpdate = DateTime.Now;

        Instances.RedrawAll();

    }
}
