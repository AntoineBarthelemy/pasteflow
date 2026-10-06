namespace PasteFlow.Core.Undo;

/// <summary>
/// Decides whether a field filled by the application can be emptied again.
/// </summary>
public static class UndoRules
{
    /// <summary>
    /// Determines whether a field still holds the value the application wrote, meaning the user did not change it.
    /// </summary>
    /// <remarks>
    /// Spaces and case are ignored, because some fields reformat what they receive
    /// (a phone number "06 47 21 85 39" displayed as "0647218539").
    /// </remarks>
    /// <param name="writtenValue">The value the application wrote.</param>
    /// <param name="currentValue">The value the field holds now.</param>
    /// <returns><see langword="true"/> if the field still holds the written value; otherwise <see langword="false"/>.</returns>
    public static bool IsStillWritten(string writtenValue, string currentValue)
    {
        ArgumentNullException.ThrowIfNull(writtenValue);
        ArgumentNullException.ThrowIfNull(currentValue);

        return string.Equals(WithoutSpaces(writtenValue), WithoutSpaces(currentValue), StringComparison.OrdinalIgnoreCase);
    }

    private static string WithoutSpaces(string value)
    {
        return string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
    }
}
