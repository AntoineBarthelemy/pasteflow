using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Form reader that returns predefined fields.
/// </summary>
internal sealed class FakeFormReader : IFormReader
{
    public IReadOnlyList<FormField> Fields { get; set; } = Array.Empty<FormField>();

    public int ReadCount { get; private set; }

    /// <summary>
    /// Gets a reading that stays pending until the test completes it, to check what happens meanwhile.
    /// </summary>
    public TaskCompletionSource<IReadOnlyList<FormField>>? PendingReading { get; init; }

    public Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken)
    {
        ReadCount++;
        return PendingReading?.Task ?? Task.FromResult(Fields);
    }
}
