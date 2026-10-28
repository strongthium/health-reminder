using Microsoft.Win32;

namespace Strongthium.HealthReminder;

/// <summary>
/// Starts health-reminder, in the tray, when the user signs in to Windows: a value in the Run key of
/// HKEY_CURRENT_USER, which needs no administrator.
/// </summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Strongthium health-reminder";

    private static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("The program's path is unknown.");

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) key.SetValue(ValueName, $"\"{Executable}\" --minimized" + (Program.OwnDataDirectory ? $" --data \"{Program.DataDirectory}\"" : ""));
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Points the entry at this copy of the program, in case it was moved or updated in another folder. Not
    /// done by a copy started with its own data folder, which would take the entry over from the usual one.</summary>
    public static void Refresh()
    {
        if (Program.OwnDataDirectory) return;
        try
        {
            if (Enabled) Enabled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            System.Diagnostics.Trace.WriteLine("health-reminder: autostart: " + exception.Message);
        }
    }
}
