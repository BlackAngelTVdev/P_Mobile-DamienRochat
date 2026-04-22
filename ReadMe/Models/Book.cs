using System.Text.Json.Serialization;
using ReadMe.Helpers;

namespace ReadMe.Models;

public class Book
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("cover_image_path")]
    public string? CoverImagePath { get; set; }

    [JsonPropertyName("epub_file_path")]
    public string EpubFilePath { get; set; } = string.Empty;

    [JsonPropertyName("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [JsonPropertyName("isbn")]
    public string? Isbn { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("publish_date")]
    public DateTime? PublishDate { get; set; }

    [JsonPropertyName("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    public string MetaLine => string.IsNullOrWhiteSpace(Author) ? "Auteur inconnu" : Author;

    public string CoverImageUrl => ResolveUrl(CoverImagePath);

    public string PublishDateText => PublishDate?.ToString("dd/MM/yyyy") ?? "Date inconnue";

    public string DetailLine => $"{FormatFileSize(FileSizeBytes)} • publié le {PublishDateText}";

    public string SecondaryLine => $"ISBN: {GetValueOrDefault(Isbn)} • {GetValueOrDefault(Language)}";

    public string UploadedLine => $"Ajouté le {UploadedAt:dd/MM/yyyy}";

    public string DescriptionLine => string.IsNullOrWhiteSpace(Description)
        ? "Aucune description"
        : Description;

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private static string GetValueOrDefault(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "N/A" : value;
    }

    private static string ResolveUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return new Uri(new Uri(ApiHelper.BaseUrl), path).ToString();
    }
}