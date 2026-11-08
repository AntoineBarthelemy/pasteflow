using System.Text.RegularExpressions;
using PasteFlow.Core.Matching;
using PasteFlow.Core.Profile;
using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Candidates;

/// <summary>
/// Cuts a text into units of meaning that could be pasted: recognized values (emails, phone numbers, dates,
/// amounts, references...) with the label that introduces them, "label: value" pairs and plain lines.
/// </summary>
/// <remarks>
/// The rules run line by line. A line made only of a label ending with ":" labels the value of the next line.
/// People, communes, countries and companies are recognized through the official lists (see <see cref="NameRecognizer"/>).
/// </remarks>
public static partial class TextSegmenter
{
    /// <summary>
    /// The maximum length of a plain line kept as a piece; longer lines are paragraphs, not values to paste.
    /// </summary>
    public const int MaxPieceLength = 200;

    /// <summary>
    /// The maximum length of a label.
    /// </summary>
    public const int MaxLabelLength = 40;

    private const int RegexTimeoutMilliseconds = 1000;
    private const int MinReferenceLength = 4;

    private const int SocialSecurityLength = 15;
    private const int SocialSecurityBodyLength = 13;
    private const string SocialSecurityKeyLabel = "Clé";

    private static readonly HashSet<CandidateKind> PersonKinds = new() { CandidateKind.FirstName, CandidateKind.LastName, CandidateKind.FullName };

    private static readonly char[] LineBreaks = { '\r', '\n' };
    private static readonly char[] LabelTrimmedChars = { ' ', '\t', ':', '-', '–', '—', '|', '(', '[', ',', ';', '<', '"' };
    private static readonly char[] UrlTrimmedEndChars = { '.', ',', ';', ':', '!', '?', ')', ']', '\'', '"' };

    /// <summary>
    /// Recognizers in priority order: a span taken by one recognizer is not seen by the next ones.
    /// </summary>
    private static readonly (Func<Regex> Pattern, CandidateKind Kind)[] Recognizers =
    {
        (EmailRegex, CandidateKind.Email),
        (UrlRegex, CandidateKind.Url),
        (IbanRegex, CandidateKind.Iban),
        (SocialSecurityRegex, CandidateKind.Number),
        (CarPlateRegex, CandidateKind.Reference),
        (CompanyNumberRegex, CandidateKind.Number),
        (PhoneRegex, CandidateKind.Phone),
        (DateRegex, CandidateKind.Date),
        (AmountRegex, CandidateKind.Amount),
        (NumberedReferenceRegex, CandidateKind.Reference),
        (MeasureRegex, CandidateKind.Measure),
        (ReferenceRegex, CandidateKind.Reference),
        (NumberRegex, CandidateKind.Number),
    };

    /// <summary>
    /// Cuts a text into units of meaning, in their order of appearance, without duplicates.
    /// </summary>
    /// <param name="text">The text to cut.</param>
    /// <returns>The pieces of the text.</returns>
    public static IReadOnlyList<TextPiece> Segment(string text)
    {
        return Segment(text, ReferenceData.Shared);
    }

    /// <summary>
    /// Cuts a text into units of meaning, in their order of appearance, without duplicates, with given official lists.
    /// </summary>
    /// <param name="text">The text to cut.</param>
    /// <param name="data">The official lists used to recognize people, communes, countries and companies.</param>
    /// <returns>The pieces of the text.</returns>
    public static IReadOnlyList<TextPiece> Segment(string text, ReferenceData data)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(data);

        var pieces = new List<TextPiece>();
        var pendingLabel = string.Empty;
        foreach (var line in text.Split(LineBreaks, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (IsLabelOnly(line))
            {
                pendingLabel = CleanLabel(line);
                continue;
            }

            pieces.AddRange(SegmentLine(line, pendingLabel, data));
            pendingLabel = string.Empty;
        }

        return RemoveDuplicates(pieces);
    }

    /// <summary>
    /// Recognizes people, communes, countries and companies, keeping only what the label allows: after
    /// "Prénom :", "Maurice" is a first name, not the country.
    /// </summary>
    /// <param name="span">The text to recognize.</param>
    /// <param name="label">The label of the text.</param>
    /// <param name="data">The official lists.</param>
    /// <returns>The pieces recognized that fit the label.</returns>
    private static IEnumerable<TextPiece> RecognizeFitting(string span, string label, ReferenceData data)
    {
        return NameRecognizer.RecognizeSpan(span, label, data).Where(piece => FitsLabel(piece.Kind, label));
    }

