using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Authentication;
using Giga3DApp.Models;

namespace Giga3DApp.Services;

public sealed class GigaChatClient
{
    private const string AuthKey = "ZTEwNjk3NzYtYzNiNS00OTJlLWIxNDktMDUxZjE4OWU2M2I4OmJmYjdlZmI5LWJiYTYtNGZkNS1iNWYzLTdhYzk5MzQwNjk3MA==";
    private const string ClientId = "e1069776-c3b5-492e-b149-051f189e63b8";
    private const string Scope = "GIGACHAT_API_PERS";
    private const string OauthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
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

    public async Task<string> GenerateAssetSpecAsync(string selectedModelId, string promptText, CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var payload = new
        {
            model = selectedModelId,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "Ты — генератор 3D-ассетов. Верни только валидный JSON без Markdown. Формат: {\"assetName\":\"string\",\"baseShape\":\"box|sphere|cylinder|pyramid|chair|rocket\",\"size\":{\"x\":number,\"y\":number,\"z\":number},\"colors\":[\"#RRGGBB\",...],\"style\":\"string\",\"description\":\"string\"}. Не добавляй пояснений."
                },
                new
                {
                    role = "user",
                    content = $"Сгенерируй 3D-объект по описанию: {promptText}. Сделай объект пригодным для процедурной генерации в низкополигональной 3D-модели для Windows."
                }
            },
            temperature = 0.7,
            max_tokens = 500
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"GenerateAssetSpec failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("GigaChat returned empty content.");
        }

        var cleaned = ExtractJson(content);
        return cleaned;
    }

    private static string ExtractJson(string input)
    {
        var text = input.Trim();

        if (text.StartsWith("```"))
        {
            var firstFence = text.IndexOf('\n');
            if (firstFence >= 0)
            {
                text = text[(firstFence + 1)..];
            }

            var lastFence = text.LastIndexOf("```");
            if (lastFence >= 0)
            {
                text = text[..lastFence].Trim();
            }
        }

        return text;
    }
}
