using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync
{
    public class GH_DocumentReplacer
    {
		private static readonly List<DocumentTask> _documentTasks = new List<DocumentTask>();

		internal static void ChangeFunction()
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

        private async void SolveAllObjects(GH_SolutionMode mode)
        {
			GH_Document Document = Instances.ActiveCanvas.Document;

			if (SolutionAsyncLoad.UseSolutionAsync)
                await FindTask(Document).Compute(mode);
            else
            {
				DocumentTask._solutionIndexInfo.SetValue(Document, -1);

				List<IGH_ActiveObject> list = new List<IGH_ActiveObject>(Document.ObjectCount);
				List<int> list2 = new List<int>(Document.ObjectCount);
				for (int i = 0; i < Document.ObjectCount; i++)
				{
					IGH_ActiveObject iGH_ActiveObject = Document.Objects[i] as IGH_ActiveObject;
					if (iGH_ActiveObject != null)
					{
						list.Add(iGH_ActiveObject);
						list2.Add(i);
					}
				}

				SortedList<Guid, bool> ignoreList = (SortedList<Guid, bool>)DocumentTask._ignoreListInfo.GetValue(Document);
				for (int j = 0; j < Document.ObjectCount; j++)
				{
					if (GH_Document.IsEscapeKeyDown())
					{
						DocumentTask._abordInfo.SetValue(Document, true);
					}
					if (Document.AbortRequested)
					{
						break;
					}

					DocumentTask._solutionIndexInfo.SetValue(Document, list2[j]);
					IGH_ActiveObject actObject = list[j];
					try
					{
						if (actObject.Phase == GH_SolutionPhase.Computed)
						{
							continue;
						}
						actObject.CollectData();
						actObject.ComputeData();
					}
					catch (Exception ex)
					{
						ProjectData.SetProjectError(ex);
						Exception ex2 = ex;
						actObject.Phase = GH_SolutionPhase.Failed;
						actObject.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
						HostUtils.ExceptionReport(ex2);
						if (mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless && !ignoreList.ContainsKey(actObject.InstanceGuid))
						{
							GH_ObjectExceptionDialog gH_ObjectExceptionDialog = new GH_ObjectExceptionDialog();

							((Label)GraphNode._iconInfo.GetValue(gH_ObjectExceptionDialog)).Image = actObject.Icon_24x24;
							((Label)GraphNode._nameInfo.GetValue(gH_ObjectExceptionDialog)).Text = $"{actObject.Name} [{actObject.NickName}]";
							((Label)GraphNode._exceptionInfo.GetValue(gH_ObjectExceptionDialog)).Text = "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {actObject.Name}" + Environment.NewLine + $"c_UUID: {actObject.InstanceGuid}" + Environment.NewLine + $"c_POS: {actObject.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

							GH_WindowsFormUtil.CenterFormOnEditor((Form)gH_ObjectExceptionDialog, limitToScreen: true);
							gH_ObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
							if (((CheckBox)GraphNode._dontShotInfo.GetValue(gH_ObjectExceptionDialog)).Checked)
							{
								ignoreList.Add(actObject.InstanceGuid, value: true);
							}
						}
						ProjectData.ClearProjectError();
					}
					actObject.Attributes.ExpireLayout();
					if (Document.AbortRequested)
					{
						break;
					}
				}
				if (Document.AbortRequested)
				{
					DocumentTask._stateInfo.SetValue(Document, GH_ProcessStep.Aborted);
				}
				else
				{
					DocumentTask._stateInfo.SetValue(Document, GH_ProcessStep.PostProcess);
				}

			}
		}

        private static DocumentTask FindTask(GH_Document doc)
        {
            foreach (var task in _documentTasks)
            {
                if(task.Document == doc)
                {
                    return task;
                }
            }

            DocumentTask newTask = new DocumentTask(doc);
            _documentTasks.Add(newTask);
            return newTask;
        }

        internal static void CancelDocument(GH_Document document)
        {
            foreach (var task in _documentTasks)
            {
                if (task.Document == document)
                {
                    task.AbortCompute();
					return;
				}
            }
        }
	}
}
