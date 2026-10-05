using System.Runtime.InteropServices;
using System.Windows.Automation;
using PasteFlow.App.Interop;
using PasteFlow.Core.Forms;

namespace PasteFlow.App.Forms;

/// <summary>
/// Reads the fields of the foreground window through Windows UI Automation, the accessibility API.
/// </summary>
/// <remarks>
/// The reading runs on a background thread, as recommended for UI Automation clients.
/// Values are never read from password fields.
/// </remarks>
internal sealed class UiAutomationFormReader : IFormReader
{
    private static readonly Condition FieldCondition = new AndCondition(
        new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox)),
        new PropertyCondition(AutomationElement.IsEnabledProperty, true),
        new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

    /// <inheritdoc/>
    public Task<IReadOnlyList<FormField>> ReadActiveFormAsync(CancellationToken cancellationToken)
    {
        // The foreground window is captured right away, before anything else can take the focus
        var windowHandle = NativeMethods.GetForegroundWindow();
        return Task.Run(() => ReadFields(windowHandle, cancellationToken), cancellationToken);
    }

    private static IReadOnlyList<FormField> ReadFields(IntPtr windowHandle, CancellationToken cancellationToken)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return Array.Empty<FormField>();
        }

        try
        {
            var window = AutomationElement.FromHandle(windowHandle);
            var elements = window.FindAll(TreeScope.Descendants, FieldCondition);

            var fields = new List<FormField>(elements.Count);
            foreach (AutomationElement element in elements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var field = TryReadField(element);
                if (field is not null)
                {
                    fields.Add(field);
                }
            }

            return fields;
        }
        catch (ElementNotAvailableException)
        {
            // The window was closed while being read
            return Array.Empty<FormField>();
        }
        catch (COMException)
        {
            // The application does not answer accessibility requests
            return Array.Empty<FormField>();
        }
    }

    private static FormField? TryReadField(AutomationElement element)
    {
        try
        {
            var current = element.Current;
            var kind = current.IsPassword
                ? FieldKind.Password
                : current.ControlType == ControlType.ComboBox ? FieldKind.Choice : FieldKind.Text;

            return new FormField
            {
                Label = ReadLabel(current),
                Kind = kind,
                HasValue = kind != FieldKind.Password && HasValue(element),
            };
        }
        catch (ElementNotAvailableException)
        {
            // The field disappeared while being read
            return null;
        }
    }

    private static string ReadLabel(AutomationElement.AutomationElementInformation current)
    {
        if (!string.IsNullOrWhiteSpace(current.Name))
        {
            return current.Name.Trim();
        }

        var labelElement = current.LabeledBy;
        if (labelElement is not null && !string.IsNullOrWhiteSpace(labelElement.Current.Name))
        {
            return labelElement.Current.Name.Trim();
        }

        // Web pages often expose the placeholder as help text
        return current.HelpText?.Trim() ?? string.Empty;
    }

    private static bool HasValue(AutomationElement element)
    {
        return element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern valuePattern
            && !string.IsNullOrWhiteSpace(valuePattern.Current.Value);
    }
}
