﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceRelease : BeatsourceReleaseSummary
{
    [JsonProperty("artists"), JsonRequired]
    public List<BeatsourceArtistSummary> Artists { get; set; } = default!;

    public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

    [JsonProperty("bpm_range"), JsonRequired]
    public BeatsourceBpmRange BpmRange { get; set; } = default!;

    [JsonProperty("catalog_number"), JsonRequired]
    public string CatalogNumber { get; set; } = default!;

    [JsonProperty("desc")] // optional
    public string? Description { get; set; }

    [JsonProperty("enabled"), JsonRequired]
    public bool IsEnabled { get; set; } = default!;

    [JsonProperty("encoded_date"), JsonRequired]
    public DateTime EncodedDate { get; set; } = default!;

    [JsonProperty("exclusive"), JsonRequired]
    public bool IsExclusive { get; set; } = default!;

    [JsonProperty("is_explicit")] // todo: check if required
    public bool IsExplicit { get; set; }

    [JsonProperty("grid")]
    public string? GrId { get; set; }

    [JsonProperty("id"), JsonRequired]
    public new int Id { get; set; } = default!;

    [JsonProperty("image")]
    public new BeatsourceImage? Image { get; set; }

    [JsonProperty("is_available_for_streaming")] // optional
    public bool IsAvailableForStreaming { get; set; }

    [JsonProperty("label"), JsonRequired]
    public new BeatsourceLabelSummary Label { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public new string Name { get; set; } = default!;

    [JsonProperty("new_release_date"), JsonRequired]
    public DateTime NewReleaseDate { get; set; } = default!;

    [JsonProperty("override_price")] // optional
    public object? OverridePrice { get; set; }

    [JsonProperty("pre_order"), JsonRequired]
    public bool IsPreorder { get; set; } = default!;

    [JsonProperty("pre_order_date")] // optional
    public DateTime? PreorderDate { get; set; }

    [JsonProperty("price"), JsonRequired]
    public BeatsourcePrice Price { get; set; } = default!;

    [JsonProperty("price_override_firm"), JsonRequired]
    public bool IsPriceOverriden { get; set; } = default!;

    [JsonProperty("publish_date"), JsonRequired]
    public DateTime PublishDate { get; set; } = default!;

    [JsonProperty("remixers"), JsonRequired]
    public List<BeatsourceArtistSummary> Remixers { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public new string Slug { get; set; } = default!;

    [JsonProperty("tracks")] // missing when lite request
    public string[]? TrackUrls { get; set; }

    [JsonProperty("track_count"), JsonRequired]
    public int TrackCount { get; set; } = default!;

    [JsonProperty("type")] // optional
    public BeatsourceReleaseType? Type { get; set; }

    [JsonProperty("upc")] // can be null :(
    public string? Upc { get; set; }

    [JsonProperty("updated"), JsonRequired]
    public DateTime Updated { get; set; } = default!;

    [JsonProperty("url")] // optional
    public string? Url { get; set; }

    [JsonProperty("is_dj_edit"), JsonRequired]
    public bool IsDjEdit { get; set; } = default!;

    [JsonProperty("is_dj_remix"), JsonRequired]
    public bool IsDjRemix { get; set; } = default!;
}