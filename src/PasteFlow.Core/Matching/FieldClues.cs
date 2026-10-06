using System.Text;
using System.Text.RegularExpressions;
using PasteFlow.Core.Candidates;
using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Matching;

/// <summary>
/// What a field tells about the value it expects, gathered from its label, its technical name and its help text.
/// </summary>
/// <remarks>
/// The label shown to the user is the main clue. The technical name (such as "billing_email") and the help text
/// (such as "JJ/MM/AAAA" or "ex : jean.dupont@exemple.fr") take over when the label says nothing about the kind
/// of value expected.
/// </remarks>
public sealed partial class FieldClues
{
    private const int RegexTimeoutMilliseconds = 100;

    private readonly IReadOnlySet<string>[] _conceptSets;

    private FieldClues(IReadOnlySet<string>[] conceptSets, IReadOnlySet<CandidateKind> expectedKinds, bool isConfirmation)
    {
        _conceptSets = conceptSets;
        ExpectedKinds = expectedKinds;
        IsConfirmation = isConfirmation;
    }

    /// <summary>
    /// Gets the kinds of values the field expects; empty when no clue tells.
    /// </summary>
    public IReadOnlySet<CandidateKind> ExpectedKinds { get; }

    /// <summary>
    /// Gets a value indicating whether the field asks to type again the value of another field,
    /// such as "Confirmez votre email".
    /// </summary>
    public bool IsConfirmation { get; }

    /// <summary>
    /// Gathers the clues of a field.
    /// </summary>
    /// <param name="field">The field to understand.</param>
    /// <returns>The clues of the field.</returns>
    public static FieldClues Of(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var labelConcepts = LabelConcepts.From(field.Label);
        var technicalName = SplitTechnicalName(field.TechnicalName);
        var technicalConcepts = LabelConcepts.From(technicalName);
        var hintConcepts = LabelConcepts.From(field.Hint);

        // The first clue that tells the expected kind wins: label, then technical name, then help text
        var expectedKinds = LabelConcepts.ExpectedKinds(labelConcepts);
        if (expectedKinds.Count == 0)
        {
            expectedKinds = LabelConcepts.ExpectedKinds(technicalConcepts);
        }

        if (expectedKinds.Count == 0)
        {
            expectedKinds = KindsShownInHint(field.Hint);
        }

        if (expectedKinds.Count == 0)
        {
            expectedKinds = LabelConcepts.ExpectedKinds(hintConcepts);
        }

        var conceptSets = new[] { labelConcepts, technicalConcepts, hintConcepts }
            .Where(concepts => concepts.Count > 0)
            .ToArray();

        var isConfirmation = LabelConcepts.IsConfirmation(field.Label) || LabelConcepts.IsConfirmation(technicalName);

        return new FieldClues(conceptSets, expectedKinds, isConfirmation);
    }

    /// <summary>
    /// Determines whether one of the clues of the field talks about a concept.
    /// </summary>
    /// <param name="concept">The concept, one of the constants of <see cref="LabelConcepts"/>.</param>
    /// <returns><see langword="true"/> if a clue talks about the concept; otherwise <see langword="false"/>.</returns>
    public bool Mentions(string concept)
    {
        ArgumentNullException.ThrowIfNull(concept);

        return _conceptSets.Any(concepts => concepts.Contains(concept));
    }

    /// <summary>
    /// Measures how much a label talks about the same thing as the field, through its best matching clue.
    /// </summary>
    /// <param name="labelConcepts">The concepts of the label to compare.</param>
    /// <returns>A value from 0 (nothing in common) to 1 (same concepts).</returns>
    public double Similarity(IReadOnlySet<string> labelConcepts)
    {
        ArgumentNullException.ThrowIfNull(labelConcepts);

        return _conceptSets.Length == 0
            ? 0
            : _conceptSets.Max(concepts => LabelConcepts.Similarity(concepts, labelConcepts));
    }

    /// <summary>
    /// Cuts a technical name into words: "billingEmail", "billing_email" and "billing-email" give "billing email".
    /// </summary>
    /// <param name="technicalName">The technical name of the field.</param>
    /// <returns>The words of the name separated by spaces.</returns>
    private static string SplitTechnicalName(string technicalName)
    {
        var builder = new StringBuilder(technicalName.Length * 2);
        for (var index = 0; index < technicalName.Length; index++)
        {
            var character = technicalName[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(technicalName[index - 1]))
            {
                _ = builder.Append(' ');
            }

            _ = builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Finds the kinds of values shown in a help text, such as an email in "ex : jean.dupont@exemple.fr"
    /// or a date in "JJ/MM/AAAA".
    /// </summary>
    /// <param name="hint">The help text of the field.</param>
    /// <returns>The kinds shown in the help text.</returns>
    private static HashSet<CandidateKind> KindsShownInHint(string hint)
    {
        var kinds = TextSegmenter.Segment(hint)
            .Select(piece => piece.Kind)
            .Where(kind => kind != CandidateKind.Text)
            .ToHashSet();

        if (DatePatternRegex().IsMatch(hint))
        {
            _ = kinds.Add(CandidateKind.Date);
        }

        return kinds;
    }

    [GeneratedRegex(@"\b(?:(?:jj|dd)[/.\-]mm[/.\-](?:aaaa|yyyy|aa|yy)|(?:aaaa|yyyy)-mm-(?:jj|dd))\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex DatePatternRegex();
}
