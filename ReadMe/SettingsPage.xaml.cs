using ReadMe.Helpers;

namespace ReadMe;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();

        ApiUrlEntry.Text = ApiHelper.BaseUrl;

        // On synchronise le switch avec le thème actuel au chargement
        ThemeSwitch.IsToggled = Application.Current.UserAppTheme == AppTheme.Dark;
    }

    private async void OnSaveApiUrlClicked(object sender, EventArgs e)
    {
        ApiHelper.SetBaseUrl(ApiUrlEntry.Text);
        ApiUrlEntry.Text = ApiHelper.BaseUrl;
        await DisplayAlert("OK", "Adresse API enregistrée.", "OK");
    }

    private async void OnResetApiUrlClicked(object sender, EventArgs e)
    {
        ApiHelper.SetBaseUrl(ApiHelper.DefaultBaseUrl);
        ApiUrlEntry.Text = ApiHelper.BaseUrl;
        await DisplayAlert("OK", "Adresse API réinitialisée.", "OK");
    }

    private async void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        // On change le thème
        Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;

        // Optionnel : Enregistrer le choix dans les préférences du téléphone
        Preferences.Default.Set("AppTheme", (int)Application.Current.UserAppTheme);
    }
}