using PasteFlow.Core.Forms;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Builds form fields for the tests.
/// </summary>
internal static class TestFields
{
    public static FormField Text(string id, string label, bool hasValue = false)
    {
        return new FormField { Id = id, Label = label, Kind = FieldKind.Text, HasValue = hasValue };
    }

    public static FormField Password(string id, string label)
    {
        return new FormField { Id = id, Label = label, Kind = FieldKind.Password, HasValue = false };
    }

    public static FormField Choice(string id, string label, bool hasValue = false)
    {
        return new FormField { Id = id, Label = label, Kind = FieldKind.Choice, HasValue = hasValue };
    }
}
