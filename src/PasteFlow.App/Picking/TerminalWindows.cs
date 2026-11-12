namespace PasteFlow.App.Picking;

/// <summary>
/// Tells the programs that show a terminal: there, a text is pasted, never typed key by key.
/// </summary>
internal static class TerminalWindows
{
    private static readonly HashSet<string> Programs = new(StringComparer.OrdinalIgnoreCase)
    {
        "windowsterminal", "openconsole", "conhost", "cmd", "powershell", "pwsh", "wsl", "bash", "mintty",
        "wezterm-gui", "alacritty", "conemu", "conemu64", "putty", "kitty", "tabby", "hyper", "fluent terminal",
    };

    /// <summary>
    /// Determines whether a program shows a terminal.
    /// </summary>
    /// <param name="program">The name of the process of the window, such as "windowsterminal".</param>
    /// <returns><see langword="true"/> for a terminal; otherwise <see langword="false"/>.</returns>
    public static bool IsTerminal(string program)
    {
        ArgumentNullException.ThrowIfNull(program);

        return Programs.Contains(program.Trim());
    }
}
