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
            private bool _isLoadingMore;
            private int _currentPage = 1;
            private const int PageSize = 10;
            private bool _hasMore = true;

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;

            _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
                ?? new ApiHelper(new HttpClient
                {
                    BaseAddress = new Uri(ApiHelper.BaseUrl)
                });
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = LoadBooksAsync();
        }

        private async void OnRefreshing(object? sender, EventArgs e)
        {
            await LoadBooksAsync();
        }

        private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not Book selectedBook)
            {
                return;
            }

            var navigationParameter = new Dictionary<string, object>
            {
                { "Book", selectedBook }
            };

            await Shell.Current.GoToAsync(nameof(BookDetailPage), navigationParameter);

            // Clear selection
            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }
        }

        private async void OnItemTapped(object sender, EventArgs e)
        {
            if (sender is VisualElement ve && ve.BindingContext is Book selectedBook)
            {
                var navigationParameter = new Dictionary<string, object>
                {
                    { "Book", selectedBook }
                };

                await Shell.Current.GoToAsync(nameof(BookDetailPage), navigationParameter);
            }
        }

        private async Task LoadBooksAsync(bool reset = true)
        {
            if (_isLoading)
            {
                return;
            }

            try
            {
                if (reset)
                {
                    _currentPage = 1;
                    _hasMore = true;
                }

                if (!_hasMore)
                {
                    return;
                }

                if (reset)
                {
                    _isLoading = true;
                    IsRefreshing = true;
                    Books.Clear();
                }
                else
                {
                    _isLoadingMore = true;
                }

                var endpoint = $"books?page={_currentPage}&pageSize={PageSize}";
                var pageResult = await _apiHelper.GetAsync<PageResult<Book>>(endpoint);

                var items = pageResult?.items ?? new List<Book>();

                foreach (var book in items)
                {
                    Books.Add(book);
                }

                // If fewer items returned than page size, we've reached the end
                if (items.Count < PageSize)
                {
                    _hasMore = false;
                }
                else
                {
                    _currentPage += 1;
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
                _isLoadingMore = false;
            }
        }

        private async void OnRemainingItemsThresholdReached(object sender, EventArgs e)
        {
            if (_isLoadingMore || !_hasMore)
            {
                return;
            }

            await LoadBooksAsync(reset: false);
        }

        // Helper type to match API paged response
        private class PageResult<T>
        {
            public List<T> items { get; set; } = new();
            public int total { get; set; }
        }
    }
}
