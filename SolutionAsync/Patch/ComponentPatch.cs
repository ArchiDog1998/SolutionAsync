using System;
using System.Collections.Generic;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using HarmonyLib;

namespace SolutionAsync.Patch;

[HarmonyPatch(typeof(GH_Component))]
internal class ComponentPatch
{
    private static readonly List<GH_Component> _originalCallDocs = new();

    [HarmonyPatch(nameof(GH_Component.ComputeData))]
    private static bool Prefix(GH_Component __instance, MethodBase __originalMethod)
    {
        if (!Data.UseSolutionAsync) return true;
        if (_originalCallDocs.Contains(__instance)) return true;

        if (Data.NoAsyncObjects.Contains(__instance.ComponentGuid))
            Instances.ActiveCanvas.Invoke(delegate
            {
                try
                {
                    _originalCallDocs.Add(__instance);
                    __originalMethod.Invoke(__instance, Array.Empty<object>());
                }
                finally
                {
                    _originalCallDocs.Remove(__instance);
                }
            });
        else
            return true;
        //int times = 0;
        //do
        //{
        //    try
        //    {
        //        __originalMethod.Invoke(__instance, Array.Empty<object>());
        //    }
        //    //This Active object can't calculate on task.
        //    catch (InvalidOperationException)
        //    {
        //        Data.NoAsyncObjects.Add(__instance.ComponentGuid);
        //    }
        //    catch (ArgumentOutOfRangeException ex)
        //    {
        //        //Changed two fast.
        //        if (ex.StackTrace.Contains("GH_StructureIterator"))
        //        {
        //            times++;
        //        }
        //        else
        //        {
        //            throw;
        //        }
        //    }
        //}
        //while(times > 0 && times < 10);
        return false;
    }
}