using ReadMe.Models;

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class PdfViewerPage : ContentPage
{
    private Book _book;
    public Book Book
    {
        get => _book;
        set
        {
            _book = value;
            OnPropertyChanged();
            LoadPdf();
        }
    }

    public PdfViewerPage()
    {
        InitializeComponent();
        BindingContext = this;
        
        PdfWebView.Navigated += (s, e) => {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        };
    }

    private void LoadPdf()
    {
        if (Book != null && !string.IsNullOrEmpty(Book.PdfUrl))
        {
            string url = Book.PdfUrl;

            // Note: For Android, WebView often requires a viewer like Google Docs to display remote PDFs
            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                url = $"https://docs.google.com/gview?embedded=true&url={System.Web.HttpUtility.UrlEncode(url)}";
            }

            PdfWebView.Source = url;
        }
    }
}
