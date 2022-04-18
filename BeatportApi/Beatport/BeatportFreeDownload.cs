using System;
using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportFreeDownload
{
    [JsonProperty("url"), JsonRequired]
    public string Url { get; set; }

    [JsonProperty("updated_date"), JsonRequired]
    public DateTime UpdatedDate { get; set; }

    [JsonProperty("updated_person_id")]
    public string UpdatedPersonId { get; set; }

    [JsonProperty("created_date"), JsonRequired]
    public DateTime CreatedDate { get; set; }

    [JsonProperty("created_person_id")]
    public string CreatedPersonId { get; set; }

    [JsonProperty("start_date"), JsonRequired]
    public DateTime StartDate { get; set; }

    [JsonProperty("end_date"), JsonRequired]
    public DateTime EndDate { get; set; }
        
    [JsonProperty("track"), JsonRequired]
    public string TrackUrl { get; set; }
}