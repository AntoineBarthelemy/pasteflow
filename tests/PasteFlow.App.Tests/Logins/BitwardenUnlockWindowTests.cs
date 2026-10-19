using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PasteFlow.App.Logins;
using PasteFlow.App.Tests.TestWindows;

namespace PasteFlow.App.Tests.Logins;

public sealed class BitwardenUnlockWindowTests : IDisposable
{
    private readonly WindowHost<BitwardenUnlockWindow> _host = new(() => new BitwardenUnlockWindow(_ => Task.FromResult(false)));

    [Fact]
    public void Eye_ShowsThenHidesTheMasterPassword()
    {
        _ = _host.Invoke(window =>
        {
            TryFind(window)!.Password = "Mot-De-Passe-Maitre";
            return true;
        });
        var eye = Find(AutomationElement.FromHandle(_host.Handle), "Afficher le mot de passe");

        ((TogglePattern)eye.GetCurrentPattern(TogglePattern.Pattern)).Toggle();

        Assert.True(_host.Invoke(window => window.IsPasswordShown));
        Assert.Equal("Mot-De-Passe-Maitre", ShownText());

        ((TogglePattern)eye.GetCurrentPattern(TogglePattern.Pattern)).Toggle();

        Assert.False(_host.Invoke(window => window.IsPasswordShown));
        Assert.Equal("Mot-De-Passe-Maitre", _host.Invoke(window => window.TypedPassword));
        Assert.Equal(string.Empty, ShownText());
    }

    public void Dispose()
    {
        _host.Dispose();
    }

    private static PasswordBox? TryFind(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is PasswordBox box)
            {
                return box;
            }

            if (TryFind(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static AutomationElement Find(AutomationElement window, string name)
    {
        var element = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name));
        Assert.NotNull(element);
        return element;
    }

    private string ShownText()
    {
        return _host.Invoke(window =>
        {
            var visible = window.TypedPassword;
            return window.IsPasswordShown ? visible : string.Empty;
        });
    }
}
