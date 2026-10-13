using System.Text.Json.Serialization;

namespace Giga3DApp.Models;

public sealed class GigaChatTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }
}

public sealed class GigaChatModelsResponse
{
    public List<GigaChatModel> Data { get; set; } = new();
}

public sealed class GigaChatModel
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public string OwnedBy { get; set; } = string.Empty;
    public long Created { get; set; }
    public string? Root { get; set; }
    public string? Parent { get; set; }
    public List<string>? Permission { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public sealed class GeneratedAssetSpec
{
    public string AssetName { get; set; } = "GeneratedAsset";
    public string BaseShape { get; set; } = "box";
    public AssetSize Size { get; set; } = new();
    public List<string> Colors { get; set; } = new();
    public string Style { get; set; } = "stylized low-poly";
    public string Description { get; set; } = string.Empty;
}

public sealed class AssetSize
{
    public double X { get; set; } = 1.0;
    public double Y { get; set; } = 1.0;
    public double Z { get; set; } = 1.0;
}
