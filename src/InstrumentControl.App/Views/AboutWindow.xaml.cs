using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using InstrumentControl.App.Services;

namespace InstrumentControl.App.Views;

public partial class AboutWindow : Window
{
    private bool _suppressSelectionChanged;

    public AboutWindow()
    {
        InitializeComponent();

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = ver != null
            ? $"{LocalizationService.Get("About_Version")} {ver.Major}.{ver.Minor}.{ver.Build}"
            : $"{LocalizationService.Get("About_Version")} dev";

        var languages = new[]
        {
            new LanguageItem("English", LocalizationService.EnglishCode),
            new LanguageItem("Polski",  LocalizationService.PolishCode)
        };
        LanguageComboBox.ItemsSource = languages;
        LanguageComboBox.DisplayMemberPath = nameof(LanguageItem.DisplayName);

        _suppressSelectionChanged = true;
        LanguageComboBox.SelectedItem = languages.FirstOrDefault(l => l.Code == LocalizationService.CurrentLanguage);
        _suppressSelectionChanged = false;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;
        if (LanguageComboBox.SelectedItem is LanguageItem item)
        {
            LocalizationService.SetLanguage(item.Code);
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = ver != null
                ? $"{LocalizationService.Get("About_Version")} {ver.Major}.{ver.Minor}.{ver.Build}"
                : $"{LocalizationService.Get("About_Version")} dev";
        }
    }

    private void GithubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private record LanguageItem(string DisplayName, string Code);
}
