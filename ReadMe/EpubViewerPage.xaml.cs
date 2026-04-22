using ReadMe.Models;
using ReadMe.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
[QueryProperty(nameof(EpubUrl), "EpubUrl")]
public partial class EpubViewerPage : ContentPage
{
    private readonly HttpClient _httpClient = new();
    private Book? _book;
    private string? _epubUrl;
    private string? _lastLoadedUrl;
    private bool _isLoading;
    private int _currentPageIndex;

    public ObservableCollection<ReaderPage> Pages { get; } = [];

    public Book? Book
    {
        get => _book;
        set
        {
            _book = value;
            OnPropertyChanged();
            LoadEpub();
        }
    }

    public string? EpubUrl
    {
        get => _epubUrl;
        set
        {
            _epubUrl = Uri.UnescapeDataString(value ?? string.Empty);
            OnPropertyChanged();
            LoadEpub();
        }
    }

    public EpubViewerPage()
    {
        InitializeComponent();
        BindingContext = this;
      UpdatePageIndicator();
    }

    private async void LoadEpub()
    {
      if (_isLoading)
        {
            return;
        }

        var sourceUrl = !string.IsNullOrWhiteSpace(EpubUrl)
            ? EpubUrl
            : Book?.EpubFileUrl;

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            await DisplayAlert("Information", "Aucun lien de lecture disponible.", "OK");
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            return;
        }

        _isLoading = true;
        UpdateLoadingUi(true, "Preparation du telechargement...", 0);

