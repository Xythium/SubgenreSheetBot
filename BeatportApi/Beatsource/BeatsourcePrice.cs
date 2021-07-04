using Newtonsoft.Json;

namespace BeatportApi.Beatsource
{
    public class BeatsourcePrice
    {
        [JsonProperty("code"), JsonRequired]
        public string Code { get; set; }

        [JsonProperty("display"), JsonRequired]
        public string Display { get; set; }

        [JsonProperty("symbol"), JsonRequired]
        public string Symbol { get; set; }

        [JsonProperty("value"), JsonRequired]
        public decimal Value { get; set; }
    }
}