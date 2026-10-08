namespace PasteFlow.DataBuilder.Tests;

public sealed class CsvTableTests
{
    [Fact]
    public void Parse_DetectsSemicolonSeparatorAndSkipsHeader()
    {
        var table = CsvTable.Parse("﻿sexe;prenom;periode;valeur\r\n1;JEAN;1950;12000\r\n");

        var row = Assert.Single(table.Rows);
        Assert.Equal(new[] { "1", "JEAN", "1950", "12000" }, row);
    }

    [Fact]
    public void Parse_DetectsCommaSeparatorAndQuotedFields()
    {
        var table = CsvTable.Parse("code,name\n51500,\"TAISSY, \"\"LE BOURG\"\"\"\n");

        Assert.Equal(new[] { "51500", "TAISSY, \"LE BOURG\"" }, Assert.Single(table.Rows));
    }

    [Fact]
    public void ColumnOf_FindsColumnWhateverCaseAndAccents()
    {
        var table = CsvTable.Parse("#Code_commune_INSEE;Nom_de_la_commune;Code_postal;Libellé_d_acheminement;Ligne_5\n");

        Assert.Equal(2, table.ColumnOf("CODE_POSTAL"));
        Assert.Equal(3, table.ColumnOf("LIBELLE_D_ACHEMINEMENT"));
    }

    [Fact]
    public void ColumnOf_WhenColumnIsMissing_ExplainsTheFormatChanged()
    {
        var table = CsvTable.Parse("a;b\n");

        var exception = Assert.Throws<InvalidDataException>(() => table.ColumnOf("CODE_POSTAL"));
        Assert.Contains("CODE_POSTAL", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindColumn_WhenColumnIsMissing_ReturnsMinusOne()
    {
        Assert.Equal(-1, CsvTable.Parse("a;b\n").FindColumn("LIGNE_5"));
    }

    [Fact]
    public void Parse_EmptyText_GivesNoRow()
    {
        Assert.Empty(CsvTable.Parse(string.Empty).Rows);
    }
}
