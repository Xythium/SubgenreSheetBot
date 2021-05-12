using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace BeatportApi
{
    public class BeatportRelease
    {
        [JsonProperty("artists"), JsonRequired]
        public List<BeatportArtistSummary> Artists { get; set; }

        public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

        [JsonProperty("bpm_range"), JsonRequired]
        public BeatportBpmRange BpmRange { get; set; }

        [JsonProperty("catalog_number"), JsonRequired]
        public string CatalogNumber { get; set; }

        [JsonProperty("desc")] // optional
        public string Description { get; set; }

        [JsonProperty("enabled"), JsonRequired]
        public bool IsEnabled { get; set; }

        [JsonProperty("encoded_date"), JsonRequired]
        public DateTime EncodedDate { get; set; }

        [JsonProperty("exclusive"), JsonRequired]
        public bool IsExclusive { get; set; }

        [JsonProperty("grid")]
        public string Grid { get; set; }

        [JsonProperty("id"), JsonRequired]
        public int Id { get; set; }

        [JsonProperty("image"), JsonRequired]
        public BeatportImage Image { get; set; }

        [JsonProperty("is_hype"), JsonRequired]
        public bool IsHype { get; set; }

        [JsonProperty("label"), JsonRequired]
        public BeatportLabelSummary Label { get; set; }

        [JsonProperty("name"), JsonRequired]
        public string Name { get; set; }

        [JsonProperty("new_release_date"), JsonRequired]
        public DateTime NewReleaseDate { get; set; }

        [JsonProperty("override_price")] // optional
        public object OverridePrice { get; set; }

        [JsonProperty("pre_order"), JsonRequired]
        public bool IsPreorder { get; set; }

        [JsonProperty("pre_order_date")] // optional
        public DateTime? PreorderDate { get; set; }

        [JsonProperty("price"), JsonRequired]
        public BeatportPrice Price { get; set; }

        [JsonProperty("price_override_firm"), JsonRequired]
        public bool IsPriceOverriden { get; set; }

        [JsonProperty("publish_date"), JsonRequired]
        public DateTime PublishDate { get; set; }

        [JsonProperty("remixers"), JsonRequired]
        public List<BeatportArtistSummary> Remixers { get; set; }

        [JsonProperty("slug"), JsonRequired]
        public string Slug { get; set; }

        [JsonProperty("track_count"), JsonRequired]
        public int TrackCount { get; set; }

        [JsonProperty("tracks")] // missing when lite request
        public string[] TrackUrls { get; set; }

        [JsonProperty("type")] // optional
        public BeatportReleaseType Type { get; set; }

        [JsonProperty("upc")] // can be null :(
        public string Upc { get; set; }

        [JsonProperty("updated"), JsonRequired]
        public DateTime Updated { get; set; }

        [JsonProperty("url")] // optional
        public string Url { get; set; }
    }
}