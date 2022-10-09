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
using System.Runtime.InteropServices;
using System.Xml.Linq;

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

            ExchangeMethod(typeof(GH_DocumentReplacer).GetRuntimeMethods().Where(info => info.Name.Contains(nameof(IsEscapeKeyDown))).First(),
                typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("IsEscapeKeyDown")).First());
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

        //public static void MyRedrawAll()
        //{
        //    Instances.DocumentEditor.BeginInvoke((MethodInvoker)delegate
        //    {
        //        Instances.RedrawCanvas();
        //        RhinoDoc.ActiveDoc?.Views.Redraw();
        //    });
        //}

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

        private static FieldInfo _initInfo = typeof(GH_Document).GetRuntimeFields().First(m => m.Name.Contains("$STATIC$IsEscapeKeyDown$002$lastCheck$Init"));
        private static FieldInfo _checkInfo = typeof(GH_Document).GetRuntimeFields().First(m => m.Name.Contains("$STATIC$IsEscapeKeyDown$002$lastCheck"));
        private static bool ShouldEscape()
        {
            //Stop for new solution.
            if (Instances.ActiveCanvas.IsDocument)
            {
                foreach (var task in _documentTasks)
                {
                    if (task.Document == Instances.ActiveCanvas.Document)
                    {
                        return task.IsQuitCalculate();
                    }
                }
            }
            return false;
        }

        static DateTime lastUpdate = DateTime.MinValue;
        public static void UpdateViews()
        {
            if (DateTime.Now - lastUpdate <= new TimeSpan(0, 0, 0, 0, 200)) return;

            lastUpdate = DateTime.Now;

            Instances.DocumentEditor.Invoke((Action)(() =>
            {
                Instances.ActiveCanvas.Refresh();

                if (!SolutionAsyncLoad.RefreshEveryLevelDuringAsync) return;

                RhinoDoc.ActiveDoc.Views.Redraw();
            }));
        }

        public static bool IsEscapeKeyDown()
        {
            UpdateViews();

            if (ShouldEscape()) return true;

            var init = _initInfo.GetValue(null) as StaticLocalInitFlag;
            var lastCheck = (DateTime)_checkInfo.GetValue(null);
            if (init == null)
            {
                Interlocked.CompareExchange(ref init, new StaticLocalInitFlag(), null);
                _initInfo.SetValue(null, init);
            }
            bool lockTaken = false;
            try
            {
                Monitor.Enter(init, ref lockTaken);
                if (init.State == 0)
                {
                    init.State = 2;
                    _initInfo.SetValue(null, init);
                    lastCheck = DateTime.MinValue;
                }

                else if (init.State == 2)
                {
                    throw new IncompleteInitialization();
                }
            }
            finally
            {
                init.State = 1;
                _initInfo.SetValue(null, init);
                if (lockTaken)
                {
                    Monitor.Exit(init);
                }
            }
            DateTime utcNow = DateTime.UtcNow;
            if ((utcNow - lastCheck).TotalMilliseconds < 250.0)
            {
                return false;
            }
            lastCheck = utcNow;
            _checkInfo.SetValue(null, lastCheck);


            if (GetAsyncKeyState(Keys.Escape) < 0)
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return false;
                }
                uint lpdwProcessId = default(uint);
                GetWindowThreadProcessId(foregroundWindow, ref lpdwProcessId);
                return Process.GetCurrentProcess().Id == lpdwProcessId;
            }
            return false;
        }

        [DllImport("user32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern short GetAsyncKeyState(Keys virtualkey);

        [DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();


        [DllImport("user32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, ref uint lpdwProcessId);
    }
}
