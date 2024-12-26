using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.Kernel;
using HarmonyLib;
using SimpleGrasshopper.Util;
using SolutionAsync.Patch;

namespace SolutionAsync;

public class SolutionAsyncInfo : GH_AssemblyInfo
{
    public override string Name => "Solution Async";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => typeof(SolutionAsyncInfo).Assembly.GetBitmap("SolutionAsyncIcon_24.png");

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "Make UI responsible during the solution.";

    public override Guid Id => new("FCC6721F-31C1-420F-8B00-E67AEE19DCF1");

    //Return a string identifying you or your company.
    public override string AuthorName => "秋水";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "201704110219@stu.zafu.edu.cn";

    public override string Version => "1.2.1";
}

partial class SimpleAssemblyPriority
{
    internal static readonly FieldInfo _abort = AccessTools.Field(typeof(GH_Document), "m_abortRequested");

    protected override int? MenuIndex => 4;

    protected override int InsertIndex => 6;

    protected override void DoWithEditor(GH_DocumentEditor editor)
    {
        var harmony = new Harmony("Grasshopper.SolutionAsync");
        harmony.PatchAll();
        StructureIteratorPatch.Patch(harmony);

        Instances.ActiveCanvas.KeyDown += ActiveCanvas_KeyDown;
        Instances.DocumentServer.DocumentAdded += DocumentServer_DocumentAdded;

        base.DoWithEditor(editor);
    }

    private void DocumentServer_DocumentAdded(GH_DocumentServer sender, GH_Document doc)
    {
        DocumentPatch._addedDocuments.Add(doc);
    }

    private void ActiveCanvas_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape) return;

        _abort.SetValue(Instances.ActiveDocument, true);
    }
}