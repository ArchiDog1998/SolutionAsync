using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Microsoft.VisualBasic.CompilerServices;
using Rhino;
using Rhino.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync
{
    public class GH_DocumentReplacer
    {
		private static readonly List<GH_Document> calculatingDoc = new List<GH_Document> ();
		private static readonly FieldInfo _stateInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("_state")).First();
		private static readonly FieldInfo _abordInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_abortRequested")).First();
		private static readonly FieldInfo _solutionIndexInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_solutionIndex")).First();
		private static readonly FieldInfo _ignoreListInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_ignoreList")).First();

		private static readonly FieldInfo _iconInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblIcon")).First();
		private static readonly FieldInfo _nameInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblName")).First();
		private static readonly FieldInfo _exceptionInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblException")).First();
		private static readonly FieldInfo _dontShotInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("chkDontShow")).First();

		internal static void Init()
        {
			ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(SolveAllObjects))).First(),
				typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("SolveAllObjects")).First());

		}

		internal static bool ExchangeMethod(MethodInfo targetMethod, MethodInfo injectMethod)
        {
            if (targetMethod == null || injectMethod == null)
            {
                return false;
            }
            RuntimeHelpers.PrepareMethod(targetMethod.MethodHandle);
            RuntimeHelpers.PrepareMethod(injectMethod.MethodHandle);
            unsafe
            {
                if (IntPtr.Size == 4)
                {
                    int* tar = (int*)targetMethod.MethodHandle.Value.ToPointer() + 2;
                    int* inj = (int*)injectMethod.MethodHandle.Value.ToPointer() + 2;
                    var relay = *tar;
                    *tar = *inj;
                    *inj = relay;
                }
                else
                {
                    long* tar = (long*)targetMethod.MethodHandle.Value.ToPointer() + 1;
                    long* inj = (long*)injectMethod.MethodHandle.Value.ToPointer() + 1;
                    var relay = *tar;
                    *tar = *inj;
                    *inj = relay;
                }
            }
            return true;
        }

		private static bool FindProcessDocument(out GH_Document doc)
        {
			doc = null;
            foreach (GH_Document document in Instances.DocumentServer)
            {
				if(document.SolutionState == GH_ProcessStep.Process && !calculatingDoc.Contains(document))
                {
					calculatingDoc.Add(document);
					doc = document;
					return true;
				}
            }
			return false;
        }

		private void SolveAllObjects(GH_SolutionMode mode)
		{
			if(!FindProcessDocument(out GH_Document doc))
            {
				MessageBox.Show("Solution Async can't find the document!");
            }

			_solutionIndexInfo.SetValue(doc, -1);
			List<IGH_ActiveObject> list = new List<IGH_ActiveObject>(doc.ObjectCount);
			List<int> list2 = new List<int>(doc.ObjectCount);
			int num = doc.ObjectCount - 1;
			for (int i = 0; i <= num; i++)
			{
				IGH_ActiveObject iGH_ActiveObject = doc.Objects[i] as IGH_ActiveObject;
				if (iGH_ActiveObject != null)
				{
					list.Add(iGH_ActiveObject);
					list2.Add(i);
				}
			}

			SortedList<Guid, bool> ignoreList = (SortedList<Guid, bool>)_ignoreListInfo.GetValue(this);
			for (int j = 0; j < list.Count; j++)
			{
				if (GH_Document.IsEscapeKeyDown())
				{
					_abordInfo.SetValue(this, true);
				}
				if (doc.AbortRequested)
				{
					break;
				}
				_solutionIndexInfo.SetValue(doc, list2[j]);

				IGH_ActiveObject iGH_ActiveObject2 = list[j];
				try
				{
					if (iGH_ActiveObject2.Phase == GH_SolutionPhase.Computed)
					{
						continue;
					}
					iGH_ActiveObject2.CollectData();
					iGH_ActiveObject2.ComputeData();
					goto IL_0233;
				}
				catch (Exception ex)
				{
					ProjectData.SetProjectError(ex);
					Exception ex2 = ex;
					iGH_ActiveObject2.Phase = GH_SolutionPhase.Failed;
					iGH_ActiveObject2.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
					HostUtils.ExceptionReport(ex2);
                    if (mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless && !ignoreList.ContainsKey(iGH_ActiveObject2.InstanceGuid))
                    {
                        GH_ObjectExceptionDialog gH_ObjectExceptionDialog = new GH_ObjectExceptionDialog();

						((Label)_iconInfo.GetValue(gH_ObjectExceptionDialog)).Image = iGH_ActiveObject2.Icon_24x24;
						((Label)_nameInfo.GetValue(gH_ObjectExceptionDialog)).Text = $"{iGH_ActiveObject2.Name} [{iGH_ActiveObject2.NickName}]";
						((Label)_exceptionInfo.GetValue(gH_ObjectExceptionDialog)).Text = "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {iGH_ActiveObject2.Name}" + Environment.NewLine + $"c_UUID: {iGH_ActiveObject2.InstanceGuid}" + Environment.NewLine + $"c_POS: {iGH_ActiveObject2.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

                        GH_WindowsFormUtil.CenterFormOnEditor((Form)gH_ObjectExceptionDialog, limitToScreen: true);
                        gH_ObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
                        if (((CheckBox)_dontShotInfo.GetValue( gH_ObjectExceptionDialog)).Checked)
                        {
							ignoreList.Add(iGH_ActiveObject2.InstanceGuid, value: true);
                        }
                    }
                    ProjectData.ClearProjectError();
					goto IL_0233;
				}
				IL_0233:
				iGH_ActiveObject2.Attributes.ExpireLayout();
				if (doc.AbortRequested)
				{
					break;
				}
			}
			if (doc.AbortRequested)
			{
				_stateInfo.SetValue(doc, GH_ProcessStep.Aborted);
			}
			else
			{
				_stateInfo.SetValue(doc, GH_ProcessStep.PostProcess);
			}

			calculatingDoc.Remove(doc);

			MessageBox.Show("Calculated from Solution Async!");
		}
	}
}
