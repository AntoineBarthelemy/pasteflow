using PasteFlow.Core.Candidates;
using PasteFlow.Core.Hints;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Hints;

public sealed class HintTargetsTests
{
    [Fact]
    public void From_GivesACodeToEachValueFromTopToBottom()
    {
        var targets = HintTargets.From(
            new[]
            {
                Text("Tél. : 06 12 34 56 78", top: 200),
                Text("antoine.b@orange.fr", top: 100),
            },
            TestReferenceData.Create());

        Assert.Equal(new[] { "antoine.b@orange.fr", "06 12 34 56 78" }, targets.Select(target => target.Value));
        Assert.Equal(new[] { "F", "J" }, targets.Select(target => target.Code));
        Assert.Equal(CandidateKind.Email, targets[0].Kind);
    }

    [Fact]
    public void From_LeavesLabelsAndLongSentencesAside()
    {
        var targets = HintTargets.From(
            new[]
            {
                Text("Email :", top: 10),
                Text("Merci de bien vouloir nous retourner le formulaire complété avant la fin du mois prochain.", top: 20),
                Text("Garage Martin", top: 30),
            },
            TestReferenceData.Create());

        Assert.Equal("Garage Martin", Assert.Single(targets).Value);
    }

    [Fact]
    public void From_GivesASingleCodeToAValueSeenTwice()
    {
        var targets = HintTargets.From(
            new[] { Text("antoine.b@orange.fr", top: 10), Text("antoine.b@orange.fr", top: 300) },
            TestReferenceData.Create());

        Assert.Equal(10, Assert.Single(targets).Place.Top);
    }

    [Fact]
    public void From_KeepsRecognizedValuesWhenThereAreTooManyTexts()
    {
        var texts = Enumerable.Range(0, 100).Select(index => Text($"Ligne {(char)('a' + (index / 26))}{(char)('a' + (index % 26))}", top: index))
            .Append(Text("antoine.b@orange.fr", top: 1000))
            .ToList();

        var targets = HintTargets.From(texts, TestReferenceData.Create());

        Assert.Equal(HintTargets.MaxTargets, targets.Count);
        Assert.Contains(targets, target => target.Value == "antoine.b@orange.fr");
    }

    private static ScreenText Text(string text, double top)
    {
        return new ScreenText { Text = text, Left = 10, Top = top, Width = 200, Height = 20 };
    }
}
