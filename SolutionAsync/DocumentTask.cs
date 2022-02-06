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
        private static readonly FieldInfo _abordInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_abortRequested")).First();
        private static readonly FieldInfo _solutionIndexInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_solutionIndex")).First();
        private static readonly FieldInfo _ignoreListInfo = typeof(GH_Document).GetRuntimeFields().Where(info => info.Name.Contains("m_ignoreList")).First();
        #endregion

        public GH_Document Document { get; }

        //private bool _recomputeAbort = false;
        private bool _ManualCancel = false;
        private bool _isCalculating = false;

        ///// <summary>
        ///// Contain the right task.
        ///// </summary>
        //private Task _task = Task.CompletedTask;

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
            //Add a count.
            _count++;

            Instances.ActiveCanvas.BeginInvoke((MethodInvoker)delegate
            {
                Instances.ActiveCanvas.Refresh();
            });

            //Return right count.
            return _count;
        }

        internal async Task Compute(GH_SolutionMode mode)
        {
            int id = AddACalculate();

            while (_isCalculating)
            {
                await Task.Delay(100);
                if (_count > id) return;
            }

            if (_count == id)
                await SolveAllObjects(mode, id);
        }

        internal void AbortCompute()
        {
            _ManualCancel = true;
            Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" received Cancel solution Command.",
                GH_RuntimeMessageLevel.Remark));
        }

        private async Task SolveAllObjects(GH_SolutionMode mode, int id)
        {
            _isCalculating = true;

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
                if (_count != id)
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

            _isCalculating = false;
        }
    }
}
