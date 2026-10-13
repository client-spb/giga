using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Authentication;
using System.IO;
using Giga3DApp.Models;

namespace Giga3DApp.Services;

public sealed class GigaChatClient
{
    private const string AuthKey = "ZTEwNjk3NzYtYzNiNS00OTJlLWIxNDktMDUxZjE4OWU2M2I4OmJmYjdlZmI5LWJiYTYtNGZkNS1iNWYzLTdhYzk5MzQwNjk3MA==";
    private const string ClientId = "e1069776-c3b5-492e-b149-051f189e63b8";
    private const string Scope = "GIGACHAT_API_PERS";
    private const string OauthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
    private const string ApiRoot = "https://api.giga.chat";
    private const string BaseUrl = "https://api.giga.chat/v1";

    private readonly HttpClient _httpClient;

    public GigaChatClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            return;
        }

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, OauthUrl);
        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", AuthKey);

        var form = new Dictionary<string, string>
        {
            ["scope"] = Scope,
            ["client_id"] = ClientId
        };

        request.Content = new FormUrlEncodedContent(form);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OAuth failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        var tokenResponse = JsonSerializer.Deserialize<GigaChatTokenResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (string.IsNullOrWhiteSpace(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException($"OAuth response did not contain access_token. Body: {body}");
        }

        return tokenResponse.AccessToken;
    }

    public async Task<List<GigaChatModel>> GetModelsAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GetModels failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        var models = JsonSerializer.Deserialize<GigaChatModelsResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return models?.Data ?? new List<GigaChatModel>();
    }

    public async Task<GeneratedModelFile> Generate3DModelAsync(string selectedModelId, string promptText, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var payload = new
        {
            model = selectedModelId,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new[] { new { text = promptText } }
                }
            },
            tool_config = new { mode = "forced", tool_name = "model_3d_generate" },
            tools = new[] { new { model_3d_generate = new { } } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiRoot}/v2/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GigaChat 3D generation failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var fileId = FindGeneratedModelFileId(document.RootElement);
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new InvalidOperationException($"GigaChat завершил ответ без файла 3D-модели. Ответ API: {body}");
        }

        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/files/{Uri.EscapeDataString(fileId)}/content");
        fileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var fileResponse = await _httpClient.SendAsync(fileRequest, cancellationToken);
        if (!fileResponse.IsSuccessStatusCode)
        {
            var errorBody = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Не удалось скачать 3D-модель: {(int)fileResponse.StatusCode} {fileResponse.ReasonPhrase}. Body: {errorBody}");
        }

        var mediaType = fileResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var extension = mediaType.ToLowerInvariant() switch
        {
            "model/gltf-binary" => ".glb",
            "model/gltf+json" => ".gltf",
            "model/obj" or "text/obj" => ".obj",
            "application/zip" => ".zip",
            _ => ".glb"
        };

        Directory.CreateDirectory(outputDirectory);
        var filePath = Path.Combine(outputDirectory, $"GigaChatModel_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
        await using (var output = File.Create(filePath))
        {
            await fileResponse.Content.CopyToAsync(output, cancellationToken);
        }

        return new GeneratedModelFile(filePath, mediaType);
    }

    private static string? FindGeneratedModelFileId(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("target", out var target)
                && target.ValueKind == JsonValueKind.String
                && string.Equals(target.GetString(), "3dmodel", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String)
            {
                return id.GetString();
            }

            foreach (var property in root.EnumerateObject())
            {
                var found = FindGeneratedModelFileId(property.Value);
                if (!string.IsNullOrWhiteSpace(found))
                    return found;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var found = FindGeneratedModelFileId(item);
                if (!string.IsNullOrWhiteSpace(found))
                    return found;
            }
        }

        return null;
    }
}

public sealed record GeneratedModelFile(string FilePath, string MediaType);
