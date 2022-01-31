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
    public class GH_DocumentReplacer
    {
		private static readonly List<DocumentTask> _documentTasks = new List<DocumentTask>();
		internal static void ChangeFunction()
        {
			ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(SolveAllObjects))).First(),
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

        private async void SolveAllObjects(bool expireAllObjects, GH_SolutionMode mode)
        {
			GH_Document Document = Instances.ActiveCanvas.Document;
            await FindTask(Document).Compute(expireAllObjects, mode);
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
