using PasteFlow.Core.Matching;
using PasteFlow.Core.Tests.Fakes;

namespace PasteFlow.Core.Tests.Matching;

public sealed class FieldOwnershipTests
{
    [Theory]
    [InlineData("Votre e-mail")]
    [InlineData("Mon téléphone")]
    [InlineData("Your email")]
    public void FieldThatSaysYou_IsAboutTheUser(string label)
    {
        Assert.Equal(FieldOwner.User, FieldOwnership.Of(TestFields.Text("f", label), PageContext.Empty));
    }

    [Theory]
    [InlineData("E-mail du contact")]
    [InlineData("Téléphone du client")]
    [InlineData("Nom du destinataire")]
    [InlineData("E-mail du parrain")]
    public void FieldThatNamesSomeoneElse_IsAboutSomeoneElse(string label)
    {
        Assert.Equal(FieldOwner.SomeoneElse, FieldOwnership.Of(TestFields.Text("f", label), PageContext.Empty));
    }

    [Fact]
    public void FieldThatSaysBoth_IsUnknown()
    {
        Assert.Equal(FieldOwner.Unknown, FieldOwnership.Of(TestFields.Text("f", "Votre client"), new PageContext { Title = "Créer un compte" }));
    }

    [Theory]
    [InlineData("Créer un compte", FieldOwner.User)]
    [InlineData("Inscription à la newsletter", FieldOwner.User)]
    [InlineData("Connectez-vous", FieldOwner.User)]
    [InlineData("Candidature - Développeur", FieldOwner.User)]
    [InlineData("Sign up", FieldOwner.User)]
    [InlineData("Nouveau client", FieldOwner.SomeoneElse)]
    [InlineData("Ajouter un contact", FieldOwner.SomeoneElse)]
    [InlineData("Facture n° 42", FieldOwner.Unknown)]
    [InlineData("Connexion | Nouveau client", FieldOwner.Unknown)]
    public void NeutralField_FollowsThePage(string title, FieldOwner expected)
    {
        Assert.Equal(expected, FieldOwnership.Of(TestFields.Text("f", "E-mail"), new PageContext { Title = title }));
    }

    [Fact]
    public void FieldWords_WinOverThePage()
    {
        Assert.Equal(FieldOwner.SomeoneElse, FieldOwnership.Of(TestFields.Text("f", "E-mail du contact"), new PageContext { Title = "Créer un compte" }));
        Assert.Equal(FieldOwner.User, FieldOwnership.Of(TestFields.Text("f", "Votre e-mail"), new PageContext { Title = "Nouveau client" }));
    }

    [Fact]
    public void WordsInsideOtherWords_DoNotCount()
    {
        Assert.Equal(FieldOwner.Unknown, FieldOwnership.Of(TestFields.Text("f", "E-mail"), new PageContext { Title = "Reconnexion du serveur" }));
    }
}
