
using System.Collections.Generic;

namespace Common.Monstercat;

public class MonstercatCatalogResponse
{
    public MonstercatRelease Release { get; set; }

    public List<MonstercatTrack> Tracks { get; set; }
}