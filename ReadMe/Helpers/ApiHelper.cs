using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Devices;

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
    public static string BaseUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:3000/"
        : "http://localhost:3000/";

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
        return await _httpClient.GetFromJsonAsync<T>(endpoint, JsonOptions, cancellationToken);
    }

    public async Task<byte[]> GetBytesAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetByteArrayAsync(endpoint, cancellationToken);
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest payload,
        CancellationToken cancellationToken = default)
    {
        var content = CreateJsonContent(payload);
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    public async Task<TResponse?> PostMultipartAsync<TResponse>(
        string endpoint,
        MultipartFormDataContent content,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
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
        using var response = await _httpClient.PutAsync(endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync(endpoint, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static StringContent CreateJsonContent<T>(T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}