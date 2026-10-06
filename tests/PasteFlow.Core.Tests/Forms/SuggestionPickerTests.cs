using PasteFlow.Core.Forms;
using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Forms;

public sealed class SuggestionPickerTests
{
    private readonly FakeFieldWriter _fields = new();
    private readonly FillMemory _fillMemory = new();

    [Theory]
    [InlineData(0, "antoine.b@orange.fr")]
    [InlineData(1, "contact@reims-auto.fr")]
    public async Task TryPickAsync_WritesThePickedValue(int index, string expected)
    {
        var picker = new SuggestionPicker(_fields, _fillMemory);

        var isWritten = await picker.TryPickAsync(EmailToCheck(), index, CancellationToken.None);

        Assert.True(isWritten);
        Assert.Equal(expected, _fields.CurrentValues["email"]);
    }

    [Fact]
    public async Task TryPickAsync_AddsThePickedValueToTheLastFillSoItCanBeUndone()
    {
        _fillMemory.Remember(new[] { new WrittenField { Field = TestFields.Text("city", "Ville"), Value = "Reims" } });
        var picker = new SuggestionPicker(_fields, _fillMemory);

        _ = await picker.TryPickAsync(EmailToCheck(), 0, CancellationToken.None);

        Assert.Equal(new[] { "city", "email" }, _fillMemory.TakeLastFill().Select(written => written.Field.Id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public async Task TryPickAsync_WithoutSuchSuggestion_WritesNothing(int index)
    {
        var picker = new SuggestionPicker(_fields, _fillMemory);

        var isWritten = await picker.TryPickAsync(EmailToCheck(), index, CancellationToken.None);

        Assert.False(isWritten);
        Assert.Empty(_fields.WrittenValues);
        Assert.Empty(_fillMemory.TakeLastFill());
    }

    [Fact]
    public async Task TryPickAsync_WhenFieldRefusesTheValue_DoesNotRememberIt()
    {
        _fields.FailingFieldIds.Add("email");
        var picker = new SuggestionPicker(_fields, _fillMemory);

        var isWritten = await picker.TryPickAsync(EmailToCheck(), 0, CancellationToken.None);

        Assert.False(isWritten);
        Assert.Empty(_fillMemory.TakeLastFill());
    }

    private static FieldToCheck EmailToCheck()
    {
        return new FieldToCheck
        {
            Field = TestFields.Text("email", "Email"),
            Suggestions = new[]
            {
                new SuggestedValue { Value = "antoine.b@orange.fr", SourceName = "Gmail" },
                new SuggestedValue { Value = "contact@reims-auto.fr", SourceName = "Gmail" },
            },
        };
    }
}
