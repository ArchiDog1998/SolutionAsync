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
using SolutionAsync.WPF;
using System.IO;
using System.Web.Script.Serialization;

namespace SolutionAsync
{
    public class SolutionAsyncLoad : GH_AssemblyPriority
    {
        internal static List<Guid> NoAsyncObjs = new List<Guid>();
        private static readonly string _location = Path.Combine(Folders.SettingsFolder, "skipAsyncObjs.json");
        public static bool UseSolutionAsync
        {
            get => Instances.Settings.GetValue(nameof(UseSolutionAsync), true);
            set
            {
                Instances.Settings.SetValue(nameof(UseSolutionAsync), value);
                if (!value)
                {
                    GH_DocumentReplacer.CancelDocuments();
                }
            }
        }

        public static bool RefreshEveryLevelDuringAsync
        {
            get => Instances.Settings.GetValue(nameof(RefreshEveryLevelDuringAsync), false);
            set => Instances.Settings.SetValue(nameof(RefreshEveryLevelDuringAsync), value);
        }
        public static bool UseSolutionOrderedLevelAsync
        {
            get => Instances.Settings.GetValue(nameof(UseSolutionOrderedLevelAsync), true);
            set => Instances.Settings.SetValue(nameof(UseSolutionOrderedLevelAsync), value);
        }

        public override GH_LoadingInstruction PriorityLoad()
        {
            Instances.CanvasCreated += Instances_CanvasCreated;
            return GH_LoadingInstruction.Proceed;
        }

        private void Instances_CanvasCreated(GH_Canvas canvas)
        {
            Instances.CanvasCreated -= Instances_CanvasCreated;

            GH_DocumentEditor editor = Instances.DocumentEditor;
            if (editor == null)
            {
                Instances.ActiveCanvas.DocumentChanged += ActiveCanvas_DocumentChanged;
                return;
            }
            DoingSomethingFirst(editor);
        }

        private void ActiveCanvas_DocumentChanged(GH_Canvas sender, GH_CanvasDocumentChangedEventArgs e)
        {
            Instances.ActiveCanvas.DocumentChanged -= ActiveCanvas_DocumentChanged;

            GH_DocumentEditor editor = Instances.DocumentEditor;
            if (editor == null)
            {
                MessageBox.Show(this.GetType().Name + " can't find the menu!");
                return;
            }
            DoingSomethingFirst(editor);
        }
        internal static void SaveToJson()
        {
            JavaScriptSerializer ser = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
            try
            {
                File.WriteAllText(_location, ser.Serialize(NoAsyncObjs));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Json Library Save Failed");
            }
        }
        private void DoingSomethingFirst(GH_DocumentEditor editor)
        {
            //Read from json.
            try
            {
                if (File.Exists(_location))
                {
                    string jsonStr = File.ReadAllText(_location);
                    JavaScriptSerializer ser = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
                    try
                    {
                        NoAsyncObjs = ser.Deserialize<List<Guid>>(jsonStr);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Json Library Load Failed");
                    }
                }
                else
                {
                    NoAsyncObjs = new List<Guid>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            GH_DocumentReplacer.ChangeFunction();
            Instances.ActiveCanvas.KeyDown += ActiveCanvas_KeyDown;

            ToolStrip _canvasToolbar = editor.Controls[0].Controls[1] as ToolStrip;

            ToolStripSeparator toolStripSeparator = new ToolStripSeparator();
            toolStripSeparator.Margin = new Padding(2, 0, 2, 0);
            toolStripSeparator.Size = new Size(6, 40);
            _canvasToolbar.Items.Add(toolStripSeparator);


            ToolStripMenuItem useOrderChangeButton = new ToolStripMenuItem("Change Solution Order", Properties.Resources.UseChangeLevelIcon_24)
            {
                Checked = UseSolutionOrderedLevelAsync,
                Enabled = UseSolutionAsync,
                ToolTipText = "Change object's solution order to make Calculate much Faster.",
            };
            useOrderChangeButton.Click += (sender, e) =>
            {
                UseSolutionOrderedLevelAsync = useOrderChangeButton.Checked = !useOrderChangeButton.Checked;
            };

            ToolStripMenuItem refreshLevels = new ToolStripMenuItem("Refresh Canvas")
            {
                Checked = RefreshEveryLevelDuringAsync,
                Enabled = UseSolutionAsync,
                ToolTipText = "Refresh Canvas every time.",
            };
            refreshLevels.Click += (sender, e) =>
            {
                RefreshEveryLevelDuringAsync = refreshLevels.Checked = !refreshLevels.Checked;
            };

            ToolStripButton openSolutionButton = new ToolStripButton(Properties.Resources.SolutionAsyncIcon_24)
            { Checked = UseSolutionAsync, ToolTipText = "Choose whether to use Solution Async." };
            ToolStripMenuItem major = new ToolStripMenuItem("Solution Async", Properties.Resources.SolutionAsyncIcon_24) { Checked = UseSolutionAsync };

            major.Click += (sender, e) =>
            {
                UseSolutionAsync = refreshLevels.Enabled = useOrderChangeButton.Enabled = openSolutionButton.Checked = major.Checked = !major.Checked;
            };
            openSolutionButton.Click += (sender, e) =>
            {
                UseSolutionAsync = refreshLevels.Enabled = useOrderChangeButton.Enabled = openSolutionButton.Checked = major.Checked = !openSolutionButton.Checked;
            };

            _canvasToolbar.Items.Add(openSolutionButton);

            major.DropDownItems.Add(useOrderChangeButton);
            major.DropDownItems.Add(refreshLevels);
            major.DropDownItems.Add(new ToolStripMenuItem("Open Skip Async Window", null, (sender, e)=>
            {
                new SkipAsyncWindow().Show();
            }));


            ((ToolStripMenuItem)editor.MainMenuStrip.Items[4]).DropDownItems.Insert(6, major);
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