    private static bool FitsLabel(CandidateKind kind, string label)
    {
        var expected = LabelConcepts.ExpectedKinds(LabelConcepts.From(label));
        return expected.Count == 0
            || expected.Contains(kind)
            || (PersonKinds.Contains(kind) && expected.Overlaps(PersonKinds));
    }

    private static bool IsLabelOnly(string line)
    {
        return line.Length <= MaxLabelLength + 1 && line.EndsWith(':') && char.IsLetter(line[0]);
    }

    private static List<TextPiece> SegmentLine(string line, string contextLabel, ReferenceData data)
    {
        var pieces = new List<TextPiece>();
        try
        {
            // « Locataire : Mme Sophie Garnier, sophie@…, 06 … »: the role before the colon labels every value of the
            // line, and the person right after it is recognized as on a line holding nothing else
            if (RoleOf(line) is { } role)
            {
                pieces.AddRange(SegmentLine(role.Values, role.Label, data).Select(piece => piece with { Label = WithRole(role.Label, piece.Label) }));

                // The person or the company named first, before its address: « M. Julien Masson, 22 avenue de Laon… »
                pieces.AddRange(RecognizeFitting(role.Values.Split(',')[0], role.Label, data));

                // Its postal address, from the street to the postal code and the commune: « 22 avenue de Laon, 51100 Reims »
                if (AddressIn(role.Values) is { } address)
                {
                    pieces.Add(Piece(address, CandidateKind.Text, $"{role.Label} adresse"));
                }

                if (line.Length <= MaxPieceLength)
                {
                    pieces.Add(Piece(line, CandidateKind.Text, contextLabel));
                }

                return pieces;
            }

            var values = FindValues(line);
            if (values.Count > 0)
            {
                var previousEnd = 0;
                foreach (var (start, value, kind) in values)
                {
                    // A person or a company before a value, as in "Antoine Barthélémy <antoine@example.com>"
                    var before = line[previousEnd..start];
                    pieces.AddRange(RecognizeFitting(before, contextLabel, data));

                    var label = CleanLabel(before);
                    var pieceValue = kind switch
                    {
                        CandidateKind.Number => value.Replace(" ", string.Empty, StringComparison.Ordinal),
                        CandidateKind.Reference when CarPlateRegex().IsMatch(value) => CarPlate(value),
                        _ => value,
                    };
                    pieces.Add(Piece(pieceValue, kind, label.Length > 0 || previousEnd > 0 ? label : contextLabel));

                    // The key of a social security number is often asked in a box of its own
                    if (kind == CandidateKind.Number && pieceValue.Length == SocialSecurityLength && SensitiveFormats.IsFrenchSocialSecurityNumber(pieceValue))
                    {
                        pieces.Add(Piece(pieceValue[SocialSecurityBodyLength..], CandidateKind.Text, SocialSecurityKeyLabel));
                    }

                    previousEnd = start + value.Length;

                    if (kind == CandidateKind.Number && ValueValidator.IsFrenchPostalCode(value)
                        && NameRecognizer.CommuneNextToPostalCode(before, value, line[previousEnd..], data) is { } commune
                        && FitsLabel(CandidateKind.City, contextLabel))
                    {
                        pieces.Add(Piece(commune, CandidateKind.City, contextLabel));
                    }
                }

                // The whole line stays available, for example a postal code with its city
                if (line.Length <= MaxPieceLength && !string.Equals(line, values[0].Value, StringComparison.Ordinal))
                {
                    pieces.Add(Piece(line, CandidateKind.Text, contextLabel));
                }

                return pieces;
            }

            var pair = LabelValueRegex().Match(line);
            if (pair.Success)
            {
                var value = pair.Groups["value"].Value.Trim();
                var label = CleanLabel(pair.Groups["label"].Value);
                pieces.AddRange(RecognizeFitting(value, label, data));
                pieces.Add(Piece(value, CandidateKind.Text, label));
                return pieces;
            }

            pieces.AddRange(RecognizeFitting(line, contextLabel, data));
        }
        catch (RegexMatchTimeoutException)
        {
            // Pathological text: the line is kept as is
            pieces.Clear();
        }

        if (line.Length <= MaxPieceLength)
        {
            pieces.Add(Piece(line, CandidateKind.Text, contextLabel));
        }

        return pieces;
    }

