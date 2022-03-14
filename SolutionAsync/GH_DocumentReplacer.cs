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
        private static Task<DocumentTask> _workTask = Task.Run(()=> default(DocumentTask));
		private static readonly List<DocumentTask> _documentTasks = new List<DocumentTask>();

        internal static void ChangeFunction()
        {
            ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(MyNewSolution))).First(),
				typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolution") && info.GetParameters().Length == 2).First());
            ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(MyRedrawAll))).First(),
                typeof(Instances).GetRuntimeMethods().Where(info => info.Name.Contains("RedrawAll")).First());

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

        public static void MyRedrawAll()
        {
            Instances.DocumentEditor.BeginInvoke((MethodInvoker)delegate
            {
                Instances.RedrawCanvas();
                RhinoDoc.ActiveDoc?.Views.Redraw();
            });
        }

        private static async void MyNewSolution(this GH_Document Document, bool expireAllObjects, GH_SolutionMode mode)
        {
            _workTask = _workTask.ContinueWith(task =>
            {
                return FindTask(Document);
            });
            await _workTask.Result.Compute(expireAllObjects, mode);
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

        internal static void CancelDocuments()
        {
            foreach (var task in _documentTasks)
            {
                task.AbortCompute();
            }
        }
    }
}
