using System.IO;
using System.Security;
using Microsoft.Win32;

namespace PasteFlow.App.Settings;

/// <summary>
/// Starts the application with Windows, for the current user only, through the "Run" key of the registry.
/// </summary>
internal sealed class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _valueName;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupRegistration"/> class.
    /// </summary>
    /// <param name="valueName">The name of the entry in the registry.</param>
    public StartupRegistration(string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);

        _valueName = valueName;
    }

    /// <summary>
    /// Gets a value indicating whether the application starts with Windows.
    /// </summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(_valueName) is string;
        }
    }

    /// <summary>
    /// Starts or stops starting the application with Windows.
    /// </summary>
    /// <param name="isEnabled">Whether the application starts with Windows.</param>
    /// <param name="executablePath">The program to start.</param>
    /// <returns><see langword="true"/> if the change was made; otherwise <see langword="false"/>.</returns>
    public bool TrySet(bool isEnabled, string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (isEnabled)
            {
                // Quoted, so that a path with spaces is read as one program
                key.SetValue(_valueName, $"\"{executablePath}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(_valueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }
}
