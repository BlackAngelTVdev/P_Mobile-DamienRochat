using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using ReadMe.Helpers;
using ReadMe.Models;

namespace ReadMe
{
    public partial class MainPage : ContentPage
    {
        private readonly IApiHelper _apiHelper;

        public ObservableCollection<Book> Books { get; } = [];

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set
            {
                if (_isRefreshing == value)
                {
                    return;
                }

                _isRefreshing = value;
                OnPropertyChanged();
            }
        }

        private bool _isRefreshing;
        private bool _isLoading;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;

            _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
                ?? new ApiHelper(new HttpClient
                {
                    BaseAddress = new Uri(ApiHelper.BaseUrl)
                });

            Loaded += OnPageLoaded;
        }

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            Loaded -= OnPageLoaded;
            await LoadBooksAsync();
        }

        private async void OnRefreshing(object? sender, EventArgs e)
        {
            await LoadBooksAsync();
        }

        private async Task LoadBooksAsync()
        {
            if (_isLoading)
            {
                return;
            }

            try
            {
                _isLoading = true;
                IsRefreshing = true;
                var books = await _apiHelper.GetAsync<List<Book>>("books");

                Books.Clear();
                if (books is null)
                {
                    return;
                }

                foreach (var book in books)
                {
                    Books.Add(book);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur API", $"Impossible de charger les livres.\n{ex.Message}", "OK");
            }
            finally
            {
                IsRefreshing = false;
                _isLoading = false;
            }
        }
    }
}
