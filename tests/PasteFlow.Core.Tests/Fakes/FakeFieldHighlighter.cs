using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Highlighter that records the highlighted fields instead of showing them.
/// </summary>
internal sealed class FakeFieldHighlighter : IFieldHighlighter
{
    public int CallCount { get; private set; }

    public IReadOnlyList<FormField> HighlightedFields { get; private set; } = Array.Empty<FormField>();

    public void Highlight(IReadOnlyList<FormField> fields)
    {
        CallCount++;
        HighlightedFields = fields;
    }
}
