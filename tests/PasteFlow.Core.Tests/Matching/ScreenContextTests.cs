using PasteFlow.Core.Candidates;
using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Tests.Matching;

public sealed class ScreenContextTests
{
    private const string LeaEmail = "lea.martin@atelier-nova.fr";
    private const string HugoEmail = "hugo@atelier-nova.fr";

    private static readonly Candidate[] Emails =
    {
        Email(LeaEmail, context: "Léa Martin, responsable communication"),
        Email(HugoEmail, context: "Hugo Bernard, fondateur"),
    };

    [Fact]
    public void Words_KeepsMeaningfulWordsWithoutAccentsOrCase()
    {
        var words = ScreenContext.Words("Bonjour Léa, voici lea.martin@gmail.com");

        Assert.Equal(new[] { "LEA", "MARTIN", "VOICI" }, words.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ChooseAmong_WithTheGreetedPerson_ChoosesTheirValue()
    {
        var context = Context("Bonjour Léa,", "Je prépare une interview");

        Assert.Equal(new[] { LeaEmail }, context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WhenTheGreetedPersonIsNotTheOnlyOneMentioned_StillChoosesTheGreetedOne()
    {
        var context = Context("Bonjour Léa,", "J'aimerais beaucoup recevoir Hugo dans ma chaîne.");

        Assert.Equal(new[] { LeaEmail }, context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WithoutGreeting_UsesTheRestOfTheWindow()
    {
        var context = Context("Objet : rendez-vous avec Hugo");

        Assert.Equal(new[] { HugoEmail }, context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WhenBothPeopleAreMentioned_ChoosesNothing()
    {
        var context = Context("Réunion avec Léa et Hugo");

        Assert.Empty(context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WithWordsSharedByBothValues_ChoosesNothing()
    {
        var context = Context("Interview Atelier Nova");

        Assert.Empty(context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WithTheValueItselfInTheWindow_DoesNotLetItPointToItself()
    {
        var context = Context($"Copie à {LeaEmail}");

        Assert.Empty(context.ChooseAmong(Emails));
    }

    [Fact]
    public void ChooseAmong_WithASingleValue_ChoosesNothing()
    {
        var context = Context("Bonjour Léa,");

        Assert.Empty(context.ChooseAmong(new[] { Emails[0] }));
    }

    [Fact]
    public void ChooseAmong_NeverChoosesPlainText()
    {
        var texts = new[]
        {
            new Candidate { Value = "Léa viendra mardi", SourceName = "Claude", SourceRank = 1 },
            new Candidate { Value = "Hugo viendra jeudi", SourceName = "Claude", SourceRank = 1 },
        };

        Assert.Empty(Context("Bonjour Léa,").ChooseAmong(texts));
    }

    [Fact]
    public void ChooseAmong_TellsApartByTheOwnWordsOfTheValue()
    {
        var bare = new[] { Email(LeaEmail, context: string.Empty), Email(HugoEmail, context: string.Empty) };

        Assert.Equal(new[] { HugoEmail }, Context("Salut Hugo !").ChooseAmong(bare));
    }

    [Fact]
    public void Empty_ChoosesNothing()
    {
        Assert.Empty(ScreenContext.Empty.ChooseAmong(Emails));
    }

    [Fact]
    public void From_IgnoresOtherWindowsAndHistory()
    {
        var context = ScreenContext.From(new[]
        {
            new Candidate { Value = "Bonjour Léa,", SourceName = "Claude", SourceRank = 1 },
            new Candidate { Value = "Bonjour Léa,", SourceName = "Historique", SourceRank = 0, IsFromHistory = true },
        });

        Assert.Empty(context.ChooseAmong(Emails));
    }

    private static Candidate Email(string value, string context)
    {
        return new Candidate { Value = value, Kind = CandidateKind.Email, SourceName = "Claude", SourceRank = 1, Context = context };
    }

    private static ScreenContext Context(params string[] lines)
    {
        return ScreenContext.From(lines.Select(line => new Candidate { Value = line, SourceName = "Gmail", SourceRank = 0 }));
    }
}
