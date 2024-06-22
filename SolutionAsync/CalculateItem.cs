using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using HarmonyLib;
using Microsoft.VisualBasic.CompilerServices;
using Rhino;
using Rhino.Render.CustomRenderMeshes;
using Rhino.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;

namespace SolutionAsync;
internal class CalculateItem
{
    internal static readonly FieldInfo _iconInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblIcon");
    internal static readonly FieldInfo _nameInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblName");
    internal static readonly FieldInfo _exceptionInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblException");
    internal static readonly FieldInfo _doNotShowInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_chkDontShow");
    internal static readonly FieldInfo _ignoreList = AccessTools.Field(typeof(GH_Document), "m_ignoreList");

    public IGH_ActiveObject[] Items { get; }
    public GH_Document Doc { get; }

    public CalculateItem(GH_Document doc, params IGH_ActiveObject[] items)
    {
        Items = items;
        Doc = doc;
    }

    public static IEnumerable<CalculateItem> Create(GH_Document doc)
    {
        var items = doc.Objects.OfType<IGH_ActiveObject>();

        if (Data.UseSolutionOrderedLevelAsync)
        {
            _cache.Clear();
            var grp = items.GroupBy(GetObjectDepth);
            return items.GroupBy(GetObjectDepth).OrderBy(i => i.Key)
                .Select(i => new CalculateItem(doc, i.ToArray()));
        }
        else
        {
            return items.Select(i => new CalculateItem(doc, i));
        }
    }

    private static readonly Dictionary<IGH_ActiveObject, int> _cache = new ();
    private static int GetObjectDepth(IGH_ActiveObject obj)
    {
        if (_cache.TryGetValue(obj, out var depth)) return depth;
        var upStream = GetUpStream(obj);
        if (upStream == null || upStream.Length == 0) return 0;
        return _cache[obj] = upStream.Max(GetObjectDepth) + 1;
    }

    private static IGH_ActiveObject[] GetUpStream(IGH_ActiveObject obj)
    {
        if (obj is IGH_Param param)
        {
            return param.Sources.Select(s => s.Attributes.GetTopLevel.DocObject)
                .OfType<IGH_ActiveObject>().ToHashSet().ToArray();
        }
        else if(obj is IGH_Component comp)
        {
            return comp.Params.Input.SelectMany(GetUpStream).ToHashSet().ToArray();
        }
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
            if (item.Phase == GH_SolutionPhase.Computed)
            {
                return;
            }

            item.CollectData();
            item.ComputeData();
        }
        catch (Exception ex)
        {
            ProjectData.SetProjectError(ex);
            Exception ex2 = ex;
            item.Phase = GH_SolutionPhase.Failed;
            item.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
            HostUtils.ExceptionReport(ex2);

            var ignoreList = (SortedList<Guid, bool>)_ignoreList.GetValue(doc);

            if (mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless && !ignoreList.ContainsKey(item.InstanceGuid))
            {
                Instances.DocumentEditor.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    GH_ObjectExceptionDialog gH_ObjectExceptionDialog = new();

                    ((Label)_iconInfo.GetValue(gH_ObjectExceptionDialog)).Image = item.Icon_24x24;
                    ((Label)_nameInfo.GetValue(gH_ObjectExceptionDialog)).Text = $"{item.Name} [{item.NickName}]";
                    ((Label)_exceptionInfo.GetValue(gH_ObjectExceptionDialog)).Text = "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {item.Name}" + Environment.NewLine + $"c_UUID: {item.InstanceGuid}" + Environment.NewLine + $"c_POS: {item.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

                    GH_WindowsFormUtil.CenterFormOnEditor(gH_ObjectExceptionDialog, limitToScreen: true);
                    gH_ObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
                    if (((CheckBox)_doNotShowInfo.GetValue(gH_ObjectExceptionDialog)).Checked)
                    {
                        ignoreList.Add(item.InstanceGuid, value: true);
                    }
                });
            }
            ProjectData.ClearProjectError();
        }
        finally
        {
            item.Attributes.ExpireLayout();
        }
    }
}
