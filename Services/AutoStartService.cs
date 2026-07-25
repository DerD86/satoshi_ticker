using Microsoft.Win32;

namespace SatoshiTicker.Services;

public static class AutoStartService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationName = "SatoshiTicker";

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Der Windows-Autostart konnte nicht geöffnet werden.");

        if (!enabled)
        {
            key.DeleteValue(ApplicationName, throwOnMissingValue: false);
            return;
        }

        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der Programmpfad konnte nicht ermittelt werden.");

        key.SetValue(ApplicationName, $"\"{executablePath}\"");
    }
}
