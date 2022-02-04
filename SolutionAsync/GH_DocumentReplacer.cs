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
using System.Diagnostics;

namespace SolutionAsync
{
    public static class GH_DocumentReplacer
    {
		private static readonly List<DocumentTask> _documentTasks = new List<DocumentTask>();
        private static readonly MethodInfo _solveAllObjInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("SolveAllObjects")).First();
        internal static IGH_ActiveObject LastCalculate { private get; set; }
        internal static void ChangeFunction()
        {
            ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(MyNewSolution))).First(),
                //typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("SolveAllObjects")).First());
				typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolution") && info.GetParameters().Length == 2).First());
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

        private static async void MyNewSolution(this GH_Document Document, bool expireAllObjects, GH_SolutionMode mode)
        {
            if (SolutionAsyncLoad.UseSolutionAsync && Document == Instances.ActiveCanvas.Document)
            {
                try
                {
                    await FindTask(Document).Compute(expireAllObjects, mode);
                }
                catch (Exception ex)
                {
                    string pluginsName = LastCalculate.GetType().Assembly.GetName().Name;

                    Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Solution Async failed to calculate! Maybe it is NOT compatible with {pluginsName}...",
                        GH_RuntimeMessageLevel.Error));

                    string message = $"When Solution Async Calculating \"{LastCalculate.Name}\" from {pluginsName}, we got an exception:";
                    message += "\n \n" + ex.Message;
                    MessageBox.Show(message, ex.GetType().Name);
                }
            }
            else
            {
                if ((bool)DocumentTask._disposeInfo.GetValue(Document))
                {
                    return;
                }
                if (!GH_Document.EnableSolutions)
                {
                    Instances.InvalidateCanvas();
                    return;
                }
                DocumentTask._solutionSetupInfo.Invoke(Document, new object[] { });
                DateTime now = DateTime.Now;
                Stopwatch profiler = (Stopwatch)DocumentTask._solutionProfilerInfo.Invoke(Document, new object[] { });
                DocumentTask._scheduleCallDelegatesInfo.Invoke(Document, new object[] { });
                DocumentTask._solutionStartInfo.Invoke(Document, new object[] { });
                if (Document.Enabled && Document.SolutionState != GH_ProcessStep.Process)
                {
                    DocumentTask._stateInfo.SetValue(Document, GH_ProcessStep.PreProcess);
                    if (expireAllObjects)
                    {
                        DocumentTask._solutionExpireAllInfo.Invoke(Document, new object[] { });
                    }
                    uint id = (uint)DocumentTask._solutionBeginUndoAllInfo.Invoke(Document, new object[] { });

                    //_stateInfo.SetValue(Document, GH_ProcessStep.Process);
                    DocumentTask._stateInfo.SetValue(Document, GH_ProcessStep.PostProcess);
                    try
                    {
                        _solveAllObjInfo.Invoke(Document, new object[] { mode });
                    }
                    catch (Exception ex)
                    {
                        ProjectData.SetProjectError(ex);
                        Exception ex2 = ex;
                        switch (mode)
                        {
                            case GH_SolutionMode.Default:
                                Tracing.Assert(new Guid("{D56F3CBE-219D-4311-8B4B-C61140D441E3}"), "An unhandled solution exception was caught.", ex2);
                                break;
                            case GH_SolutionMode.CommandLine:
                                RhinoApp.WriteLine("An unhandled solution exception was caught.:");
                                while (ex2 != null)
                                {
                                    RhinoApp.WriteLine("  " + ex2.Message);
                                    ex2 = ex2.InnerException;
                                }
                                break;
                        }
                        ProjectData.ClearProjectError();
                    }
                    DocumentTask._solutionEndUndoInfo.Invoke(Document, new object[] { id });
                    DocumentTask._solutionProfiledInfo.Invoke(Document, new object[] { now, profiler });
                }

                DocumentTask._solutionCleanUpInfo.Invoke(Document, new object[] { });
                DocumentTask._solutionEndInfo.Invoke(Document, new object[] { now });
                DocumentTask._solutionCompletionMessagingInfo.Invoke(Document, new object[] { mode });
                DocumentTask._solutionTriggerInfo.Invoke(Document, new object[] { mode });
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
