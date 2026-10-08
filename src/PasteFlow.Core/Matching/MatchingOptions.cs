namespace PasteFlow.Core.Matching;

/// <summary>
/// The thresholds that decide when a value is sure enough to fill a field without asking.
/// </summary>
public sealed record MatchingOptions
{
    /// <summary>
    /// Gets the default thresholds, cautious without being excessive.
    /// </summary>
    public static MatchingOptions Balanced { get; } = new() { ConfidenceThreshold = 0.6, MinimumLead = 0.15 };

    /// <summary>
    /// Gets the minimum score of the best value to fill a field.
    /// </summary>
    public required double ConfidenceThreshold { get; init; }

    /// <summary>
    /// Gets the minimum lead of the best value over the second best one to fill a field.
    /// </summary>
    public required double MinimumLead { get; init; }

    /// <summary>
    /// Gets the thresholds of a level of prudence.
    /// </summary>
    /// <param name="prudence">The level chosen by the user.</param>
    /// <returns>The thresholds: higher when cautious, lower when bold.</returns>
    public static MatchingOptions For(Prudence prudence)
    {
        return prudence switch
        {
            Prudence.Cautious => new MatchingOptions { ConfidenceThreshold = 0.75, MinimumLead = 0.25 },
            Prudence.Bold => new MatchingOptions { ConfidenceThreshold = 0.5, MinimumLead = 0.1 },
            _ => Balanced,
        };
    }
}
