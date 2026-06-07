using System.Text.Json.Serialization;

namespace OrionLauncher.Models;

public class ModManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("mods")]
    public List<ModManifestEntry> Mods { get; set; } = [];
}

public class ModManifestEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("shortDescription")]
    public string ShortDescription { get; set; } = "";

    [JsonPropertyName("zipUrl")]
    public string ZipUrl { get; set; } = "";
}

public class ServerManifest
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("servers")]
    public List<ServerEntry> Servers { get; set; } = [];
}
