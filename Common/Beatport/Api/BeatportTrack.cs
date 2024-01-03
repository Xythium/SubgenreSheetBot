using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Common.Beatport.Api;

public class BeatportTrack
{
    [JsonProperty("artists"), JsonRequired]
    public List<BeatportArtistSummary> Artists { get; set; } = default!;

    [JsonProperty("audio_format")] // todo
    public object? AudioFormat { get; set; }

    public string ArtistConcat => string.Join(" x ", Artists.Select(a => a.Name));

    [JsonProperty("available_worldwide"), JsonRequired]
    public bool IsAvailableWorldwide { get; set; } = default!;

    [JsonProperty("bpm")] // optional
    public int? Bpm { get; set; }

    [JsonProperty("catalog_number"), JsonRequired]
    public string CatalogNumber { get; set; } = default!;

    [JsonProperty("copyright")] // optional
    public string? Copyright { get; set; }

    [JsonProperty("current_status"), JsonRequired]
    public BeatportStatusSummary CurrentStatus { get; set; } = default!;

    [JsonProperty("desc")] // optional
    public string? Description { get; set; }

    [JsonProperty("enabled")] // todo
    public bool IsEnabled { get; set; }

    [JsonProperty("encode_status")]
    public string? EncodeStatus { get; set; }

    [JsonProperty("encoded_date"), JsonRequired]
    public DateTime EncodedDate { get; set; } = default!;

    [JsonProperty("exclusive"), JsonRequired]
    public bool IsExclusive { get; set; } = default!;

    [JsonProperty("exclusive_period")] // optional
    public BeatportExclusiveSummary? ExclusivePeriod { get; set; }

    [JsonProperty("free_download_end_date")] // optional
    public DateTime? FreeDownloadEndDate { get; set; }

    [JsonProperty("free_download_start_date")] // optional
    public DateTime? FreeDownloadStartDate { get; set; }

    [JsonProperty("free_downloads"), JsonRequired]
    public BeatportFreeDownload[] FreeDownloads { get; set; } = default!;

    [JsonProperty("genre"), JsonRequired]
    public BeatportGenreSummary Genre { get; set; } = default!;

    [JsonProperty("hidden")]
    public bool? IsHidden { get; set; }

    [JsonProperty("id"), JsonRequired]
    public int Id { get; set; } = default!;

    [JsonProperty("image")]
    public BeatportImage? Image { get; set; }

    [JsonProperty("is_available_for_streaming"), JsonRequired]
    public bool IsStreaming { get; set; } = default!;

    [JsonProperty("is_classic")]
    public bool? IsClassic { get; set; }

    [JsonProperty("is_hype"), JsonRequired]
    public bool IsHype { get; set; } = default!;

    [JsonProperty("isrc")] // optional
    public string? Isrc { get; set; }

    [JsonProperty("key")]
    public BeatportKeySummary? Key { get; set; }

    [JsonProperty("label_track_identifier")]
    public string? LabelTrackIdentifier { get; set; }

    [JsonProperty("length")]
    public TimeSpan Length => TimeSpan.FromMilliseconds(LengthMs);

    [JsonProperty("length_ms"), JsonRequired]
    public int LengthMs { get; set; } = default!;

    [JsonProperty("mix_name"), JsonRequired]
    public string MixName { get; set; } = default!;

    [JsonProperty("name"), JsonRequired]
    public string Name { get; set; } = default!;

    [JsonProperty("new_release_date"), JsonRequired]
    public DateTime NewReleaseDate { get; set; } = default!;

    [JsonProperty("number")]
    public int? Number { get; set; }

    [JsonProperty("pre_order"), JsonRequired]
    public bool IsPreorder { get; set; } = default!;

    [JsonProperty("pre_order_date")] // optional
    public DateTime? PreorderDate { get; set; }

    [JsonProperty("price"), JsonRequired]
    public BeatportPrice Price { get; set; } = default!;

    [JsonProperty("publish_date"), JsonRequired]
    public DateTime PublishDate { get; set; } = default!;

    [JsonProperty("publish_status"), JsonRequired]
    public string PublishStatus { get; set; } = default!;

    [JsonProperty("release"), JsonRequired]
    public BeatportReleaseSummary Release { get; set; } = default!;

    [JsonProperty("remixers"), JsonRequired]
    public List<BeatportArtistSummary> Remixers { get; set; } = default!;

    [JsonProperty("sale_type"), JsonRequired]
    public BeatportSaleTypeSummary SaleType { get; set; } = default!;

    [JsonProperty("sample_end")]
    public TimeSpan? SampleEnd => SampleEndMs != null ? TimeSpan.FromMilliseconds(SampleEndMs.Value) : null;

    [JsonProperty("sample_end_ms")]
    public int? SampleEndMs { get; set; }

    [JsonProperty("sample_start")]
    public TimeSpan? SampleStart => SampleStartMs != null ? TimeSpan.FromMilliseconds(SampleStartMs.Value) : null;

    [JsonProperty("sample_start_ms")]
    public int? SampleStartMs { get; set; }

    [JsonProperty("sample_url"), JsonRequired]
    public string SampleUrl { get; set; } = default!;

    [JsonProperty("slug"), JsonRequired]
    public string Slug { get; set; } = default!;

    [JsonProperty("sub_genre")] // optional
    public BeatportSubgenreSummary? Subgenre { get; set; }

    [JsonProperty("territories")] // idk why
    public int[]? Territories { get; set; }

    [JsonProperty("url")] // optional
    public string? Url { get; set; }

    [JsonProperty("was_ever_exclusive")]
    public bool? WasEverExclusive { get; set; }
}