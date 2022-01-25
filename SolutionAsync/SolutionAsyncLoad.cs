using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync
{
    public class SolutionAsyncLoad : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Grasshopper.Instances.CanvasCreated += Instances_CanvasCreated;
            return GH_LoadingInstruction.Proceed;
        }

        private void Instances_CanvasCreated(GH_Canvas canvas)
        {
            Grasshopper.Instances.CanvasCreated -= Instances_CanvasCreated;

            GH_DocumentEditor editor = Grasshopper.Instances.DocumentEditor;
            if (editor == null)
            {
                Grasshopper.Instances.ActiveCanvas.DocumentChanged += ActiveCanvas_DocumentChanged;
                return;
            }
            DoingSomethingFirst(editor);
        }

        private void ActiveCanvas_DocumentChanged(GH_Canvas sender, GH_CanvasDocumentChangedEventArgs e)
        {
            Grasshopper.Instances.ActiveCanvas.DocumentChanged -= ActiveCanvas_DocumentChanged;

            GH_DocumentEditor editor = Grasshopper.Instances.DocumentEditor;
            if (editor == null)
            {
                MessageBox.Show(this.GetType().Name + " can't find the menu!");
                return;
            }
            DoingSomethingFirst(editor);
        }

        private void DoingSomethingFirst(GH_DocumentEditor editor)
        {
            GH_DocumentReplacer.Init();
            Grasshopper.Instances.ActiveCanvas.KeyDown += ActiveCanvas_KeyDown;
        }

        private void ActiveCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Escape)
            {
                GH_DocumentReplacer.CancelDoc(Grasshopper.Instances.ActiveCanvas.Document, false);
            }
        }
    }
}