    /// <summary>
    /// Finds the recognized values of a line, ordered by position.
    /// </summary>
    /// <param name="line">The line to analyze.</param>
    /// <returns>The start, text and kind of each value.</returns>
    private static List<(int Start, string Value, CandidateKind Kind)> FindValues(string line)
    {
        var values = new List<(int Start, string Value, CandidateKind Kind)>();
        foreach (var (pattern, kind) in Recognizers)
        {
            foreach (Match match in pattern().Matches(line))
            {
                var value = kind == CandidateKind.Url ? match.Value.TrimEnd(UrlTrimmedEndChars) : match.Value;
                if (kind == CandidateKind.Reference && value.Length < MinReferenceLength)
                {
                    continue;
                }

                // Safety net: a value with the right shape but an impossible content (wrong IBAN key, 31/02...)
                // is not taken for that kind, its text stays available to the next recognizers
                if (!ValueValidator.IsValid(kind, value))
                {
                    continue;
                }

                // A number written by groups, as « 812 345 676 », is only a number when it is a real SIREN or SIRET,
                // or a social security number with its key
                if (kind == CandidateKind.Number && value.Contains(' ', StringComparison.Ordinal)
                    && !ValueValidator.IsSirenOrSiret(value) && !SensitiveFormats.IsFrenchSocialSecurityNumber(value))
                {
                    continue;
                }

                var end = match.Index + value.Length;
                var isFree = values.TrueForAll(taken => end <= taken.Start || match.Index >= taken.Start + taken.Value.Length);
                if (isFree)
                {
                    values.Add((match.Index, value, kind));
                }
            }
        }

        values.Sort((first, second) => first.Start.CompareTo(second.Start));
        return values;
    }

    // A label of words only, before a colon, followed by several values: « Débiteur : M. Julien Masson, 22 avenue… »
    private static (string Label, string Values)? RoleOf(string line)
    {
        var pair = LabelValueRegex().Match(line);
        if (!pair.Success)
        {
            return null;
        }

        var label = CleanLabel(pair.Groups["label"].Value);
        var rest = pair.Groups["value"].Value.Trim();
        var isRole = label.Length > 0 && !label.Any(char.IsDigit) && !rest.StartsWith('/')
            && FindValues(rest) is { Count: > 0 } values
            && rest[..values[0].Start].Any(char.IsLetter);
        return isRole ? (label, rest) : null;
    }

    private static string? AddressIn(string values)
    {
        var parts = values.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var street = Array.FindIndex(parts, part => StreetRegex().IsMatch(part));
        if (street < 0)
        {
            return null;
        }

        var end = Array.FindIndex(parts, street, part => PostalCodeAndCommuneRegex().IsMatch(part));
        return string.Join(", ", parts[street..(end < 0 ? street + 1 : end + 1)]);
    }

    private static string WithRole(string role, string label)
    {
        return label.Length == 0 || label.Contains(role, StringComparison.Ordinal) ? (label.Length == 0 ? role : label) : $"{role} : {label}";
    }

    /// <summary>
    /// Removes the punctuation around a label and keeps its last words when it is too long.
    /// </summary>
    /// <param name="text">The text before a value.</param>
    /// <returns>The label, or an empty string if the text holds no letter.</returns>
    private static string CleanLabel(string text)
    {
        var label = text.Trim(LabelTrimmedChars);
        if (!label.Any(char.IsLetter))
        {
            return string.Empty;
        }

        if (label.Length <= MaxLabelLength)
        {
            return label;
        }

        var start = label.IndexOf(' ', label.Length - MaxLabelLength);
        return start < 0 ? label[^MaxLabelLength..] : label[(start + 1)..];
    }

    // A plate is written as the registration certificate shows it: « AB-123-CD »
    private static string CarPlate(string value)
    {
        var compact = value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return $"{compact[..2]}-{compact[2..5]}-{compact[5..]}";
    }

