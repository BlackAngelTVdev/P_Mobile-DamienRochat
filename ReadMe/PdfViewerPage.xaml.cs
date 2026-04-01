using ReadMe.Models;
using System.Web; // Nécessaire pour HttpUtility

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class PdfViewerPage : ContentPage
{
    // Correction : Utilisation de nullable ? pour éviter l'erreur de constructeur
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

        // On vérifie si PdfWebView existe avant d'abonner l'événement
        // (Évite les plantages si le XAML n'est pas encore bien généré)
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
        // ATTENTION : Remplacement de PdfUrl par Extrait selon ton modèle
        if (Book != null && !string.IsNullOrEmpty(Book.Extrait))
        {
            string url = Book.Extrait;

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