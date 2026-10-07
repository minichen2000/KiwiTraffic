using System.Windows;
using KiwiTraffic.Core.Settings;

namespace KiwiTraffic.App.Services;

/// <summary>
/// Swaps the colour palette at runtime.
/// </summary>
/// <remarks>
/// The theme lives in its own merged dictionary so that switching means
/// replacing one dictionary, not walking the visual tree. Everything in the
/// windows refers to colours through <c>DynamicResource</c>, so they repaint
/// without being recreated.
/// </remarks>
public static class ThemeManager
{
    private const string LightSource = "Themes/Light.xaml";
    private const string DarkSource = "Themes/Dark.xaml";

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static Uri SourceFor(AppTheme theme)
        => new(theme == AppTheme.Dark ? DarkSource : LightSource, UriKind.Relative);

    public static void Apply(AppTheme theme)
    {
        var dictionaries = Application.Current?.Resources.MergedDictionaries;

        if (dictionaries is null)
        {
            Current = theme;
            return;
        }

        var replacement = new ResourceDictionary { Source = SourceFor(theme) };
        var existing = dictionaries.FirstOrDefault(IsThemeDictionary);

        if (existing is null)
        {
            // Before the control styles, so those can still override.
            dictionaries.Insert(0, replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(existing)] = replacement;
        }

        Current = theme;
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;

        return source is not null
            && (source.EndsWith(LightSource, StringComparison.OrdinalIgnoreCase)
                || source.EndsWith(DarkSource, StringComparison.OrdinalIgnoreCase));
    }
}
