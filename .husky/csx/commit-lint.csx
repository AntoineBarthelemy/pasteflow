// The engine of the scripts does not always reference LINQ by itself (SDK installed for one user)
#r "System.Linq"
using System.Linq;

/// <summary>
/// Checks that the first line of the commit message follows the Conventional Commits specification.
/// https://www.conventionalcommits.org/fr/v1.0.0/
/// Allowed types: build, ci, chore, docs, feat, fix, perf, refactor, revert, style, test.
/// The scope is free, the subject may start with a lower case letter.
/// </summary>

using System.Text.RegularExpressions;

private const string Pattern = @"^(?=.{1,100}$)(build|ci|chore|docs|feat|fix|perf|refactor|revert|style|test)(\([^()\s]+\))?!?: \S.{2,}(?<![.\s])$";

private var firstLine = File.ReadLines(Args[0]).FirstOrDefault() ?? string.Empty;

if (Regex.IsMatch(firstLine, Pattern))
{
    return 0;
}

Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"Invalid commit message: '{firstLine}'");
Console.ResetColor();
Console.WriteLine("Expected: 'type(scope): subject' or 'type: subject', in English, without final period.");
Console.WriteLine("Types: build, ci, chore, docs, feat, fix, perf, refactor, revert, style, test");
Console.WriteLine("More info: https://www.conventionalcommits.org/fr/v1.0.0/");

return 1;
