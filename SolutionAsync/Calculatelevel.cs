//using Grasshopper.Kernel;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace SolutionAsync;

//internal class Calculatelevel
//{
//    private readonly List<GraphNode> _nodes = new ();
//    private bool[] _nodeSucceed;

//    public Calculatelevel()
//    {
        
//    }

//    public Calculatelevel(GraphNode node)
//    {
//        AddANode(node);
//    }

//    internal void AddANode(GraphNode node)
//    {
//        _nodes.Add(node);
//    }

//    internal async Task<bool> SolveOneLevel(DocumentTask doc)
//    {
//        _nodeSucceed = (await Task.WhenAll(_nodes.Select(no => no.SolveOneObject(doc)))).ToArray();
//        return !_nodeSucceed?.Any(b => !b) ?? true;
//    }

//    internal void ClearFailedLevel()
//    {
//        if (_nodeSucceed.Length != _nodes.Count)
//        {
//            ClearLevel();
//            return;
//        }
//        for (int i = 0; i < _nodeSucceed.Length; i++)
//        {
//            if (!_nodeSucceed[i])
//            {
//                _nodes[i].ActiveObject.ExpireSolution(false);
//            }
//        }
//    }

//    private void ClearLevel()
//    {
//        foreach (var node in _nodes)
//        {
//            node.ActiveObject.ExpireSolution(false);
//        }
//    }

//    internal static Calculatelevel[] CreateLevels(List<IGH_ActiveObject> objs, List<Action> indexes, bool Calculate, SortedList<Guid, bool> ignoreList, GH_SolutionMode mode)
//    {
//        GraphNode[] nodes = new GraphNode[objs.Count];
//        for (int i = 0; i < objs.Count; i++)
//        {
//            nodes[i] = new GraphNode(objs[i], indexes[i], ignoreList, mode);
//        }

//        if (!Calculate)
//        {
//            return nodes.Select(no => new Calculatelevel(no)).ToArray();
//        }

//        foreach (var node in nodes)
//        {
//            node.FindNextNode(nodes);
//        }

//        foreach (var node in nodes)
//        {
//            if (node.CalculateLevel == 0)
//                node.SetNextLevel();
//        }

//        foreach (var node in nodes.OrderByDescending(o => o.CalculateLevel))
//        {
//            node.UpperLevel();
//        }

//        List<Calculatelevel> result = new ();
//        foreach (var node in nodes)
//        {
//            while (result.Count <= node.CalculateLevel)
//            {
//                result.Add(new Calculatelevel());
//            }
//            result[node.CalculateLevel].AddANode(node);
//        }
//        return result.ToArray();
//    }
//}
