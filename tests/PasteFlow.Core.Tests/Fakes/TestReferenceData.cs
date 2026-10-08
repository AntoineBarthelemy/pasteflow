using PasteFlow.Core.Reference;

namespace PasteFlow.Core.Tests.Fakes;

/// <summary>
/// Small official lists, so that tests do not depend on the content of the real ones.
/// </summary>
internal static class TestReferenceData
{
    public static ReferenceData Create()
    {
        return new ReferenceData(
            new[] { "Antoine", "Marie", "Jean", "Pierre", "Jean-Pierre", "Martin", "Rose", "France", "Claude", "Charles" },
            new[]
            {
                "51500;TAISSY", "51100;REIMS", "75010;PARIS 10", "85160;ST JEAN DE MONTS", "80120;RUE", "67220;VILLE",
                "84100;ORANGE", "51000;CHALONS EN CHAMPAGNE",
            },
            new[] { "France", "Belgique", "Germany" });
    }
}
