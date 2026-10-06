using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Highlighter that records the highlighted fields instead of showing them.
/// </summary>
internal sealed class FakeFieldHighlighter : IFieldHighlighter
{
    public int CallCount { get; private set; }

    public IReadOnlyList<FieldToCheck> HighlightedFields { get; private set; } = Array.Empty<FieldToCheck>();

    public void Highlight(IReadOnlyList<FieldToCheck> fields)
    {
        CallCount++;
        HighlightedFields = fields;
    }
}