    private static TextPiece Piece(string value, CandidateKind kind, string label)
    {
        return new TextPiece { Value = value, Kind = kind, Label = label };
    }

    /// <summary>
    /// Keeps the first occurrence of each value, completed by the first label found for it.
    /// </summary>
    /// <param name="pieces">The pieces in order of appearance.</param>
    /// <returns>The pieces without duplicates.</returns>
    private static List<TextPiece> RemoveDuplicates(List<TextPiece> pieces)
    {
        var unique = new List<TextPiece>(pieces.Count);
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var piece in pieces)
        {
            if (!positions.TryGetValue(piece.Value, out var position))
            {
                positions[piece.Value] = unique.Count;
                unique.Add(piece);
            }
            else if (piece.Kind != CandidateKind.Text && piece.Label.Length > 0 && unique[position].Label.Length > 0
                && !string.Equals(unique[position].Label, piece.Label, StringComparison.Ordinal)
                && !unique.Exists(kept => kept.Value == piece.Value && kept.Label == piece.Label))
            {
                // The same value under another label is another piece of information: « Loyer : 720,00 € » and
                // « Dépôt de garantie : 720,00 € »
                unique.Add(piece);
            }
            else if (unique[position].Label.Length == 0 && piece.Label.Length > 0)
            {
                unique[position] = unique[position] with { Label = piece.Label };
            }
        }

        return unique;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>""]+", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,3})?\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"(?<![\d+])(?:\+\d{2,3}[ .\-]?(?:\(0\)[ .\-]?)?[1-9]|0[1-9])(?:[ .\-]?\d{2}){4}(?!\d)", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<!\d)(?:\d{4}-\d{2}-\d{2}|\d{1,2}[/.\-]\d{1,2}[/.\-](?:\d{4}|\d{2}))(?!\d)", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"(?:[€$£] ?\d{1,3}(?:[   .,]\d{3})*(?:[.,]\d{1,2})?|\d{1,3}(?:[   .]\d{3})*(?:[.,]\d{1,2})? ?(?:€|EUR|\$|USD|£))", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"\b(?=[A-Z0-9\-]*\d)(?=[A-Z0-9\-]*[A-Z])[A-Z0-9]+(?:-[A-Z0-9]+)*\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex ReferenceRegex();

    [GeneratedRegex(@"(?<![\d,.])\d{3} \d{3} \d{3}(?: \d{5})?(?![\d,.])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex CompanyNumberRegex();

    // A number written after « n° » is a reference even without letters: « Facture n° 2026-0312 »
    [GeneratedRegex(@"(?<=\b[Nn](?:°|º|o\.?)\s?)\d+(?:[-/.]\d+)+(?![\d/.-])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex NumberedReferenceRegex();

    [GeneratedRegex(@"(?<![\d,.])\d+(?:[.,]\d+)?\s?(?:m²|m2|m³|m3|km|kg|cm|mm|ha)(?![\p{L}\d])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex MeasureRegex();

    [GeneratedRegex(@"^\d{1,4}(?:\s?(?:bis|ter))?,?\s+(?:rue|avenue|av\.|boulevard|bd|place|all[ée]e|chemin|impasse|quai|route|cours|square|r[ée]sidence|lotissement|cit[ée]|voie|passage)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex StreetRegex();

    [GeneratedRegex(@"^\d{5}\s+\p{L}", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex PostalCodeAndCommuneRegex();

    [GeneratedRegex(@"(?<![\dA-Z])[1-478] ?\d{2} ?\d{2} ?(?:\d{2}|2[AB]) ?\d{3} ?\d{3} ?\d{2}(?![\dA-Z])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex SocialSecurityRegex();

    // The plates of the French registration system since 2009: two letters, three digits, two letters (I, O and U unused)
    [GeneratedRegex(@"\b[A-HJ-NP-TV-Z]{2}[ -]?\d{3}[ -]?[A-HJ-NP-TV-Z]{2}\b", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex CarPlateRegex();

    [GeneratedRegex(@"(?<![\d,.])\d{5,}(?![\d,.])", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"^(?<label>\p{L}[^:]{0,39}?)\s*:\s*(?<value>\S.*)$", RegexOptions.CultureInvariant, RegexTimeoutMilliseconds)]
    private static partial Regex LabelValueRegex();
}
