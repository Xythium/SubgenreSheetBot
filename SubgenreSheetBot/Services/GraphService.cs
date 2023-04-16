using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Google.Apis.Sheets.v4.Data;
using GraphVizNet;
using Serilog;

namespace SubgenreSheetBot.Services;

public class GraphService
{
    /*
     * [Ambient],
     * [null, Ambient Dub],
     * [null, Dark Ambient],
     * [null, null, Black Ambient]
     *
     *
     * 0 null -> Ambient node of Root // [Ambient]
     * 1 null -> Ambient Dub node of Ambient // [Ambient, Ambient Dub]
     * 1 null -> Dark Ambient node of Ambient // [Ambient, Dark Ambient]
     * 2 null -> Black Ambient node of Dark Ambient // [Ambient, Dark Ambient, Black Ambient]
     */
    public GenreNode ParseTree(IList<ValueRange> valueRanges)
    {
        var rootNode = new GenreNode
        {
            Name = "Root",
            IsRoot = true
        };

        var allNodes = new Dictionary<string, GenreNode>();

        foreach (var valueRange in valueRanges)
        {
            if (valueRange.Values is null)
                throw new InvalidDataException("Values not loaded");
            if (valueRange.Values.Count < 1)
                throw new InvalidDataException("No values gotten");

            var map = new string[5];

            foreach (var row in valueRange.Values)
            {
                //var depth = row.Count(v => string.IsNullOrWhiteSpace(v as string));
                var name = row.LastOrDefault() as string;
                if (string.IsNullOrWhiteSpace(name))
                    throw new InvalidDataException("No genre name");

                var depth = row.IndexOf(name);

                map[depth] = name;
                if (!allNodes.TryGetValue(name, out var node))
                {
                    node = new GenreNode
                    {
                        Name = name
                    };
                    allNodes.Add(name, node);
                }

                node.IsMeta = node.IsMeta || depth == 0;

                var parentDepth = depth - 1;
                var parent = parentDepth == -1 ? rootNode : allNodes[map[parentDepth]];

                if (parent == node)
                {
                    Log.Error("{Name} is a node of itself", node.Name);
                    continue;
                }

                parent.AddSubgenre(node);
                //Log.Verbose("{Depth} null -> {Node} node of {Parent} // [{Map}]", depth, name, parent.Name, string.Join(", ", map));
            }
        }

        return rootNode;
    }

    public byte[] Render(GenreNode rootNode, SheetService.SheetGraphCommandOptions options)
    {
        //Log.Verbose("finding {Node}", options.Subgenre);
        var subgenreNode = FindNode(rootNode, options.Subgenre);
        //Log.Verbose("node: {Node}", subgenreNode.Name);
        var sb = GenerateGraphString(subgenreNode, options); // subgenreNode
        //Log.Verbose("sb {L}", sb);

        File.WriteAllText("layout.gv", sb);
        var graph = new GraphViz();
        var imageBytes = graph.LayoutAndRender(null, sb, null, options.Engine, "png");
        return imageBytes;
    }

    public byte[] RenderCollabs(string[] artists, SheetService.CollabGraphCommandOptions options)
    {
        var sb = new StringBuilder($$"""
graph G {
graph [engine={{options.Engine}}]
""");

        foreach (var artist in artists)
        {
            if (string.Equals(artist, options.StartArtist, StringComparison.OrdinalIgnoreCase))
                continue;
            sb.AppendLine($"{Quote(options.StartArtist)}--{Quote(artist)}");
        }

        sb.AppendLine("}");

        var graph = new GraphViz();
        var imageBytes = graph.LayoutAndRender(null, sb.ToString(), null, options.Engine, "png");
        return imageBytes;
    }

    public GenreNode? FindNode(GenreNode node, string test)
    {
        if (string.Equals(node.Name, test, StringComparison.OrdinalIgnoreCase))
            return node;

        foreach (var subNode in node.Subgenres)
        {
            //Log.Verbose("checking {Node} of {Root}", subNode.Name, node.Name);
            if (string.Equals(subNode.Name, test, StringComparison.OrdinalIgnoreCase))
                return subNode;

            var n = FindNode(subNode, test);
            if (n != null)
                return n;
        }

        return null;
    }

    private static string GenerateGraphString(GenreNode rootNode, SheetService.SheetGraphCommandOptions options)
    {
        var sb = new StringBuilder("digraph G {\r\n");
        sb.AppendLine($"graph [engine={options.Engine},splines=compound,overlap=false,fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")},fontsize=12,];"); //ratio=0.421940928 bgcolor={Quote("transparent")}
        sb.AppendLine($"node [fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")},fontsize=12];");
        sb.AppendLine($"fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")}");
        sb.Append(GenerateNodeString(rootNode));
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static StringBuilder GenerateNodeString(GenreNode node)
    {
        var sb = new StringBuilder();

        if (node.IsMeta)
        {
            sb.AppendLine($"{Quote(node.Name)} [shape=box]");
        }
        else
        {
            sb.AppendLine($"{Quote(node.Name)} [shape=box,style=rounded]");
        }

        foreach (var subgenre in node.Subgenres.OrderBy(sg => sg.Name))
        {
            if (subgenre.IsRoot)
                continue;
            if (subgenre.IsMeta)
            {
                sb.AppendLine($"{Quote(subgenre.Name)} [shape=box]");
            }
            else
            {
                sb.AppendLine($"{Quote(subgenre.Name)} [shape=box,style=rounded]");
            }

            if (subgenre.Parent is not null)
                sb.AppendLine($"{Quote(subgenre.Parent.Name)}->{Quote(subgenre.Name)};");
            sb.Append(GenerateNodeString(subgenre));
        }

        //Log.Verbose("generating node string for {Name}", node.Name);
        return sb;
    }

    private static string Quote(string str)
    {
        return $"\"{str}\"";
    }
}