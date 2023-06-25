using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Discord;
using GraphVizNet;
using Serilog;

namespace SubgenreSheetBot.Services;

public class GraphService
{
#region Subgenre

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
    public GenreNode ParseTree(IList<IList<object>> values, Dictionary<string, Color> genreColors)
    {
        var rootNode = new GenreNode
        {
            Name = "Root",
            IsRoot = true
        };

        var allNodes = new Dictionary<string, GenreNode>();
        var map = new string[5];

        GenreNode? meta = null;

        foreach (var row in values)
        {
            var name = row.LastOrDefault() as string;
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("No genre name");

            var depth = row.IndexOf(name);

            var index = name.IndexOf('[');
            if (index >= 0)
            {
                var endIndex = name.IndexOf(']');
                if (name[(index + 1)..endIndex] == "Meta")
                    name = name.Substring(0, index);
            }

            name = name.Trim();

            if (name == "Electronic Dance Music (EDM)")
                name = "EDM";
            if (name == "Tradition Music")
                name = "Traditional Music";

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
            if (node.IsMeta)
                meta = node;
            node.Color = genreColors[meta.Name];

            //Log.Verbose("{Depth} null -> {Node} node of {Parent} // [{Map}]", depth, name, parent.Name, string.Join(", ", map));
        }


        return rootNode;
    }

    public byte[] Render(GenreNode rootNode, SheetService.SheetGraphCommandOptions options)
    {
        //Log.Verbose("finding {Node}", options.Subgenre);
        var subgenreNode = FindNode(rootNode, options.Subgenre);
        //Log.Verbose("node: {Node}", subgenreNode.Name);
        var sb = GenerateSubgenreGraphString(subgenreNode, options); // subgenreNode
        //Log.Verbose("sb {L}", sb);

        File.WriteAllText("layout.gv", sb);
        var graph = new GraphViz();
        var imageBytes = graph.LayoutAndRender(null, sb, null, options.Engine, "png");
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

    private static string GenerateSubgenreGraphString(GenreNode rootNode, SheetService.SheetGraphCommandOptions options)
    {
        var sb = new StringBuilder("digraph G {\r\n");
        sb.AppendLine($"graph [engine={options.Engine},splines=compound,overlap=false,fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")},fontsize=12,];"); //ratio=0.421940928 bgcolor={Quote("transparent")}
        sb.AppendLine($"node [fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")},fontsize=12];");
        sb.AppendLine($"fontname={Quote("Roboto,RobotoDraft,Helvetica,Arial,sans-serif")}");
        sb.Append(GenerateSubgenreNodeString(rootNode));
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static StringBuilder GenerateSubgenreNodeString(GenreNode node)
    {
        var sb = new StringBuilder();

        if (node.IsMeta)
        {
            sb.AppendLine($"{Quote(node.Name)} [shape={Quote("box")},style={Quote("filled")},color={Quote(node.Color.ToString())}]");
        }
        else
        {
            sb.AppendLine($"{Quote(node.Name)} [shape={Quote("box")},style={Quote("rounded,filled")},color={Quote(node.Color.ToString())}]");
        }

        foreach (var subgenre in node.Subgenres.OrderBy(sg => sg.Name))
        {
            if (subgenre.IsRoot)
                continue;
            if (subgenre.IsMeta)
            {
                sb.AppendLine($"{Quote(subgenre.Name)} [shape={Quote("box")},style={Quote("filled")},color={Quote(node.Color.ToString())}]");
            }
            else
            {
                sb.AppendLine($"{Quote(subgenre.Name)} [shape={Quote("box")},style={Quote("rounded,filled")},color={Quote(node.Color.ToString())}]");
            }

            if (subgenre.Parent is not null)
                sb.AppendLine($"{Quote(subgenre.Parent.Name)}->{Quote(subgenre.Name)};");
            sb.Append(GenerateSubgenreNodeString(subgenre));
        }

        //Log.Verbose("generating node string for {Name}", node.Name);
        return sb;
    }

#endregion

#region Collab

    public byte[] RenderCollabs(CollabNode node, SheetService.CollabGraphCommandOptions options)
    {
        var sb = GenerateCollabGraphString(node, options);
        File.WriteAllText("collab.gv", sb.ToString());
        var graph = new GraphViz();
        var imageBytes = graph.LayoutAndRender(null, sb.ToString(), null, options.Engine, "png");
        return imageBytes;
    }

    private StringBuilder GenerateCollabGraphString(CollabNode node, SheetService.CollabGraphCommandOptions options)
    {
        var sb = new StringBuilder($$"""
graph G {
graph [engine={{options.Engine}}]
""");

        sb.Append(GenerateCollabNodeString(node));

        /*foreach (var artist in node.SubNodes)
        {
            if (string.Equals(artist.Name, options.StartArtist, StringComparison.OrdinalIgnoreCase))
                continue;
            sb.Append();
        }*/

        sb.AppendLine("}");
        return sb;
    }

    private StringBuilder GenerateCollabNodeString(CollabNode node)
    {
        var sb = new StringBuilder();

        foreach (var artist in node.SubNodes.OrderBy(sg => sg.Name))
        {
            if (artist.IsRoot)
                continue;

            sb.AppendLine($"{Quote(artist.Name)} [shape=box,style=rounded]");

            if (artist.Parent is not null)
                sb.AppendLine($"{Quote(artist.Parent.Name)}--{Quote(artist.Name)};");
            sb.Append(GenerateCollabNodeString(artist));
        }

        //sb.AppendLine($"{Quote(options.StartArtist)}--{Quote(artist)}");
        return sb;
    }

#endregion

    private static string Quote(string str)
    {
        return $"\"{str}\"";
    }
}