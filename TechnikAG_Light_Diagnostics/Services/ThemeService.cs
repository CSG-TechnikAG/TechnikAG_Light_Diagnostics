using System.Windows;
using Microsoft.Win32;

namespace TechnikAG_Light_Diagnostics.Services;

public enum AppTheme
{
    Light,
    Dark
}

public static class ThemeService
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ResourceDictionary? _themeDictionary;

    public static AppTheme Current { get; private set; } = AppTheme.Light;

    public static bool IsDark => Current == AppTheme.Dark;

    public static void Initialize() => Apply(DetectSystemTheme());

    public static void Toggle() => Apply(IsDark ? AppTheme.Light : AppTheme.Dark);

    public static void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/TechnikAG_Light_Diagnostics;component/Themes/{theme}Theme.xaml",
                UriKind.Absolute)
        };

        var dictionaries = application.Resources.MergedDictionaries;
        if (_themeDictionary is not null)
        {
            dictionaries.Remove(_themeDictionary);
        }

        dictionaries.Add(dictionary);
        _themeDictionary = dictionary;
        Current = theme;
    }

    private static AppTheme DetectSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("AppsUseLightTheme") is int useLightTheme)
            {
                return useLightTheme == 0 ? AppTheme.Dark : AppTheme.Light;
            }
        }
        catch
        {
            // Fall back to the light theme when the preference cannot be read.
        }

        return AppTheme.Light;
    }
}
