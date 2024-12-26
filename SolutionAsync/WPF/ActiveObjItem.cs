using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace SolutionAsync.WPF;

public class ActiveObjItem
{
    public ActiveObjItem(Guid guid)
    {
        Guid = guid;

        var proxy = Instances.ComponentServer.EmitObjectProxy(guid);
        if (proxy == null) return;

        Icon = proxy.Icon;
        Name = proxy.Desc.Name;

        if (proxy.Desc.HasCategory) Category = proxy.Desc.Category;
        if (proxy.Desc.HasSubCategory) Subcategory = proxy.Desc.SubCategory;
    }

    public ActiveObjItem(IGH_ActiveObject obj)
    {
        Guid = obj.ComponentGuid;

        Icon = obj.Icon_24x24;
        Name = obj.Name;

        if (obj.HasCategory) Category = obj.Category;
        if (obj.HasSubCategory) Subcategory = obj.SubCategory;
    }

    public Bitmap Icon { get; }
    public string Name { get; } = "Not Found!";
    public Guid Guid { get; }

    public string Category { get; } = "Not Found!";
    public string Subcategory { get; } = "Not Found!";
}