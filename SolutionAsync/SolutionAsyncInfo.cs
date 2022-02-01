using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace SolutionAsync
{
    public class SolutionAsyncInfo : GH_AssemblyInfo
    {
        public override string Name => "SolutionAsync";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => Properties.Resources.SolutionAsyncIcon_24;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "Make UI responsible during the solution.";

        public override Guid Id => new Guid("FCC6721F-31C1-420F-8B00-E67AEE19DCF1");

        //Return a string identifying you or your company.
        public override string AuthorName => "秋水";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "201704110219@stu.zafu.edu.cn";

        public override string Version => "0.9.1";
    }
}