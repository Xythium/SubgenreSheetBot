using System;
using Common.Beatport.Api;

namespace Common;

public class GenericFreeDownload
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public static GenericFreeDownload FromTrack(BeatportFreeDownload dl)
    {
        return new GenericFreeDownload
        {
            Start = dl.StartDate,
            End = dl.EndDate
        };
    }
}