        try
        {
            var resolvedEpubUrl = NormalizeUrl(sourceUrl);
            if (_lastLoadedUrl == resolvedEpubUrl)
            {
                return;
            }

            Debug.WriteLine($"[EPUB] Start download: {resolvedEpubUrl}");
            var epubBytes = await DownloadEpubWithProgressAsync(resolvedEpubUrl);
            Debug.WriteLine($"[EPUB] Download complete: {epubBytes.Length} bytes");

            UpdateLoadingUi(true, "Lecture du livre...", 1);
            var parsedPages = await Task.Run(() => ParseEpubToPages(epubBytes));
            Debug.WriteLine($"[EPUB] Parsed pages: {parsedPages.Count}");

            if (parsedPages.Count == 0)
            {
                throw new InvalidOperationException("Aucune page lisible trouvee dans cet EPUB.");
            }

            Pages.Clear();
            foreach (var page in parsedPages)
            {
                Pages.Add(page);
            }

            _currentPageIndex = 0;
            ReaderCarousel.Position = 0;
            UpdatePageIndicator();
            _lastLoadedUrl = resolvedEpubUrl;
            Debug.WriteLine("[EPUB] Native reader ready");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EPUB] Reader failed: {ex}");
            await DisplayAlert("Erreur lecture", "Impossible d'ouvrir ce fichier EPUB depuis l'API. Le backend ne sert probablement pas encore le fichier.", "OK");
        }
        finally
        {
            UpdateLoadingUi(false, string.Empty, 0);
            _isLoading = false;
        }
    }

    private async Task<byte[]> DownloadEpubWithProgressAsync(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var buffer = new MemoryStream();

            var chunk = new byte[16 * 1024];
            long readTotal = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length))) > 0)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead));
                readTotal += bytesRead;

                if (totalBytes.HasValue && totalBytes.Value > 0)
                {
                    var progress = (double)readTotal / totalBytes.Value;
                    var percent = (int)Math.Round(progress * 100);

                    UpdateLoadingUi(true, $"Telechargement EPUB: {percent}% ({readTotal / 1024} KB / {totalBytes.Value / 1024} KB)", progress);
                    Debug.WriteLine($"[EPUB] Download progress: {percent}% ({readTotal}/{totalBytes.Value} bytes)");
                }
                else
                {
                    UpdateLoadingUi(true, $"Telechargement EPUB: {readTotal / 1024} KB", 0);
                    Debug.WriteLine($"[EPUB] Download progress: {readTotal} bytes (unknown total)");
                }
            }

            UpdateLoadingUi(true, "Ouverture du livre...", 1);
            return buffer.ToArray();
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"[EPUB] Progressive stream disposed, fallback download: {ex.Message}");
            UpdateLoadingUi(true, "Reprise du telechargement...", 0);

            var bytes = await _httpClient.GetByteArrayAsync(url);

            Debug.WriteLine($"[EPUB] Fallback download complete: {bytes.Length} bytes");
            UpdateLoadingUi(true, "Ouverture du livre...", 1);
            return bytes;
        }
    }

    private static List<ReaderPage> ParseEpubToPages(byte[] epubBytes)
    {
        using var memory = new MemoryStream(epubBytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);

        var containerEntry = archive.GetEntry("META-INF/container.xml")
            ?? throw new InvalidOperationException("container.xml introuvable.");

        string containerXml;
        using (var containerStream = containerEntry.Open())
        using (var reader = new StreamReader(containerStream))
        {
            containerXml = reader.ReadToEnd();
        }

        var containerDoc = XDocument.Parse(containerXml);
        var rootFilePath = containerDoc
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "rootfile")
            ?.Attribute("full-path")
            ?.Value;

        if (string.IsNullOrWhiteSpace(rootFilePath))
        {
            throw new InvalidOperationException("fichier OPF introuvable.");
        }

        var opfEntryPath = NormalizeZipPath(rootFilePath);
        var opfEntry = archive.GetEntry(opfEntryPath)
            ?? throw new InvalidOperationException("OPF introuvable dans l'archive.");

        string opfXml;
        using (var opfStream = opfEntry.Open())
        using (var reader = new StreamReader(opfStream))
        {
            opfXml = reader.ReadToEnd();
        }

        var opfDoc = XDocument.Parse(opfXml);
        var manifest = opfDoc
            .Descendants()
            .Where(e => e.Name.LocalName == "item")
            .Select(e => new
            {
                Id = e.Attribute("id")?.Value,
                Href = e.Attribute("href")?.Value,
                MediaType = e.Attribute("media-type")?.Value
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Href))
            .ToDictionary(x => x.Id!, x => (Href: x.Href!, MediaType: x.MediaType ?? string.Empty));

        var spineIds = opfDoc
            .Descendants()
            .Where(e => e.Name.LocalName == "itemref")
            .Select(e => e.Attribute("idref")?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToList();

        var opfDirectory = GetDirectoryPath(rootFilePath);
        var result = new List<ReaderPage>();

        foreach (var idref in spineIds)
        {
            if (!manifest.TryGetValue(idref, out var item))
            {
                continue;
            }

            var isTextLike = item.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                || item.MediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || item.Href.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                || item.Href.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || item.Href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);

            if (!isTextLike)
            {
                continue;
            }

            var contentPath = NormalizeZipPath(CombinePosix(opfDirectory, item.Href));
            var contentEntry = archive.GetEntry(contentPath);
            if (contentEntry == null)
            {
                continue;
            }

            string html;
            using (var stream = contentEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
            }

            var cleanText = ExtractReadableText(html);
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                continue;
            }

            foreach (var pageText in SplitTextIntoPages(cleanText, 1800))
            {
                result.Add(new ReaderPage { Content = pageText });
            }
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("Contenu EPUB vide ou non supporte.");
        }

        return result;
    }

    private static IEnumerable<string> SplitTextIntoPages(string text, int maxChars)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new System.Text.StringBuilder(maxChars + 200);

        foreach (var word in words)
        {
            if (current.Length + word.Length + 1 > maxChars && current.Length > 0)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            yield return current.ToString().Trim();
        }
    }

    private static string ExtractReadableText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        var withoutStyles = Regex.Replace(withoutScripts, "<style[\\s\\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        var withBreaks = Regex.Replace(withoutStyles, "</(p|div|h1|h2|h3|h4|h5|h6|li|section|article|br)>", "\n", RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withBreaks, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var normalizedLines = Regex.Replace(decoded, "[\\t\\r ]+", " ");
        var normalizedParagraphs = Regex.Replace(normalizedLines, "\\n{3,}", "\n\n");

        return normalizedParagraphs.Trim();
    }

    private static string NormalizeZipPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string GetDirectoryPath(string path)
    {
        var normalized = NormalizeZipPath(path);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash >= 0 ? normalized[..lastSlash] : string.Empty;
    }

    private static string CombinePosix(string basePath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return NormalizeZipPath(relativePath);
        }

        var rootUri = new Uri($"http://epub.local/{NormalizeZipPath(basePath).TrimEnd('/')}/");
        var combined = new Uri(rootUri, relativePath).AbsolutePath;
        return NormalizeZipPath(combined);
    }

    private void OnReaderPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        _currentPageIndex = Math.Max(0, e.CurrentPosition);
        UpdatePageIndicator();
    }

    private void OnPrevClicked(object sender, EventArgs e)
    {
        if (Pages.Count == 0 || _currentPageIndex <= 0)
        {
            return;
        }

        ReaderCarousel.Position = _currentPageIndex - 1;
    }

    private void OnNextClicked(object sender, EventArgs e)
    {
        if (Pages.Count == 0 || _currentPageIndex >= Pages.Count - 1)
        {
            return;
        }

        ReaderCarousel.Position = _currentPageIndex + 1;
    }

    private void UpdatePageIndicator()
    {
        var total = Pages.Count;
        var current = total == 0 ? 0 : Math.Clamp(_currentPageIndex + 1, 1, total);
        PageIndicatorLabel.Text = $"Page {current} / {total}";
    }

    private void UpdateLoadingUi(bool isVisible, string status, double progress)
    {
        LoadingIndicator.IsVisible = isVisible;
        LoadingIndicator.IsRunning = isVisible;

        if (DownloadPanel != null)
        {
            DownloadPanel.IsVisible = isVisible;
        }

        if (DownloadStatusLabel != null)
        {
            DownloadStatusLabel.Text = status;
        }

        if (DownloadProgressBar != null)
        {
            DownloadProgressBar.Progress = Math.Clamp(progress, 0, 1);
        }
    }

    private static string NormalizeUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        return new Uri(new Uri(ApiHelper.BaseUrl), url).ToString();
    }

      public sealed class ReaderPage
      {
        public string Content { get; init; } = string.Empty;
      }
}
