using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using HarmonyLib;
using Microsoft.VisualBasic.CompilerServices;
using Rhino;
using Rhino.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync;

internal static class ActiveObjectHelper
{
    internal static readonly FieldInfo _iconInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog), "_lblIcon");
    internal static readonly FieldInfo _nameInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog),"_lblName");
    internal static readonly FieldInfo _exceptionInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog),"_lblException");
    internal static readonly FieldInfo _doNotShowInfo = AccessTools.Field(typeof(GH_ObjectExceptionDialog),"chkDontShow");
    internal static readonly FieldInfo _ignoreList = AccessTools.Field(typeof(GH_Document), "m_ignoreList");

    internal static void SolveOne(this IGH_ActiveObject item, GH_SolutionMode mode, GH_Document doc)
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
