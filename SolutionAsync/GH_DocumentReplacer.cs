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
				CancellationTokenSource cancel = calculatingDocDict[doc];
				calculatingDocDict.Remove(doc);
				cancel.Cancel();
            }
		}

		private CancellationTokenSource SolveAllObjects(GH_Document doc, GH_SolutionMode mode)
        {
			CancellationTokenSource cancel = new CancellationTokenSource();
			Task.Run(async () =>
            {
				_solutionIndexInfo.SetValue(doc, -1);
				List<IGH_ActiveObject> objList = new List<IGH_ActiveObject>(doc.ObjectCount);
				List<Action> setIndexList = new List<Action>(doc.ObjectCount);
				for (int i = 0; i < doc.ObjectCount; i++)
				{
					IGH_ActiveObject iGH_ActiveObject = doc.Objects[i] as IGH_ActiveObject;
					if (iGH_ActiveObject != null)
					{
						objList.Add(iGH_ActiveObject);
						setIndexList.Add(() => _solutionIndexInfo.SetValue(doc, i));
					}
				}

				SortedList<Guid, bool> ignoreList = (SortedList<Guid, bool>)_ignoreListInfo.GetValue(this);
				Calculatelevel[] levels = Calculatelevel.CrateLevels(objList, setIndexList, false, ignoreList, mode);

                foreach (var level in levels)
                {
                    if (cancel.IsCancellationRequested)
                    {
                        _abordInfo.SetValue(this, true);
                    }
                    if (doc.AbortRequested)
					{
                        if (calculatingDocDict.ContainsKey(doc))
                        {
							_abordInfo.SetValue(this, false);
						}
						break;
					}

					await level.SolveOneLevel();

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
	}
}
