using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InstrumentControl.App.Views;

/// <summary>
/// Modal error window with selectable, copyable text — user can Ctrl+A / Ctrl+C to copy details.
/// </summary>
public class ErrorWindow : Window
{
    private ErrorWindow(string title, string headline, string details)
    {
        Title = $"Błąd — {title}";
        Width = 640;
        Height = 400;
        MinWidth = 420;
        MinHeight = 250;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xF9, 0xFA));

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Headline
        var headlineTb = new TextBlock
        {
            Text = headline,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B))
        };
        Grid.SetRow(headlineTb, 0);

        // Details (read-only, selectable, copyable)
        var detailBorder = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
        };
        var detailBox = new TextBox
        {
            Text = details,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AcceptsReturn = false,
            IsUndoEnabled = false
        };
        detailBorder.Child = detailBox;
        Grid.SetRow(detailBorder, 1);

        // Buttons row
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var copyBtn = new Button
        {
            Content = "Kopiuj szczegóły",
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        copyBtn.Click += (_, _) =>
        {
            Clipboard.SetText(details);
            copyBtn.Content = "Skopiowano ✓";
        };

        var closeBtn = new Button
        {
            Content = "Zamknij",
            Padding = new Thickness(16, 6, 16, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x95, 0xA5, 0xA6)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0)
        };
        closeBtn.Click += (_, _) => Close();

        btnPanel.Children.Add(copyBtn);
        btnPanel.Children.Add(closeBtn);
        Grid.SetRow(btnPanel, 2);

        root.Children.Add(headlineTb);
        root.Children.Add(detailBorder);
        root.Children.Add(btnPanel);
        Content = root;

        // Select all text on open for easy copy
        Loaded += (_, _) => detailBox.SelectAll();
    }

    public static void ShowException(string context, Exception ex)
    {
        var details = BuildDetails(context, ex);
        ShowOnUiThread(context, ex.Message, details);
    }

    public static void Show(string title, string message, string? details = null)
    {
        ShowOnUiThread(title, message, details ?? message);
    }

    private static void ShowOnUiThread(string title, string headline, string details)
    {
        var app = Application.Current;
        if (app == null) return;

        if (app.Dispatcher.CheckAccess())
            new ErrorWindow(title, headline, details).Show();
        else
            app.Dispatcher.InvokeAsync(() => new ErrorWindow(title, headline, details).Show());
    }

    private static string BuildDetails(string context, Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Kontekst: {context}");
        sb.AppendLine($"Czas: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine();
        AppendException(sb, ex, 0);
        return sb.ToString();
    }

    private static void AppendException(System.Text.StringBuilder sb, Exception ex, int depth)
    {
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}Typ: {ex.GetType().FullName}");
        sb.AppendLine($"{indent}Komunikat: {ex.Message}");
        if (!string.IsNullOrWhiteSpace(ex.StackTrace))
        {
            sb.AppendLine($"{indent}Stack trace:");
            foreach (var line in ex.StackTrace.Split('\n'))
                sb.AppendLine($"{indent}  {line.TrimEnd()}");
        }
        if (ex.InnerException != null)
        {
            sb.AppendLine($"{indent}--- Inner exception ---");
            AppendException(sb, ex.InnerException, depth + 1);
        }
    }
}
