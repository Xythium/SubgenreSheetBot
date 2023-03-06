using System.Collections.Generic;

namespace Common.SubgenreSheet;

public struct KeyCount
{
    public string Key;
    public int Count;
}

public struct KeyCount<T>
{
    public string Key;
    public int Count;
    public List<T> Elements;
}