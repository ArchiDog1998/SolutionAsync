using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.CompilerServices;
using Rhino;

namespace SolutionAsync
{
    internal class DocumentTask
    {
        #region SolveAllObjects Field
        internal static readonly FieldInfo _stateInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("_state")).First();
        internal static readonly FieldInfo _abordInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_abortRequested")).First();
        internal static readonly FieldInfo _solutionIndexInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_solutionIndex")).First();
        internal static readonly FieldInfo _ignoreListInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_ignoreList")).First();
        #endregion

        internal static readonly FieldInfo _disposeInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_disposed")).First();
        internal static readonly MethodInfo _solutionSetupInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionSetup")).First();
        internal static readonly MethodInfo _solutionProfilerInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionProfiler")).First();
        internal static readonly MethodInfo _scheduleCallDelegatesInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("ScheduleCallDelegates")).First();
        internal static readonly MethodInfo _solutionStartInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionOnSolutionStart")).First();
        internal static readonly MethodInfo _solutionExpireAllInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionExpireAll")).First();
        internal static readonly MethodInfo _solutionBeginUndoAllInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionBeginUndo")).First();
        internal static readonly MethodInfo _solutionEndUndoInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionEndUndo")).First();
        internal static readonly MethodInfo _solutionProfiledInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionProfiled")).First();
        internal static readonly MethodInfo _solutionCleanUpInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionCleanup")).First();
        internal static readonly MethodInfo _solutionEndInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionOnSolutionEnd")).First();
        internal static readonly MethodInfo _solutionCompletionMessagingInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionCompletionMessaging")).First();
        internal static readonly MethodInfo _solutionTriggerInfo = typeof(GH_Document).GetRuntimeMethods().Where(info => info.Name.Contains("NewSolutionTriggerSchedules")).First();

        public GH_Document Document { get; }

        private bool _recomputeAbort = false;
        private bool _ManualCancel = false;
        //private bool _isCalculating = false;
        private bool _isFirst = true;
        //private readonly Queue<Task> _tasks = new Queue<Task>(new Task[] { Task.CompletedTask });

        /// <summary>
        /// Contain the right task.
        /// </summary>
        private Task _task = Task.CompletedTask;

        /// <summary>
        /// Record the calculate count.
        /// </summary>
        private int _count = -1;

        public DocumentTask(GH_Document doc)
        {
            Document = doc;
        }
        private int AddACalculate()
        {
            //Abort all solution.
            _recomputeAbort = true;

            //Add a count.
            _count++;

            Instances.ActiveCanvas.Refresh();

            //Return right count.
            return _count;
        }

        private bool ShouldCalulate(int count)
        {
            return _count == count;
        }

        internal async Task Compute(bool expireAllObjects, GH_SolutionMode mode)
        {
            if (_isFirst)
            {
                _isFirst = false;
                return;
            }

            int count = AddACalculate();

            await _task;
            Task rightTask = _task = _task.ContinueWith(async fakeTask =>
            {
                if (ShouldCalulate(count))
                {
                    _recomputeAbort = false;
                    _ManualCancel = false;

                    await MyNewSolution(expireAllObjects, mode);
                }
            }).Result;

            await rightTask;

        }

        private async Task MyNewSolution(bool expireAllObjects, GH_SolutionMode mode)
        {
            if ((bool)_disposeInfo.GetValue(Document))
            {
                return;
            }
            if (!GH_Document.EnableSolutions)
            {
                Instances.InvalidateCanvas();
                return;
            }
            _solutionSetupInfo.Invoke(Document, new object[] { });
            DateTime now = DateTime.Now;
            Stopwatch profiler = (Stopwatch)_solutionProfilerInfo.Invoke(Document, new object[] { });
            _scheduleCallDelegatesInfo.Invoke(Document, new object[] { });
            _solutionStartInfo.Invoke(Document, new object[] { });
            if (Document.Enabled && Document.SolutionState != GH_ProcessStep.Process)
            {
                _stateInfo.SetValue(Document, GH_ProcessStep.PreProcess);
                if (expireAllObjects)
                {
                    _solutionExpireAllInfo.Invoke(Document, new object[] { });
                }
                uint id = (uint)_solutionBeginUndoAllInfo.Invoke(Document, new object[] { });

                //_stateInfo.SetValue(Document, GH_ProcessStep.Process);
                _stateInfo.SetValue(Document, GH_ProcessStep.PostProcess);
                try
                {
                    await SolveAllObjects(mode);
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
                _solutionEndUndoInfo.Invoke(Document, new object[] { id });
                _solutionProfiledInfo.Invoke(Document, new object[] { now, profiler });
            }

            _solutionCleanUpInfo.Invoke(Document, new object[] { });
            _solutionEndInfo.Invoke(Document, new object[] { now });
            _solutionCompletionMessagingInfo.Invoke(Document, new object[] { mode });

            Instances.DocumentEditor.BeginInvoke((MethodInvoker)delegate
            {
                _solutionTriggerInfo.Invoke(Document, new object[] { mode });
            });
        }

        internal void AbortCompute()
        {
            _ManualCancel = true;
            Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" received Cancel solution Command.",
                GH_RuntimeMessageLevel.Remark));
        }

        private async Task SolveAllObjects(GH_SolutionMode mode)
        {
            _solutionIndexInfo.SetValue(Document, -1);
            List<IGH_ActiveObject> objList = new List<IGH_ActiveObject>(Document.ObjectCount);
            List<Action> setIndexList = new List<Action>(Document.ObjectCount);
            for (int i = 0; i < Document.ObjectCount; i++)
            {
                IGH_ActiveObject iGH_ActiveObject = Document.Objects[i] as IGH_ActiveObject;
                if (iGH_ActiveObject != null)
                {
                    objList.Add(iGH_ActiveObject);
                    setIndexList.Add(() => _solutionIndexInfo.SetValue(Document, i));
                }
            }

            SortedList<Guid, bool> ignoreList = (SortedList<Guid, bool>)_ignoreListInfo.GetValue(Document);

            //Get Graph
            Task<Calculatelevel[]> getLevels = Task<Calculatelevel[]>.Run(() =>
                Calculatelevel.CrateLevels(objList, setIndexList, SolutionAsyncLoad.UseSolutionOrderedLevelAsync, ignoreList, mode));
            Calculatelevel[] levels = getLevels.Result;

            for (int i = 0; i < levels.Length; i++)
            {
                Calculatelevel level = levels[i];
                if (_ManualCancel)
                {
                    _abordInfo.SetValue(Document, true);

                }
                if (_recomputeAbort)
                {
                    if (i != 0)
                    {
                        levels[i - 1].ClearLevel();
                    }
                    break;
                }
                if (Document.AbortRequested)
                {
                    break;
                }

                await level.SolveOneLevel();
            }

            if (Document.AbortRequested)
            {
                _stateInfo.SetValue(Document, GH_ProcessStep.Aborted);
            }
            else
            {
                _stateInfo.SetValue(Document, GH_ProcessStep.PostProcess);
            }
        }
    }
}
