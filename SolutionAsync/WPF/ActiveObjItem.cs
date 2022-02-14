using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionAsync.WPF
{
    public class ActiveObjItem
    {
        public Bitmap Icon { get; } = null;
        public string Name { get; } = "Not Found!";
        public Guid Guid { get; }

        public string Category { get; } = "Not Found!";
        public string Subcategory { get; } = "Not Found!";
        public ActiveObjItem(Guid guid)
        {
            this.Guid = guid;

            IGH_ObjectProxy proxy = Grasshopper.Instances.ComponentServer.EmitObjectProxy(guid);
            if (proxy == null) return;

            Icon = proxy.Icon;
            Name = proxy.Desc.Name;

            if (proxy.Desc.HasCategory) Category = proxy.Desc.Category;
            if (proxy.Desc.HasSubCategory) Subcategory = proxy.Desc.SubCategory;
        }

        public ActiveObjItem(IGH_ActiveObject obj)
        {
            this.Guid = obj.ComponentGuid;

            this.Icon = obj.Icon_24x24;
            this.Name = obj.Name;

            if (obj.HasCategory) Category = obj.Category;
            if (obj.HasSubCategory) Subcategory = obj.SubCategory;
        }
    }
}
