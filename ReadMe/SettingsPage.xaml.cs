namespace ReadMe;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();

        // On synchronise le switch avec le thème actuel au chargement
        ThemeSwitch.IsToggled = Application.Current.UserAppTheme == AppTheme.Dark;
    }

    private async void OnThemeToggled(object sender, ToggledEventArgs e)
    {
        // On change le thème
        Application.Current.UserAppTheme = e.Value ? AppTheme.Dark : AppTheme.Light;

        // Optionnel : Enregistrer le choix dans les préférences du téléphone
        Preferences.Default.Set("AppTheme", (int)Application.Current.UserAppTheme);
    }
}