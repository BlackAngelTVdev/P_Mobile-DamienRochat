using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace ReadMe.Helpers;

public interface IApiHelper
{
    Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<byte[]> GetBytesAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken cancellationToken = default);
    Task<TResponse?> PostMultipartAsync<TResponse>(string endpoint, MultipartFormDataContent content, CancellationToken cancellationToken = default);
    Task<TResponse?> PutAsync<TRequest, TResponse>(string endpoint, TRequest payload, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default);
}

public class ApiHelper : IApiHelper
{
    private const string BaseUrlPreferenceKey = "ApiBaseUrl";

    public static string DefaultBaseUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:3000/"
        : "http://localhost:3000/";

    public static string BaseUrl => NormalizeBaseUrl(
        Preferences.Default.Get(BaseUrlPreferenceKey, DefaultBaseUrl));

    public static void SetBaseUrl(string? baseUrl)
    {
        Preferences.Default.Set(BaseUrlPreferenceKey, NormalizeBaseUrl(baseUrl));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiHelper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<T?> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<T>(ResolveUri(endpoint), JsonOptions, cancellationToken);
    }

    public async Task<byte[]> GetBytesAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetByteArrayAsync(ResolveUri(endpoint), cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        var content = CreateJsonContent(payload);
        using var response = await _httpClient.PostAsync(ResolveUri(endpoint), content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    public async Task<TResponse?> PostMultipartAsync<TResponse>(
        string endpoint,
        MultipartFormDataContent content,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(ResolveUri(endpoint), content, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    public async Task<TResponse?> PutAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        var content = CreateJsonContent(payload);
        using var response = await _httpClient.PutAsync(ResolveUri(endpoint), content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(ResolveUri(endpoint), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static Uri ResolveUri(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        return new Uri(new Uri(BaseUrl), endpoint);
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return DefaultBaseUrl;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedUri))
        {
            return DefaultBaseUrl;
        }

        var normalized = parsedUri.ToString();
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}