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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync
{
    public class GH_DocumentReplacer
    {
		private static readonly SortedDictionary<GH_Document, CancellationTokenSource> calculatingDocDict = new SortedDictionary<GH_Document, CancellationTokenSource>();

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


		private void SolveAllObjects(GH_SolutionMode mode)
		{
			//MessageBox.Show("Calculating!");
			GH_Document doc = Instances.ActiveCanvas.Document;
			CancelDoc(doc, true);
			calculatingDocDict.Add(doc, SolveAllObjects(doc, mode));
		}

		internal static void CancelDoc(GH_Document doc, bool clearData)
        {
			if (calculatingDocDict.ContainsKey(doc))
			{
				calculatingDocDict[doc].Cancel();
				calculatingDocDict.Remove(doc);
                //if (clearData)
                //{
                //    foreach (IGH_ActiveObject obj in doc.Objects)
                //    {
                //        if (obj != null)
                //        {
                //            obj.ClearData();
                //        }
                //    }
                //}
            }
		}

		private CancellationTokenSource SolveAllObjects(GH_Document doc, GH_SolutionMode mode)
        {
			CancellationTokenSource cancel = new CancellationTokenSource();
			Task.Run(async () =>
            {
				_solutionIndexInfo.SetValue(doc, -1);
				List<IGH_ActiveObject> objList = new List<IGH_ActiveObject>(doc.ObjectCount);
				List<int> indexList = new List<int>(doc.ObjectCount);
				for (int i = 0; i < doc.ObjectCount; i++)
				{
					IGH_ActiveObject iGH_ActiveObject = doc.Objects[i] as IGH_ActiveObject;
					if (iGH_ActiveObject != null)
					{
						objList.Add(iGH_ActiveObject);
						indexList.Add(i);
					}
				}
				SortedList<Guid, bool> ignoreList = (SortedList<Guid, bool>)_ignoreListInfo.GetValue(this);

				for (int j = 0; j < objList.Count; j++)
				{
					//if (GH_Document.IsEscapeKeyDown())
					//{
					//	_abordInfo.SetValue(this, true);
					//}
					if (doc.AbortRequested)
					{
						break;
					}
					_solutionIndexInfo.SetValue(doc, indexList[j]);

					IGH_ActiveObject calculateObj = objList[j];
					try
					{
						if (calculateObj.Phase == GH_SolutionPhase.Computed)
						{
							continue;
						}
						await SolveOneObject(calculateObj);
						goto IL_0233;
					}
					catch (Exception ex)
					{
						ProjectData.SetProjectError(ex);
						Exception ex2 = ex;
						calculateObj.Phase = GH_SolutionPhase.Failed;
						calculateObj.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
						HostUtils.ExceptionReport(ex2);
						if (mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless && !ignoreList.ContainsKey(calculateObj.InstanceGuid))
						{
							GH_ObjectExceptionDialog gH_ObjectExceptionDialog = new GH_ObjectExceptionDialog();

							((Label)_iconInfo.GetValue(gH_ObjectExceptionDialog)).Image = calculateObj.Icon_24x24;
							((Label)_nameInfo.GetValue(gH_ObjectExceptionDialog)).Text = $"{calculateObj.Name} [{calculateObj.NickName}]";
							((Label)_exceptionInfo.GetValue(gH_ObjectExceptionDialog)).Text = "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {calculateObj.Name}" + Environment.NewLine + $"c_UUID: {calculateObj.InstanceGuid}" + Environment.NewLine + $"c_POS: {calculateObj.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

							GH_WindowsFormUtil.CenterFormOnEditor((Form)gH_ObjectExceptionDialog, limitToScreen: true);
							gH_ObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
							if (((CheckBox)_dontShotInfo.GetValue(gH_ObjectExceptionDialog)).Checked)
							{
								ignoreList.Add(calculateObj.InstanceGuid, value: true);
							}
						}
						ProjectData.ClearProjectError();
						goto IL_0233;
					}
				IL_0233:
					calculateObj.Attributes.ExpireLayout();
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

				calculatingDocDict.Remove(doc);

			}, cancel.Token);

			return cancel;
        }

		private static Task SolveOneObject(IGH_ActiveObject actObj)
        {
			return Task.Run(() =>
			{
				actObj.CollectData();
				actObj.ComputeData();
				Instances.ActiveCanvas.ScheduleRegen(1);
			});
		}
	}
}
