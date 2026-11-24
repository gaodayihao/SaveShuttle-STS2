using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SaveShuttle;

internal sealed class ManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; }
}

internal sealed class SaveArchiveManifest
{
    public const string FormatId = "sts2-save-shuttle";

    [JsonPropertyName("format")]
    public string Format { get; set; } = FormatId;

    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; }

    [JsonPropertyName("mods")]
    public List<string> Mods { get; set; } = new List<string>();

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; set; } = new List<ManifestFile>();

    public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static SaveArchiveManifest FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveArchiveManifest>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public bool IsCompatible => Format == FormatId && FormatVersion == 1;
}
