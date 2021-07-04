using Newtonsoft.Json;

namespace BeatportApi.Beatport
{
    public class BeatportPrice
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