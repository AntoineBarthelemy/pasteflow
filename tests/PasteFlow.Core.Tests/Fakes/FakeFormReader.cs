using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Form reader that returns predefined fields.
/// </summary>
internal sealed class FakeFormReader : IFormReader
{
    public IReadOnlyList<FormField> Fields { get; init; } = Array.Empty<FormField>();

    public int ReadCount { get; private set; }

    public Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken)
    {
        ReadCount++;
        return Task.FromResult(Fields);
    }
}
