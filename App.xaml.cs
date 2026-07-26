using System.Windows;

namespace SatoshiTicker;

public partial class App : Application
{
    public void ApplyTheme(string theme)
    {
        string themeName = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? "Light"
            : "Dark";

        Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative)
        };
    }
}
