using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using ReadMe.Helpers;

namespace ReadMe
{
    public partial class AddBookPage : ContentPage
    {
        private readonly IApiHelper _apiHelper;
        private FileResult? _selectedFile;

        public AddBookPage()
        {
            InitializeComponent();

            _apiHelper = Application.Current?.Handler?.MauiContext?.Services.GetService<IApiHelper>()
                ?? new ApiHelper(new HttpClient());
        }

        private async void OnPickFileClicked(object sender, EventArgs e)
        {
            try
            {
                _selectedFile = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Sélectionne un fichier EPUB",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, ["org.idpf.epub-container"] },
                        { DevicePlatform.Android, ["application/epub+zip"] },
                        { DevicePlatform.WinUI, [".epub"] },
                        { DevicePlatform.MacCatalyst, [".epub"] }
                    })
                });

                SelectedFileLabel.Text = _selectedFile is null
                    ? "Aucun fichier sélectionné"
                    : _selectedFile.FileName;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", ex.Message, "OK");
            }
        }

        private async void OnUploadClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleEntry.Text) || string.IsNullOrWhiteSpace(AuthorEntry.Text))
            {
                await DisplayAlert("Information", "Titre et auteur sont obligatoires.", "OK");
                return;
            }

            if (_selectedFile is null)
            {
                await DisplayAlert("Information", "Choisis un fichier EPUB.", "OK");
                return;
            }

            try
            {
                SetBusy(true);

                await using var fileStream = await _selectedFile.OpenReadAsync();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/epub+zip");

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(TitleEntry.Text.Trim()), "title");
                form.Add(new StringContent(AuthorEntry.Text.Trim()), "author");
                form.Add(new StringContent(IsbnEntry.Text?.Trim() ?? string.Empty), "isbn");
                form.Add(new StringContent(LanguageEntry.Text?.Trim() ?? string.Empty), "language");
                form.Add(new StringContent(CoverImagePathEntry.Text?.Trim() ?? string.Empty), "cover_image_path");
                form.Add(new StringContent(PublishDatePicker.Date.ToString("O")), "publish_date");
                form.Add(new StringContent(DescriptionEditor.Text?.Trim() ?? string.Empty), "description");
                form.Add(new StringContent(ExtraitEditor.Text?.Trim() ?? string.Empty), "excerpt");
                form.Add(streamContent, "file", _selectedFile.FileName);

                await _apiHelper.PostMultipartAsync<object>("books/upload", form);

                await DisplayAlert("Succès", "Livre uploadé.", "OK");
                TitleEntry.Text = string.Empty;
                AuthorEntry.Text = string.Empty;
                IsbnEntry.Text = string.Empty;
                LanguageEntry.Text = string.Empty;
                CoverImagePathEntry.Text = string.Empty;
                PublishDatePicker.Date = DateTime.Today;
                DescriptionEditor.Text = string.Empty;
                ExtraitEditor.Text = string.Empty;
                _selectedFile = null;
                SelectedFileLabel.Text = "Aucun fichier sélectionné";
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur upload", ex.Message, "OK");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool value)
        {
            BusyIndicator.IsVisible = value;
            BusyIndicator.IsRunning = value;
            TitleEntry.IsEnabled = !value;
            AuthorEntry.IsEnabled = !value;
            IsbnEntry.IsEnabled = !value;
            LanguageEntry.IsEnabled = !value;
            CoverImagePathEntry.IsEnabled = !value;
            PublishDatePicker.IsEnabled = !value;
            DescriptionEditor.IsEnabled = !value;
            ExtraitEditor.IsEnabled = !value;
        }
    }
}
