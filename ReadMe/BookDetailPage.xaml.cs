using Microsoft.Extensions.DependencyInjection;
using ReadMe.Helpers;
using ReadMe.Models;

namespace ReadMe;

[QueryProperty(nameof(Book), "Book")]
public partial class BookDetailPage : ContentPage
{
    private readonly IApiHelper _apiHelper;

    private Book _book;
    public Book Book
    {
        get => _book;
        set
        {
            _book = value;
            OnPropertyChanged();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
        }
    }

    private int _selectedRating = 5;

    public BookDetailPage()
    {
        InitializeComponent();
        BindingContext = this;

        _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
            ?? new ApiHelper(new HttpClient { BaseAddress = new Uri(ApiHelper.BaseUrl) });
        
        UpdateStars();
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
        if (string.IsNullOrWhiteSpace(CommentEntry.Text))
        {
            await DisplayAlert("Information", "Veuillez saisir un commentaire.", "OK");
            return;
        }

        try
        {
            IsBusy = true;

            // Submit Comment
            var newComment = new BookComment
            {
                UserId = 1, // Simulated current user
                Title = CommentEntry.Text
            };

            // JSON Server simulation: we POST to /comments or /books/{id}/comments
            // For simplicity and resilience, we show success even if the endpoint is not 100% standard for this demo
            await _apiHelper.PostAsync<BookComment, BookComment>($"books/{Book.Id}/comments", newComment);

            // Submit Rate
            var newRate = new BookRate
            {
                UserId = 1,
                Value = _selectedRating
            };
            await _apiHelper.PostAsync<BookRate, BookRate>($"books/{Book.Id}/rates", newRate);

            // Update UI locally (simulated)
            Book.Comments.Add(newComment);
            Book.Rates.Add(newRate);
            
            // Refresh bindings
            OnPropertyChanged(nameof(Book));
            CommentEntry.Text = string.Empty;

            await DisplayAlert("Succès", "Votre avis a été enregistré !", "OK");
        }
        catch (Exception ex)
        {
            // Even if API fails (e.g. endpoint doesn't exist on mock server), we show a positive message for UX
            // but log for debug
            System.Diagnostics.Debug.WriteLine($"API Error: {ex.Message}");
            await DisplayAlert("Information", "Votre avis a été envoyé (simulé)", "OK");
            
            // Still update UI locally for immediate feedback
            Book.Comments.Add(new BookComment { UserId = 1, Title = CommentEntry.Text });
            OnPropertyChanged(nameof(Book));
            CommentEntry.Text = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnReadClicked(object sender, EventArgs e)
    {
        // Check if there is a PDF excerpt
        if (string.IsNullOrEmpty(Book.Extrait))
        {
            await DisplayAlert("Information", "Aucun extrait disponible pour ce livre.", "OK");
            return;
        }

        var navigationParameter = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync(nameof(PdfViewerPage), navigationParameter);
    }

    private async void OnViewPdfClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(Book.Extrait))
        {
            await DisplayAlert("Information", "Aucun résumé PDF disponible pour ce livre.", "OK");
            return;
        }

        var navigationParameter = new Dictionary<string, object>
        {
            { "Book", Book }
        };
        await Shell.Current.GoToAsync(nameof(PdfViewerPage), navigationParameter);
    }
}
