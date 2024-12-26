using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using HarmonyLib;
using Microsoft.VisualBasic.CompilerServices;
using Rhino;
using Rhino.Runtime;
using MethodInvoker = System.Windows.Forms.MethodInvoker;

namespace SolutionAsync;

internal class CalculateItem
{
    private static readonly FieldInfo IconInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblIcon");
    private static readonly FieldInfo NameInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblName");

    private static readonly FieldInfo ExceptionInfo =
        AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblException");

    private static readonly FieldInfo DoNotShowInfo =
        AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_chkDontShow");

    private static readonly FieldInfo IgnoreList = AccessTools.Field(typeof(GH_Document), "m_ignoreList");

    private static readonly Dictionary<IGH_ActiveObject, int> Cache = new();

    private CalculateItem(GH_Document doc, params IGH_ActiveObject[] items)
    {
        Items = items;
        Doc = doc;
    }

    public IGH_ActiveObject[] Items { get; }
    private GH_Document Doc { get; }

    public static IEnumerable<CalculateItem> Create(GH_Document doc)
    {
        var items = doc.Objects.OfType<IGH_ActiveObject>();

        if (!Data.UseSolutionOrderedLevelAsync) return items.Select(i => new CalculateItem(doc, i));

        Cache.Clear();
        var grp = items.GroupBy(GetObjectDepth);
        return grp.OrderBy(i => i.Key)
            .Select(i => new CalculateItem(doc, i.ToArray()));
    }

    private static int GetObjectDepth(IGH_ActiveObject obj)
    {
        if (Cache.TryGetValue(obj, out var depth)) return depth;
        var upStream = GetUpStream(obj);
        if (upStream == null || upStream.Length == 0) return 0;
        return Cache[obj] = upStream.Max(GetObjectDepth) + 1;
    }

    private static IGH_ActiveObject[] GetUpStream(IGH_ActiveObject obj)
    {
        if (obj is IGH_Param param)
            return param.Sources.Select(s => s.Attributes.GetTopLevel.DocObject)
                .OfType<IGH_ActiveObject>().ToHashSet().ToArray();

        if (obj is IGH_Component comp) return comp.Params.Input.SelectMany(GetUpStream).ToHashSet().ToArray();
        return Array.Empty<IGH_ActiveObject>();
    }

    public void Solve(GH_SolutionMode mode)
    {
        var tasks = Items.Select(i => Task.Run(() => SolveOne(i, mode, Doc)));
        Task.WaitAll(tasks.ToArray());
    }

    private static void SolveOne(IGH_ActiveObject item, GH_SolutionMode mode, GH_Document doc)
    {
        try
        {
            if (item.Phase == GH_SolutionPhase.Computed) return;

            item.CollectData();
            item.ComputeData();
        }
        catch (Exception ex)
        {
            ProjectData.SetProjectError(ex);
            var ex2 = ex;
            item.Phase = GH_SolutionPhase.Failed;
            item.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
            HostUtils.ExceptionReport(ex2);

            var ignoreList = (SortedList<Guid, bool>)IgnoreList.GetValue(doc);

            if (mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless &&
                ignoreList != null &&
                !ignoreList.ContainsKey(item.InstanceGuid))
                Instances.DocumentEditor.BeginInvoke((MethodInvoker)delegate
                {
                    using GH_ObjectExceptionDialog gHObjectExceptionDialog = new();

                    ((Label)IconInfo.GetValue(gHObjectExceptionDialog))!.Image = item.Icon_24x24;
                    ((Label)NameInfo.GetValue(gHObjectExceptionDialog))!.Text = $"{item.Name} [{item.NickName}]";
                    ((Label)ExceptionInfo.GetValue(gHObjectExceptionDialog))!.Text =
                        "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {item.Name}" +
                        Environment.NewLine + $"c_UUID: {item.InstanceGuid}" + Environment.NewLine +
                        $"c_POS: {item.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

                    GH_WindowsFormUtil.CenterFormOnEditor(gHObjectExceptionDialog, true);
                    gHObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
                    if (((CheckBox)DoNotShowInfo.GetValue(gHObjectExceptionDialog))!.Checked)
                        ignoreList.Add(item.InstanceGuid, true);
                });
            ProjectData.ClearProjectError();
        }
        finally
        {
            item.Attributes.ExpireLayout();
        }
    }
}