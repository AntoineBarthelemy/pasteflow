namespace PasteFlow.DataBuilder.Tests;

public sealed class ReferenceListsTests
{
    [Fact]
    public void FirstNames_SumsBirthsOverYearsAndSexesAndKeepsFrequentNames()
    {
        var table = CsvTable.Parse(
            "SEXE;PRENOM;PERIODE;VALEUR;RANG\n"
            + "1;JÉRÔME;1975;300;10\n"
            + "1;JÉRÔME;1976;300;12\n"
            + "2;CAMILLE;1990;450;3\n"
            + "1;CAMILLE;1990;100;80\n"
            + "1;ZÉPHYRIN;1910;20;900\n");

        var names = ReferenceLists.FirstNames(table);

        Assert.Equal(new[] { "CAMILLE", "JEROME" }, names);
    }

    [Fact]
    public void FirstNames_SkipsRareNamesGroupAndInvalidCounts()
    {
        var table = CsvTable.Parse(
            "sexe;preusuel;annais;nombre\n"
            + "1;_PRENOMS_RARES;1950;90000\n"
            + "1;PIERRE;XXXX;abc\n"
            + "1;JEAN-PIERRE;1955;5000\n");

        Assert.Equal(new[] { "JEAN PIERRE" }, ReferenceLists.FirstNames(table));
    }

    [Fact]
    public void Communes_KeepsCommuneDeliveryNameAndLocalityForEachPostalCode()
    {
        var table = CsvTable.Parse(
            "#Code_commune_INSEE;Nom_de_la_commune;Code_postal;Libellé_d_acheminement;Ligne_5\n"
            + "51564;TAISSY;51500;TAISSY;\n"
            + "51454;REIMS;51100;REIMS;\n"
            + "85234;ST JEAN DE MONTS;85160;ST JEAN DE MONTS;OROUET\n"
            + "01001;L ABERGEMENT CLEMENCIAT;1400;L ABERGEMENT CLEMENCIAT;\n");

        var lines = ReferenceLists.Communes(table);

        Assert.Equal(
            new[] { "01400;L ABERGEMENT CLEMENCIAT", "51100;REIMS", "51500;TAISSY", "85160;OROUET", "85160;ST JEAN DE MONTS" },
            lines);
    }

    [Fact]
    public void Countries_MergesLanguagesWithoutDuplicates()
    {
        var names = ReferenceLists.Countries(new[]
        {
            "{\"FR\":\"France\",\"DE\":\"Allemagne\",\"CI\":\"Côte d’Ivoire\"}",
            "{\"FR\":\"France\",\"DE\":\"Germany\"}",
        });

        Assert.Equal(new[] { "ALLEMAGNE", "COTE D IVOIRE", "FRANCE", "GERMANY" }, names);
    }
}
