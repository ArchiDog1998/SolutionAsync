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

        public GH_Document Document { get; }

        private bool _abort = false;
        private bool _isCalculating = false;
        private bool _isNotFirst = false;

        public DocumentTask(GH_Document doc)
        {
            Document = doc;
        }
        internal async Task Compute(GH_SolutionMode mode)
        {
            if (_isCalculating)
            {
                if (_isNotFirst)
                {
                    Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" is calculating, Solution Async can't calculate it again. If this causes the unexpective result, please UNABLE Solution Async.",
                        GH_RuntimeMessageLevel.Warning));
                }
                _isNotFirst = true;
                return;
            }

            _abort = false;
            _isCalculating = true;
            await SolveAllObjects(mode);
            _isCalculating = false;
        }

        internal void AbortCompute()
        {
            _abort = true;
            Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" received Cancel solution Command.",
                GH_RuntimeMessageLevel.Remark));
        }

        private async Task SolveAllObjects(GH_SolutionMode mode)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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

            foreach (var level in levels)
            {
                if (_abort)
                {
                    _abordInfo.SetValue(Document, true);
                }
                if (Document.AbortRequested)
                {
                    break;
                }

                await level.SolveOneLevel();
            }

            stopwatch.Stop();
            string span = stopwatch.Elapsed.ToString("dd\\.hh\\:mm\\:ss");

            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

            if (Document.AbortRequested)
            {
                _stateInfo.SetValue(Document, GH_ProcessStep.Aborted);
                Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" have aborted the solution.    Time: " + span,
                    GH_RuntimeMessageLevel.Remark));
            }
            else
            {
                _stateInfo.SetValue(Document, GH_ProcessStep.PostProcess);
                Instances.DocumentEditor.SetStatusBarEvent(new GH_RuntimeMessage($"Document \"{Document.DisplayName}\" have calcualted successfully.   Time: " + span,
                    GH_RuntimeMessageLevel.Remark));

            }
        }
    }
}
