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
    internal class GraphNode
    {
        private bool UseBackTask => !SolutionAsyncLoad.NoAsyncObjs.Contains(ActiveObject.ComponentGuid);
        public IGH_ActiveObject ActiveObject { get; }
        private Action _setIndex;
        /// <summary>
        /// -1 means it is not the first node.
        /// </summary>
        public int CalculateLevel { get; set; } = 0;

        private SortedList<Guid, bool> _ignoreList;

        private List<GraphNode> _nextNodes = new List<GraphNode>();
        private GH_SolutionMode _mode;

        internal static readonly FieldInfo _iconInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblIcon")).First();
        internal static readonly FieldInfo _nameInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblName")).First();
        internal static readonly FieldInfo _exceptionInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("_lblException")).First();
        internal static readonly FieldInfo _dontShotInfo = typeof(GH_ObjectExceptionDialog).GetRuntimeFields().Where(info => info.Name.Contains("chkDontShow")).First();

        public GraphNode(IGH_ActiveObject obj, Action setIndex, SortedList<Guid, bool> ignoreList, GH_SolutionMode mode)
        {
            this.ActiveObject = obj;
            this._setIndex = setIndex;
            this._ignoreList = ignoreList;
            this._mode = mode;
        }

        internal void FindNextNode(GraphNode[] nodes)
        {
            //Find the next objects.
            IGH_DocumentObject[] objects = new IGH_DocumentObject[0];
            if(ActiveObject is IGH_Component)
            {
                IGH_Component component = (IGH_Component)ActiveObject;

                List<IGH_DocumentObject> gH_DocumentObjects = new List<IGH_DocumentObject>();
                component.Params.Output.ForEach(obj =>
                {
                    obj.Recipients.ToList().ForEach(h =>
                    {
                        IGH_DocumentObject objRelay = h.Attributes.GetTopLevel.DocObject;
                        if (objRelay == null) return;
                        if (!gH_DocumentObjects.Contains(objRelay))
                        {
                            gH_DocumentObjects.Add(objRelay);
                        }
                    });
                });
                objects = gH_DocumentObjects.ToArray();
            }
            else if(ActiveObject is IGH_Param)
            {
                IGH_Param param = (IGH_Param)ActiveObject;
                objects = param.Recipients.Select(o => o.Attributes.GetTopLevel.DocObject).ToArray();
            }

            //Add the next nodes.
            foreach (GraphNode node in nodes)
            {
                if(node != null && objects.Contains(node.ActiveObject))
                {
                    node.CalculateLevel = -1;
                    _nextNodes.Add(node);
                }
            }
        }

        internal void SetNextLevel()
        {
            int nextLevel = CalculateLevel + 1;
            foreach (var nextNode in _nextNodes)
            {
                if(nextNode.CalculateLevel < nextLevel)
                {
                    nextNode.CalculateLevel = nextLevel;
                    nextNode.SetNextLevel();
                }
            }
        }

        internal void UpperLevel()
        {
            if (_nextNodes != null && _nextNodes.Count > 0)
                CalculateLevel = _nextNodes.Min(n => n.CalculateLevel) - 1;
        }

        internal async Task<bool> SolveOneObject(DocumentTask doc)
        {
            _setIndex.Invoke();
            try
            {
                if (ActiveObject.Phase == GH_SolutionPhase.Computed)
                {
                    return true;
                }

                SolutionAsyncLoad.ComputingObjects.Add(ActiveObject);
                GH_DocumentReplacer.UpdateViews();

                if (UseBackTask)
                {
                    doc.LastCalculate = ActiveObject;
                    if (!await Task.Run(() =>
                    {
                        try
                        {
                            ActiveObject.CollectData();
                            ActiveObject.ComputeData();
                            return true;
                        }
                        //This Active Obejct can't calculate on task.
                        catch (InvalidOperationException)
                        {
                            SolutionAsyncLoad.NoAsyncObjs.Add(ActiveObject.ComponentGuid);
                            SolutionAsyncLoad.SaveToJson();
                            return false;
                        }
                        catch (ArgumentOutOfRangeException ex)
                        {
                            //Changed two fast.
                            if (ex.StackTrace.Contains("GH_StructureIterator"))
                                return false;
                        }
                        //Error for ActiveObject
                        return true;
                    })) return false;
                }
                else
                {
                    Instances.ActiveCanvas.Invoke((Action)delegate
                    {
                        ActiveObject.CollectData();
                        ActiveObject.ComputeData();
                    });
                }
                SolutionAsyncLoad.ComputingObjects.Remove(ActiveObject);
                GH_DocumentReplacer.UpdateViews();

            }
            catch (Exception ex)
            {
                ProjectData.SetProjectError(ex);
                Exception ex2 = ex;
                ActiveObject.Phase = GH_SolutionPhase.Failed;
                ActiveObject.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex2.Message);
                HostUtils.ExceptionReport(ex2);
                if (_mode == GH_SolutionMode.Default && !RhinoApp.IsRunningHeadless && !_ignoreList.ContainsKey(ActiveObject.InstanceGuid))
                {
                    Instances.DocumentEditor.BeginInvoke((MethodInvoker)delegate
                    {
                        GH_ObjectExceptionDialog gH_ObjectExceptionDialog = new GH_ObjectExceptionDialog();

                        ((Label)_iconInfo.GetValue(gH_ObjectExceptionDialog)).Image = ActiveObject.Icon_24x24;
                        ((Label)_nameInfo.GetValue(gH_ObjectExceptionDialog)).Text = $"{ActiveObject.Name} [{ActiveObject.NickName}]";
                        ((Label)_exceptionInfo.GetValue(gH_ObjectExceptionDialog)).Text = "An exception was thrown during a solution:" + Environment.NewLine + $"Component: {ActiveObject.Name}" + Environment.NewLine + $"c_UUID: {ActiveObject.InstanceGuid}" + Environment.NewLine + $"c_POS: {ActiveObject.Attributes.Pivot}" + Environment.NewLine + Environment.NewLine + ex2.Message;

                        GH_WindowsFormUtil.CenterFormOnEditor(gH_ObjectExceptionDialog, limitToScreen: true);
                        gH_ObjectExceptionDialog.ShowDialog(Instances.DocumentEditor);
                        if (((CheckBox)_dontShotInfo.GetValue(gH_ObjectExceptionDialog)).Checked)
                        {
                            _ignoreList.Add(ActiveObject.InstanceGuid, value: true);
                        }
                    });
                }
                ProjectData.ClearProjectError();
            }
            finally
            {
                ActiveObject.Attributes.ExpireLayout();
            }
            return true;
        }
    }
}
