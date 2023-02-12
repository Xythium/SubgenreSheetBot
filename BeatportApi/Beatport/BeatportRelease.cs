using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BeatportApi.Beatport;

public class BeatportRelease
{
    [JsonProperty("artists"), JsonRequired]
    public List<BeatportArtistSummary> Artists { get; set; }= default!;

    public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

    [JsonProperty("bpm_range"), JsonRequired]
    public BeatportBpmRange BpmRange { get; set; }= default!;

    [JsonProperty("catalog_number"), JsonRequired]
    public string CatalogNumber { get; set; }= default!;

    [JsonProperty("desc")] // optional
    public string? Description { get; set; }

    [JsonProperty("enabled"), JsonRequired]
    public bool IsEnabled { get; set; }= default!;

    [JsonProperty("encoded_date"), JsonRequired]
    public DateTime EncodedDate { get; set; }= default!;

    [JsonProperty("exclusive"), JsonRequired]
    public bool IsExclusive { get; set; }= default!;

    [JsonProperty("grid")]
    public string? Grid { get; set; }

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; }= default!;

    [JsonProperty("image"), JsonRequired]
    public BeatportImage Image { get; set; }= default!;

    [JsonProperty("is_available_for_streaming")] // todo ?
    public bool IsAvailableForStreaming { get; set; }
        
    [JsonProperty("is_hype"), JsonRequired]
    public bool IsHype { get; set; }= default!;

    [JsonProperty("label"), JsonRequired]
    public BeatportLabelSummary Label { get; set; }= default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; }= default!;

    [JsonProperty("new_release_date"), JsonRequired]
    public DateTime NewReleaseDate { get; set; }= default!;

    [JsonProperty("override_price")] // optional
    public object? OverridePrice { get; set; }

    [JsonProperty("pre_order"), JsonRequired]
    public bool IsPreorder { get; set; }= default!;

    [JsonProperty("pre_order_date")] // optional
    public DateTime? PreorderDate { get; set; }

    [JsonProperty("price"), JsonRequired]
    public BeatportPrice Price { get; set; }= default!;

    [JsonProperty("price_override_firm"), JsonRequired]
    public bool IsPriceOverriden { get; set; }= default!;

    [JsonProperty("publish_date"), JsonRequired]
    public DateTime PublishDate { get; set; }= default!;

    [JsonProperty("remixers"), JsonRequired]
    public List<BeatportArtistSummary> Remixers { get; set; }= default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; }= default!;

    [JsonProperty("track_count"), JsonRequired]
    public int TrackCount { get; set; }= default!;

    [JsonProperty("tracks")] // missing when lite request
    public string[]? TrackUrls { get; set; }

    [JsonProperty("type")] // optional
    public BeatportReleaseType? Type { get; set; }

    [JsonProperty("upc")] // can be null :(
    public string? Upc { get; set; }

    [JsonProperty("updated"), JsonRequired]
    public DateTime Updated { get; set; }

    [JsonProperty("url")] // optional
    public string? Url { get; set; }
}