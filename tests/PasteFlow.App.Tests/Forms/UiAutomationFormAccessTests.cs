using PasteFlow.App.Forms;
using PasteFlow.App.Tests.TestWindows;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Tests.Forms;

public sealed class UiAutomationFormAccessTests : IDisposable
{
    private readonly WindowHost<ContactFormWindow> _host = new(() => new ContactFormWindow());
    private readonly UiAutomationFormAccess _formAccess = new();

    [Fact]
    public async Task ReadFormAsync_FindsEnabledFieldsInReadingOrder()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.Equal(new[] { "Nom", "Email", "Ville", "Mot de passe", "Pays", string.Empty }, fields.Select(field => field.Label));
    }

    [Fact]
    public async Task ReadFormAsync_SkipsDisabledFields()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.DoesNotContain(fields, field => field.Label == "Désactivé");
    }

    [Fact]
    public async Task ReadFormAsync_DetectsKindOfEachField()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.Equal(FieldKind.Text, Field(fields, "Nom").Kind);
        Assert.Equal(FieldKind.Password, Field(fields, "Mot de passe").Kind);
        Assert.Equal(FieldKind.Choice, Field(fields, "Pays").Kind);
    }

    [Fact]
    public async Task ReadFormAsync_DetectsFieldsAlreadyFilled()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.False(Field(fields, "Nom").HasValue);
        Assert.True(Field(fields, "Email").HasValue);
    }

    [Fact]
    public async Task ReadFormAsync_UsesLabelElementWhenFieldHasNoName()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.Contains(fields, field => field.Label == "Ville");
    }

    [Fact]
    public async Task ReadFormAsync_ReadsHelpTextAsHint()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.Equal(ContactFormWindow.EmailHint, Field(fields, "Email").Hint);
        Assert.Equal(string.Empty, Field(fields, "Nom").Hint);
    }

    [Fact]
    public async Task ReadFormAsync_ReadsTechnicalName()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        var phone = Assert.Single(fields, field => field.TechnicalName == ContactFormWindow.PhoneTechnicalName);
        Assert.Equal(string.Empty, phone.Label);
    }

    [Fact]
    public async Task ReadFormAsync_NeverReadsPasswordValue()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.False(Field(fields, "Mot de passe").HasValue);
    }

    [Fact]
    public async Task ReadFormAsync_GivesUniqueIdentifiers()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        Assert.Equal(fields.Count, fields.Select(field => field.Id).Distinct().Count());
    }

    [Fact]
    public async Task ReadFormAsync_WithoutWindow_ReturnsNothing()
    {
        var fields = await _formAccess.ReadFormAsync(IntPtr.Zero, CancellationToken.None);

        Assert.Empty(fields);
    }

    [Fact]
    public async Task TryWriteAsync_WritesValueIntoField()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        var isWritten = await _formAccess.TryWriteAsync(Field(fields, "Nom"), "Antoine", CancellationToken.None);

        Assert.True(isWritten);
        Assert.Equal("Antoine", _host.Invoke(window => window.NameBox.Text));
    }

    [Fact]
    public async Task TryWriteAsync_WritesFieldLabelledByAnotherElement()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);

        var isWritten = await _formAccess.TryWriteAsync(Field(fields, "Ville"), "Reims", CancellationToken.None);

        Assert.True(isWritten);
        Assert.Equal("Reims", _host.Invoke(window => window.CityBox.Text));
    }

    [Fact]
    public async Task TryWriteAsync_WithFieldNotFromLastReading_ReturnsFalse()
    {
        _ = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);
        var unknownField = new FormField { Id = "unknown", Label = "Nom", Kind = FieldKind.Text, HasValue = false };

        var isWritten = await _formAccess.TryWriteAsync(unknownField, "Antoine", CancellationToken.None);

        Assert.False(isWritten);
        Assert.Equal(string.Empty, _host.Invoke(window => window.NameBox.Text));
    }

    [Fact]
    public async Task TryWriteAsync_AfterReadingAnotherWindow_DoesNotWriteIntoOldFields()
    {
        var fields = await _formAccess.ReadFormAsync(_host.Handle, CancellationToken.None);
        _ = await _formAccess.ReadFormAsync(IntPtr.Zero, CancellationToken.None);

        var isWritten = await _formAccess.TryWriteAsync(Field(fields, "Nom"), "Antoine", CancellationToken.None);

        Assert.False(isWritten);
        Assert.Equal(string.Empty, _host.Invoke(window => window.NameBox.Text));
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static FormField Field(IReadOnlyList<FormField> fields, string label)
    {
        return Assert.Single(fields, field => field.Label == label);
    }
}
