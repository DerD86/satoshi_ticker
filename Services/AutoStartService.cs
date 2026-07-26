using Microsoft.Win32;

namespace SatoshiTicker.Services;

public static class AutoStartService
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApplicationName = "SatoshiTicker";

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("Der Windows-Autostart konnte nicht geöffnet werden.");

        if (!enabled)
        {
            key.DeleteValue(ApplicationName, throwOnMissingValue: false);
            return;
        }

        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Der Programmpfad konnte nicht ermittelt werden.");

        key.SetValue(ApplicationName, $"\"{executablePath}\"");

        // Windows remembers entries disabled in Task Manager separately. Removing
        // that stale decision makes an explicit enable action effective again.
        using RegistryKey? startupApproved = Registry.CurrentUser.OpenSubKey(
            StartupApprovedPath,
            writable: true);
        startupApproved?.DeleteValue(ApplicationName, throwOnMissingValue: false);
    }
}
