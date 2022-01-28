using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SolutionAsync
{
    public class SolutionAsyncLoad : GH_AssemblyPriority
    {
        public static bool UseSolutionAsync
        {
            get => Instances.Settings.GetValue(nameof(UseSolutionAsync), true);
            set => Instances.Settings.SetValue(nameof(UseSolutionAsync), value);
        }
        public static bool UseSolutionOrderedLevelAsync
        {
            get => Instances.Settings.GetValue(nameof(UseSolutionOrderedLevelAsync), true);
            set => Instances.Settings.SetValue(nameof(UseSolutionOrderedLevelAsync), value);
        }

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
            GH_DocumentReplacer.ChangeFunction();
            Instances.ActiveCanvas.KeyDown += ActiveCanvas_KeyDown;

            ToolStrip _canvasToolbar = editor.Controls[0].Controls[1] as ToolStrip;

            ToolStripSeparator toolStripSeparator = new ToolStripSeparator();
            toolStripSeparator.Margin = new Padding(2, 0, 2, 0);
            toolStripSeparator.Size = new Size(6, 40);
            _canvasToolbar.Items.Add(toolStripSeparator);

            ToolStripButton useButton = new ToolStripButton(Properties.Resources.UseChangeLevelIcon_24)
            {
                Checked = UseSolutionOrderedLevelAsync,
                Enabled = UseSolutionAsync,
                ToolTipText = "Change object's solution order to make Calculate much Faster.",
            };
            useButton.Click += (sender, e) =>
            {
                UseSolutionOrderedLevelAsync = useButton.Checked = !useButton.Checked;
            };

            ToolStripButton openButton = new ToolStripButton(Properties.Resources.SolutionAsyncIcon_24)
            { Checked = UseSolutionAsync, ToolTipText = "Choose whether to use Solution Async.\nNOTE: Please UNABLE Solution Async if you are using plugins that need to do some solution events, such as Kangaroo or Anemone." };
            openButton.Click += (sender, e) =>
            {
                UseSolutionAsync = useButton.Enabled = openButton.Checked = !openButton.Checked;
            };

            _canvasToolbar.Items.Add(openButton);
            _canvasToolbar.Items.Add(useButton);
        }

        private void ActiveCanvas_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Escape)
            {
                GH_DocumentReplacer.CancelDocument(Instances.ActiveCanvas.Document);
            }
        }
    }
}
