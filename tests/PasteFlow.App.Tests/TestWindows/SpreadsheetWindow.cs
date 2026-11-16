using System.Windows;
using System.Windows.Controls;

namespace PasteFlow.App.Tests.TestWindows;

/// <summary>
/// A grid of cells too narrow for their text, like a spreadsheet: the screen shows the start of an e-mail address,
/// the cell holds it whole.
/// </summary>
internal sealed class SpreadsheetWindow : Window
{
    public const string WindowTitle = "Tableau de test PasteFlow";
    public const string Order = "CMD-4471";
    public const string Email = "helene.moreau@moreau-conseil.fr";

    public SpreadsheetWindow()
    {
        Title = WindowTitle;
        Left = 0;
        Top = 0;
        Width = 320;
        Height = 200;
        ShowActivated = false;
        ShowInTaskbar = false;

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.None,
            ItemsSource = new[] { new Row("Commande", "E-mail"), new Row(Order, Email) },
        };
        grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding(nameof(Row.First)), Width = 80 });
        grid.Columns.Add(new DataGridTextColumn { Binding = new System.Windows.Data.Binding(nameof(Row.Second)), Width = 80 });
        Content = grid;
    }

    /// <summary>
    /// One row of the grid.
    /// </summary>
    /// <param name="First">The first cell.</param>
    /// <param name="Second">The second cell.</param>
    public sealed record Row(string First, string Second);
}
