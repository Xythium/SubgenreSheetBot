using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace BeatportApi.Beatsource;

public class BeatsourceTrack
{
    [JsonProperty("artists"), JsonRequired]
    public List<BeatsourceArtistSummary> Artists { get; set; } = default!;

    public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

    [JsonProperty("audio_format")]
    public object? AudioFormat { get; set; }

    [JsonProperty("available_worldwide"), JsonRequired]
    public bool IsAvailableWorldwide { get; set; } = default!;

    [JsonProperty("bpm")] // optional
    public int? Bpm { get; set; }

    [JsonProperty("catalog_number"), JsonRequired]
    public string CatalogNumber { get; set; } = default!;

    [JsonProperty("copyright")] // optional, might not exist
    public string? Copyright { get; set; }

    [JsonProperty("current_status"), JsonRequired]
    public BeatsourceStatusSummary CurrentStatus { get; set; } = default!;

    [JsonProperty("desc")] // optional
    public string? Description { get; set; }

    [JsonProperty("enabled"), JsonRequired]
    public bool IsEnabled { get; set; } = default!;

    [JsonProperty("encode_status"), JsonRequired]
    public string EncodeStatus { get; set; } = default!;

    [JsonProperty("encoded_date"), JsonRequired]
    public DateTime EncodedDate { get; set; } = default!;

    [JsonProperty("exclusive"), JsonRequired]
    public bool IsExclusive { get; set; } = default!;

    [JsonProperty("exclusive_period"), JsonRequired]
    public BeatsourceExclusiveSummary ExclusivePeriod { get; set; } = default!;

    [JsonProperty("free_downloads"), JsonRequired]
    public object[] FreeDownloads { get; set; } = default!;

    [JsonProperty("free_download_end_date")] // optional
    public DateTime? FreeDownloadEndDate { get; set; }

    [JsonProperty("free_download_start_date")] // optional
    public DateTime? FreeDownloadStartDate { get; set; }

    [JsonProperty("genre"), JsonRequired]
    public BeatsourceGenreSummary Genre { get; set; } = default!;

    [JsonProperty("hidden"), JsonRequired]
    public bool IsHidden { get; set; } = default!;

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image")]
    public BeatsourceImage? Image { get; set; }

    [JsonProperty("is_available_for_streaming"), JsonRequired]
    public bool IsStreaming { get; set; } = default!;

    [JsonProperty("is_classic"), JsonRequired]
    public bool IsClassic { get; set; } = default!;

    [JsonProperty("isrc")] // optional
    public string? Isrc { get; set; }

    [JsonProperty("key"), JsonRequired]
    public BeatsourceKeySummary Key { get; set; } = default!;

    [JsonProperty("label_track_identifier")]
    public string? LabelTrackIdentifier { get; set; }

    [JsonProperty("length")]
    public TimeSpan Length => TimeSpan.FromMilliseconds(LengthMs);

    [JsonProperty("length_ms")] // optional?
    public int LengthMs { get; set; }

    [JsonProperty("mix_name"), JsonRequired]
    public string MixName { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("new_release_date"), JsonRequired]
    public DateTime NewReleaseDate { get; set; } = default!;

    [JsonProperty("number"), JsonRequired]
    public int Number { get; set; } = default!;

    [JsonProperty("pre_order"), JsonRequired]
    public bool IsPreorder { get; set; } = default!;

    [JsonProperty("pre_order_date")] // optional
    public DateTime? PreorderDate { get; set; }

    [JsonProperty("price"), JsonRequired]
    public BeatsourcePrice Price { get; set; } = default!;

    [JsonProperty("publish_date"), JsonRequired]
    public DateTime PublishDate { get; set; } = default!;

    [JsonProperty("publish_status"), JsonRequired]
    public string PublishStatus { get; set; } = default!;

    [JsonProperty("release"), JsonRequired]
    public BeatsourceReleaseSummary Release { get; set; } = default!;

    [JsonProperty("remixers"), JsonRequired]
    public List<BeatsourceArtistSummary> Remixers { get; set; } = default!;

    [JsonProperty("sale_type"), JsonRequired]
    public BeatsourceSaleTypeSummary SaleType { get; set; } = default!;

    [JsonProperty("sample_url"), JsonRequired]
    public string SampleUrl { get; set; } = default!;

    [JsonProperty("sample_start")]
    public TimeSpan SampleStart => TimeSpan.FromMilliseconds(SampleStartMs);

    [JsonProperty("sample_start_ms"), JsonRequired]
    public int SampleStartMs { get; set; } = default!;

    [JsonProperty("sample_end")]
    public TimeSpan SampleEnd => TimeSpan.FromMilliseconds(SampleEndMs);

    [JsonProperty("sample_end_ms"), JsonRequired]
    public int SampleEndMs { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;

    [JsonProperty("sub_genre")] // optional
    public BeatsourceSubgenreSummary? Subgenre { get; set; }

    [JsonProperty("territories")]
    public int[]? Territories { get; set; } // not required anymore when worldwide

    [JsonProperty("url")] // optional
    public string? Url { get; set; }

    [JsonProperty("was_ever_exclusive"), JsonRequired]
    public bool WasEverExclusive { get; set; } = default!;

    [JsonProperty("is_explicit"), JsonRequired]
    public bool IsExplicit { get; set; } = default!;

    [JsonProperty("is_available_for_alacarte"), JsonRequired]
    public bool IsAvailableForAlacarte { get; set; } = default!;

    [JsonProperty("is_promo_pool_track"), JsonRequired]
    public bool IsPromoPoolTrack { get; set; } = default!;

    [JsonProperty("is_dj_edit"), JsonRequired]
    public bool IsDjEdit { get; set; } = default!;

    [JsonProperty("is_dj_remix"), JsonRequired]
    public bool IsDjRemix { get; set; } = default!;

    [JsonProperty("bsrc_remixer"), JsonRequired]
    public string[] BsrcRemixer { get; set; } = default!;
}