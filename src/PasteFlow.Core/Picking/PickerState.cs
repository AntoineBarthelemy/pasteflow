using PasteFlow.Core.Candidates;

namespace PasteFlow.Core.Picking;

/// <summary>
/// What the list shown at the cursor displays, and how the keys of the user change it.
/// </summary>
/// <remarks>
/// <para>Before anything is typed, the list shows the most likely values. As soon as the user types, it shows the
/// passages of the windows holding those letters, cut at the typed words; Tab widens the cut to the sentence, then the
/// line. The chosen passage can then be adjusted word by word at either end.</para>
/// </remarks>
public sealed class PickerState
{
    /// <summary>
    /// The number of addresses behind links offered at most next to the passages found.
    /// </summary>
    public const int MaxLinkResults = 2;

    private const int MaxLinkTextLength = 40;

    private static readonly PassageCut[] Cuts = { PassageCut.Words, PassageCut.Sentence, PassageCut.Line, PassageCut.List, PassageCut.Page };

    private readonly PickRequest _request;
    private List<PickItem> _items;

    /// <summary>
    /// Initializes a new instance of the <see cref="PickerState"/> class, showing the most likely values.
    /// </summary>
    /// <param name="request">The values and the search to offer.</param>
    public PickerState(PickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _request = request;
        _items = SuggestedItems();
    }

    /// <summary>
    /// Gets the letters typed by the user.
    /// </summary>
    public string Query { get; private set; } = string.Empty;

    /// <summary>
    /// Gets how much text around the typed letters the passages take.
    /// </summary>
    public PassageCut Cut { get; private set; } = PassageCut.Words;

    /// <summary>
    /// Gets a value indicating whether the list shows passages found from typed letters rather than likely values.
    /// </summary>
    public bool IsSearching => Query.Trim().Length > 0;

    /// <summary>
    /// Gets the choices shown, at most <see cref="PassageFinder.MaxResults"/>.
    /// </summary>
    public IReadOnlyList<PickItem> Items => _items;

    /// <summary>
    /// Gets the position of the highlighted choice, or -1 when the list is empty.
    /// </summary>
    public int SelectedIndex { get; private set; }

    /// <summary>
    /// Gets the highlighted choice, or <see langword="null"/> when the list is empty.
    /// </summary>
    public PickItem? Selected => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;

    /// <summary>
    /// Changes the letters typed and updates the list.
    /// </summary>
    /// <param name="query">The letters typed.</param>
    public void SetQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        Query = query;
        Refresh();
    }

    /// <summary>
    /// Widens the cut of the passages: typed words, then sentence, then line, then the whole list, then the whole page, then typed words again.
    /// </summary>
    public void NextCut()
    {
        Cut = Cuts[(Array.IndexOf(Cuts, Cut) + 1) % Cuts.Length];
        if (IsSearching)
        {
            Refresh();
        }
    }

    /// <summary>
    /// Moves the highlight up or down the list, staying inside it.
    /// </summary>
    /// <param name="delta">-1 to go up, 1 to go down.</param>
    public void MoveSelection(int delta)
    {
        if (_items.Count > 0)
        {
            SelectedIndex = Math.Clamp(SelectedIndex + delta, 0, _items.Count - 1);
        }
    }

    /// <summary>
    /// Adds the next word at the end of the highlighted passage.
    /// </summary>
    public void ExtendEnd()
    {
        ChangeSelectedPassage(PassageFinder.ExtendEnd);
    }

    /// <summary>
    /// Removes the last word of the highlighted passage.
    /// </summary>
    public void ShrinkEnd()
    {
        ChangeSelectedPassage(PassageFinder.ShrinkEnd);
    }

    /// <summary>
    /// Adds the previous word at the start of the highlighted passage.
    /// </summary>
    public void ExtendStart()
    {
        ChangeSelectedPassage(PassageFinder.ExtendStart);
    }

    /// <summary>
    /// Removes the first word of the highlighted passage.
    /// </summary>
    public void ShrinkStart()
    {
        ChangeSelectedPassage(PassageFinder.ShrinkStart);
    }

    private static string Describe(CandidateKind kind)
    {
        return kind switch
        {
            CandidateKind.Email => "e-mail",
            CandidateKind.Phone => "téléphone",
            CandidateKind.Url => "lien",
            CandidateKind.Date => "date",
            CandidateKind.Amount => "montant",
            CandidateKind.Iban => "IBAN",
            CandidateKind.Reference => "référence",
            CandidateKind.Number => "numéro",
            CandidateKind.FirstName => "prénom",
            CandidateKind.LastName => "nom",
            CandidateKind.FullName => "nom",
            CandidateKind.City => "ville",
            CandidateKind.Country => "pays",
            CandidateKind.Company => "entreprise",
            CandidateKind.Measure => "mesure",
            _ => string.Empty,
        };
    }

    private static string DescribeLink(Candidate link)
    {
        var text = link.Label.Trim();
        return text.Length == 0
            ? "lien"
            : $"lien : {(text.Length > MaxLinkTextLength ? $"{text[..(MaxLinkTextLength - 1)]}…" : text)}";
    }

    private static bool IsAtWordStart(string folded, string query)
    {
        for (var index = folded.IndexOf(query, StringComparison.Ordinal); index >= 0; index = folded.IndexOf(query, index + 1, StringComparison.Ordinal))
        {
            if (index == 0 || !char.IsLetterOrDigit(folded[index - 1]))
            {
                return true;
            }
        }

        return false;
    }

    private List<PickItem> SuggestedItems()
    {
        return _request.Suggestions
            .Take(PassageFinder.MaxResults)
            .Select(candidate => new PickItem
            {
                Text = candidate.Value,
                SourceName = candidate.SourceName,
                Description = candidate.IsLinkAddress ? DescribeLink(candidate) : Describe(candidate.Kind),
            })
            .ToList();
    }

    private void Refresh()
    {
        if (!IsSearching)
        {
            _items = SuggestedItems();
        }
        else
        {
            // The text found comes first; the address behind a link or of a page whose words match follows it
            var links = LinksFound(Query);
            _items = _request.Finder.Find(Query, Cut)
                .Take(PassageFinder.MaxResults - links.Count)
                .Select(passage => new PickItem { Text = passage.Text, SourceName = passage.SourceName, Passage = passage })
                .Concat(links)
                .ToList();
        }

        SelectedIndex = _items.Count > 0 ? 0 : -1;
    }

    private List<PickItem> LinksFound(string query)
    {
        var folded = PassageFinder.FoldQuery(query);
        if (folded.Length == 0)
        {
            return new List<PickItem>();
        }

        return _request.Links
            .Where(link => IsAtWordStart(PassageFinder.Fold(link.Label), folded) || IsAtWordStart(PassageFinder.Fold(link.Value), folded))
            .DistinctBy(link => link.Value, StringComparer.Ordinal)
            .Take(MaxLinkResults)
            .Select(link => new PickItem { Text = link.Value, SourceName = link.SourceName, Description = DescribeLink(link) })
            .ToList();
    }

    private void ChangeSelectedPassage(Func<Passage, Passage> change)
    {
        if (Selected is not { Passage: { } passage } item)
        {
            return;
        }

        var changed = change(passage);
        _items[SelectedIndex] = item with { Text = changed.Text, Passage = changed };
    }
}
