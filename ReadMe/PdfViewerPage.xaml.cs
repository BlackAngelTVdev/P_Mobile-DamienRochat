using ReadMe.Models;
using System.Web; // N�cessaire pour HttpUtility

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class PdfViewerPage : ContentPage
{
    // Correction : Utilisation de nullable ? pour �viter l'erreur de constructeur
    private Book? _book;
    public Book? Book
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

        // On v�rifie si PdfWebView existe avant d'abonner l'�v�nement
        // (�vite les plantages si le XAML n'est pas encore bien g�n�r�)
        if (PdfWebView != null)
        {
            PdfWebView.Navigated += (s, e) => {
                if (LoadingIndicator != null)
                {
                    LoadingIndicator.IsRunning = false;
                    LoadingIndicator.IsVisible = false;
                }
            };
        }
    }

    private void LoadPdf()
    {
        // ATTENTION : ce viewer suit maintenant le champ EpubFilePath du modèle
        if (Book != null && !string.IsNullOrEmpty(Book.EpubFilePath))
        {
            string url = Book.EpubFilePath;

            if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // On utilise HttpUtility pour encoder l'URL proprement
                url = $"https://docs.google.com/gview?embedded=true&url={HttpUtility.UrlEncode(url)}";
            }

            if (PdfWebView != null)
            {
                PdfWebView.Source = url;
            }
        }
    }
}