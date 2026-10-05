/// <summary>
/// Rejects staged C# files that still contain debugging leftovers.
/// The arguments are the staged files selected by the task runner.
/// </summary>

private static readonly string[] ForbiddenPatterns =
{
    "Console.WriteLine(",
    "Console.Write(",
    "Debug.WriteLine(",
    "Debugger.Break(",
    "Debugger.Launch(",
};

private var violations = Args
    .Where(File.Exists)
    .SelectMany(file => File.ReadLines(file)
        .Select((line, index) => (File: file, Line: index + 1, Text: line)))
    .Where(entry => ForbiddenPatterns.Any(pattern => entry.Text.Contains(pattern, StringComparison.Ordinal)))
    .ToList();

if (violations.Count == 0)
{
    return 0;
}

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine("Debugging leftovers found, remove them before committing:");
Console.ResetColor();
foreach (var violation in violations)
{
    Console.WriteLine($"  {violation.File}:{violation.Line}: {violation.Text.Trim()}");
}

return 1;
