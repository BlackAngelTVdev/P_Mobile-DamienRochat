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

    public Book? Book
    {
        get => _book;
        set
        {
            _book = value;
            OnPropertyChanged();

            if (value?.Id > 0)
            {
                _ = LoadBookDetailsAsync(value.Id);
            }
        }
    }

    // Utilisation de 'new' pour éviter le conflit avec la propriété native
    public new bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public BookDetailPage()
    {
        InitializeComponent();
        BindingContext = this;

        _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
            ?? new ApiHelper(new HttpClient { BaseAddress = new Uri(ApiHelper.BaseUrl) });
    }

    private async void OnReadClicked(object sender, EventArgs e)
    {
        if (Book?.Id is null or <= 0)
        {
            await DisplayAlert("Information", "Livre introuvable.", "OK");
            return;
        }

        await Shell.Current.GoToAsync(nameof(EpubViewerPage), new Dictionary<string, object>
        {
            { "Book", Book }
        });
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (Book?.Id is null or <= 0)
        {
            await DisplayAlert("Information", "Livre introuvable.", "OK");
            return;
        }

        var confirm = await DisplayAlert("Suppression", "Supprimer ce livre ?", "Oui", "Non");
        if (!confirm)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var deleted = await _apiHelper.DeleteAsync($"book/{Book.Id}/delete");
            if (!deleted)
            {
                await DisplayAlert("Erreur", "Suppression impossible.", "OK");
                return;
            }

            await DisplayAlert("OK", "Livre supprimé.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBookDetailsAsync(int bookId)
    {
        try
        {
            IsBusy = true;
            var details = await _apiHelper.GetAsync<Book>($"book/{bookId}");
            if (details is not null)
            {
                _book = details;
                OnPropertyChanged(nameof(Book));
            }
        }
        catch
        {
            // Keep partial data from list when detail endpoint is unavailable.
        }
        finally
        {
            IsBusy = false;
        }
    }
}