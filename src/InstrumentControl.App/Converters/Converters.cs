using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace InstrumentControl.App.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public static InverseBoolConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b && !b;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public static InverseBoolToVisibilityConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is Visibility vis && vis == Visibility.Collapsed;
}

[ValueConversion(typeof(bool), typeof(string))]
public class PauseResumeConverter : IValueConverter
{
    public static PauseResumeConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is bool b && b
            ? Application.Current?.Resources["Converter_Resume"] as string ?? "▶ Resume"
            : Application.Current?.Resources["Converter_Pause"] as string ?? "⏸ Pause";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(bool), typeof(string))]
public class SimModeConverter : IValueConverter
{
    public static SimModeConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is bool b && b
            ? Application.Current?.Resources["StatusBar_SimMode"] as string ?? "⚠ SIM"
            : Application.Current?.Resources["StatusBar_VisaActive"] as string ?? "● VISA";
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(int), typeof(Visibility))]
public class LengthToVisibilityConverter : IValueConverter
{
    public static LengthToVisibilityConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is int len && len > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

[ValueConversion(typeof(System.Windows.Media.Color), typeof(System.Windows.Media.SolidColorBrush))]
public class ColorToBrushConverter : IValueConverter
{
    public static ColorToBrushConverter Instance { get; } = new();
    public object Convert(object v, Type t, object p, CultureInfo c) =>
        v is System.Windows.Media.Color col ? new System.Windows.Media.SolidColorBrush(col) : Binding.DoNothing;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
