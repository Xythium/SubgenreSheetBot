using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportStream
{
    [JsonProperty("sample_end_ms"), JsonRequired]
    public int SampleEndMs { get; set; } = default!;

    [JsonProperty("sample_start_ms"), JsonRequired]
    public int SampleStartMs { get; set; } = default!;

    [JsonProperty("stream_url"), JsonRequired]
    public string StreamUrl { get; set; } = default!;
}