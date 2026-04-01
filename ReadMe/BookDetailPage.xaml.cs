using Microsoft.Extensions.DependencyInjection;
using ReadMe.Helpers;
using ReadMe.Models;

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class BookDetailPage : ContentPage
{
    private readonly IApiHelper _apiHelper;
    private Book? _book;
    private bool _isBusy;
    private int _selectedRating = 5;

    public Book? Book
    {
        get => _book;
        set { _book = value; OnPropertyChanged(); }
    }

    // Utilisation de 'new' pour éviter le conflit avec la propriété native
    public new bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public BookDetailPage()
    {
        // Si cette ligne reste rouge après un Rebuild, vérifie le x:Class dans ton XAML
        InitializeComponent();
        BindingContext = this;

        _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
            ?? new ApiHelper(new HttpClient { BaseAddress = new Uri(ApiHelper.BaseUrl) });

        // On attend que l'UI soit prête pour afficher les étoiles
        Dispatcher.Dispatch(() => UpdateStars());
    }

    private void OnStarTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string ratingStr && int.TryParse(ratingStr, out int rating))
        {
            _selectedRating = rating;
            UpdateStars();
        }
    }

    private void UpdateStars()
    {
        // Le check 'null' ici évite le crash si le lien XAML/C# est encore instable
        if (StarRatingLayout?.Children == null) return;

        for (int i = 0; i < StarRatingLayout.Children.Count; i++)
        {
            if (StarRatingLayout.Children[i] is Label starLabel)
            {
                starLabel.TextColor = (i < _selectedRating) ? Color.FromArgb("#6A44A3") : Color.FromArgb("#DCCCF0");
                starLabel.Text = (i < _selectedRating) ? "★" : "☆";
            }
        }
    }

    private async void OnSubmitReviewClicked(object sender, EventArgs e)
    {
        // Sécurité si l'Entry n'est pas encore bindée
        if (CommentEntry == null || string.IsNullOrWhiteSpace(CommentEntry.Text))
        {
            await DisplayAlert("Information", "Veuillez saisir un commentaire.", "OK");
            return;
        }

        try
        {
            IsBusy = true;
            var newComment = new BookComment { UserId = 1, Title = CommentEntry.Text };

            // Simulation API
            await _apiHelper.PostAsync<BookComment, BookComment>($"books/{Book?.Id}/comments", newComment);

            Book?.Comments?.Add(newComment);
            OnPropertyChanged(nameof(Book));
            CommentEntry.Text = string.Empty;

            await DisplayAlert("Succès", "Votre avis a été enregistré !", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
            await DisplayAlert("Information", "Envoi simulé réussi.", "OK");
        }
        finally { IsBusy = false; }
    }

    private async void OnReadClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(Book?.Extrait))
        {
            await DisplayAlert("Information", "Aucun extrait disponible.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("PdfViewerPage", new Dictionary<string, object> { { "Book", Book } });
    }

    private async void OnViewPdfClicked(object sender, EventArgs e) => OnReadClicked(sender, e);
}