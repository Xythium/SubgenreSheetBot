using System.Collections.Generic;
using Discord;
using Newtonsoft.Json;

namespace SubgenreSheetBot.Services;

public class GenreNode
{
    public string Name { get; init; }

    public bool IsRoot { get; init; }

    public bool IsMeta { get; set; }

    [JsonIgnore]
    public GenreNode? Parent { get; set; }

    public List<GenreNode> Subgenres { get; set; } = new List<GenreNode>();

    public Color Color { get; set; }

    public static List<GenreNode> All = new List<GenreNode>();

    public GenreNode()
    {
        All.Add(this);
    }

    public void AddSubgenre(GenreNode node)
    {
        /*if (node.Parent is not null)
        {
            if (node.Parent == this)
                throw new Exception("Subgenre already added to this parent");
            throw new Exception("Parent already set");
        }*/

        node.Parent = this;
        Subgenres.Add(node);
    }

    public bool ShouldSerializeSubgenres()
    {
        return Subgenres.Count > 0;
    }

    public bool ShouldSerializeIsRoot()
    {
        return IsRoot;
    }

    public bool ShouldSerializeIsMeta()
    {
        return IsMeta;
    }
}