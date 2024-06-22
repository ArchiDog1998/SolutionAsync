using SimpleGrasshopper.Attributes;
using SolutionAsync.WPF;
using System;
using System.Collections.Generic;

namespace SolutionAsync;
internal static partial class Data
{
    [Setting]
    private static readonly List<Guid> _NoAsyncObjects = new();

    [Setting, Config("Solution Async"), ToolButton("SolutionAsyncIcon_24.png")]
    private static readonly bool _UseSolutionAsync = true;

    [Setting, Config("Refresh Views")]
    private static readonly bool _RefreshEveryLevelDuringAsync = true;

    [Setting, Config("Change Solution Order", "Change object's solution order to make Calculate much Faster."),
        ToolButton("UseChangeLevelIcon_24.png")]
    private static readonly bool _UseSolutionOrderedLevelAsync = true;

    [Config("Open Skip Async Window")]
    internal static object OpenWindow
    {
        get => false;
        set
        {
            new SkipAsyncWindow().Show();
        }
    }
}
