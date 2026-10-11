using PasteFlow.Core.Picking;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// A text cursor, a list and a keyboard under the control of the tests.
/// </summary>
internal sealed class FakeCaret : ICaretReader, IPastePicker, ITextInserter
{
    public CaretContext? Caret { get; set; }

    /// <summary>
    /// Gets or sets what the user chooses in the list, from what it offers; <see langword="null"/> to cancel.
    /// </summary>
    public Func<PickRequest, string?> Choose { get; set; } = _ => null;

    public PickRequest? ShownRequest { get; private set; }

    public List<string> InsertedTexts { get; } = new();

    public bool AcceptsText { get; set; } = true;

    public Task<CaretContext?> ReadCaretAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Caret);
    }

    public Task<string?> PickAsync(PickRequest request, CancellationToken cancellationToken)
    {
        ShownRequest = request;
        return Task.FromResult(Choose(request));
    }

    public Task<bool> InsertAsync(string text, CancellationToken cancellationToken)
    {
        if (AcceptsText)
        {
            InsertedTexts.Add(text);
        }

        return Task.FromResult(AcceptsText);
    }
}
