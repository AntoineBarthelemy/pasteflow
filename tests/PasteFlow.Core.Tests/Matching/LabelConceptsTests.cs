using PasteFlow.Core.Candidates;
using PasteFlow.Core.Matching;

namespace PasteFlow.Core.Tests.Matching;

public sealed class LabelConceptsTests
{
    [Theory]
    [InlineData("Email", LabelConcepts.Email)]
    [InlineData("E-mail", LabelConcepts.Email)]
    [InlineData("COURRIEL", LabelConcepts.Email)]
    [InlineData("Adresse mail", LabelConcepts.Email)]
    [InlineData("Adresse-mail", LabelConcepts.Email)]
    [InlineData("E mail", LabelConcepts.Email)]
    [InlineData("Adresse électronique", LabelConcepts.Email)]
    [InlineData("Votre adresse e-mail *", LabelConcepts.Email)]
    [InlineData("Tél.", LabelConcepts.Phone)]
    [InlineData("Numéro de téléphone", LabelConcepts.Phone)]
    [InlineData("Portable", LabelConcepts.Phone)]
    [InlineData("Référence", LabelConcepts.Order)]
    [InlineData("Commande n°", LabelConcepts.Order)]
    [InlineData("Order number", LabelConcepts.Order)]
    [InlineData("Prénom", LabelConcepts.FirstName)]
    [InlineData("First name", LabelConcepts.FirstName)]
    [InlineData("Last name", LabelConcepts.Name)]
    [InlineData("Zip code", LabelConcepts.PostalCode)]
    [InlineData("Adresse", LabelConcepts.Address)]
    [InlineData("Adresse postale", LabelConcepts.Address)]
    [InlineData("Ville", LabelConcepts.City)]
    [InlineData("Date de naissance", LabelConcepts.Birth)]
    public void From_RecognizesConceptWhateverTheWording(string label, string concept)
    {
        Assert.Contains(concept, LabelConcepts.From(label));
    }

    [Fact]
    public void From_IgnoresFillerWords()
    {
        var concepts = LabelConcepts.From("Veuillez saisir votre numéro de commande");

        Assert.Equal(new[] { LabelConcepts.Order }, concepts);
    }

    [Fact]
    public void From_KeepsUnknownWordsAsConcepts()
    {
        var concepts = LabelConcepts.From("Code promo");

        Assert.Equal(new[] { "code", "promo" }, concepts.Order());
    }

    [Fact]
    public void From_DistinguishesPostalCodeFromPostalAddress()
    {
        Assert.Contains(LabelConcepts.PostalCode, LabelConcepts.From("Code postal"));
        Assert.DoesNotContain(LabelConcepts.PostalCode, LabelConcepts.From("Adresse postale"));
    }

    [Theory]
    [InlineData("Confirmez votre email")]
    [InlineData("Confirmation de l'e-mail")]
    [InlineData("Répétez votre adresse mail")]
    [InlineData("Retype email")]
    public void From_IgnoresConfirmationWords(string label)
    {
        Assert.Equal(new[] { LabelConcepts.Email }, LabelConcepts.From(label));
    }

    [Fact]
    public void From_KeepsConfirmationWhenItIsTheValueItself()
    {
        Assert.Contains("confirmation", LabelConcepts.From("Numéro de confirmation"));
    }

    [Theory]
    [InlineData("Confirmez votre email", true)]
    [InlineData("Confirmation de l'e-mail", true)]
    [InlineData("Répéter le mot de passe", true)]
    [InlineData("Email (again)", true)]
    [InlineData("email Confirm", true)]
    [InlineData("Email", false)]
    [InlineData("Code de vérification", false)]
    [InlineData("Numéro de confirmation", false)]
    [InlineData("Code de confirmation", false)]
    [InlineData("", false)]
    public void IsConfirmation_FindsConfirmationWords(string text, bool expected)
    {
        Assert.Equal(expected, LabelConcepts.IsConfirmation(text));
    }

    [Fact]
    public void From_WithEmptyLabel_ReturnsNothing()
    {
        Assert.Empty(LabelConcepts.From(string.Empty));
    }

    [Theory]
    [InlineData("Email", CandidateKind.Email)]
    [InlineData("Téléphone", CandidateKind.Phone)]
    [InlineData("Site web", CandidateKind.Url)]
    [InlineData("Date", CandidateKind.Date)]
    [InlineData("Montant", CandidateKind.Amount)]
    [InlineData("IBAN", CandidateKind.Iban)]
    [InlineData("Référence", CandidateKind.Reference)]
    [InlineData("Référence", CandidateKind.Number)]
    [InlineData("Code postal", CandidateKind.Number)]
    [InlineData("Nom", CandidateKind.Text)]
    [InlineData("Ville", CandidateKind.Text)]
    [InlineData("Nom et prénom du locataire", CandidateKind.FullName)]
    [InlineData("Nom du débiteur", CandidateKind.FullName)]
    [InlineData("Nom du créancier", CandidateKind.Company)]
    [InlineData("Patient", CandidateKind.FullName)]
    [InlineData("Date de facture", CandidateKind.Date)]
    [InlineData("SIREN du fournisseur", CandidateKind.Number)]
    public void ExpectedKinds_DeducesKindFromLabel(string label, CandidateKind kind)
    {
        Assert.Contains(kind, LabelConcepts.ExpectedKinds(LabelConcepts.From(label)));
    }

    [Theory]
    [InlineData("Prénom", new[] { CandidateKind.FirstName, CandidateKind.Text })]
    [InlineData("Nom", new[] { CandidateKind.FullName, CandidateKind.Text })]
    [InlineData("Nom de famille", new[] { CandidateKind.LastName, CandidateKind.Text })]
    [InlineData("Nom et prénom", new[] { CandidateKind.FullName, CandidateKind.Text })]
    [InlineData("Nom complet", new[] { CandidateKind.FullName, CandidateKind.Text })]
    [InlineData("Full name", new[] { CandidateKind.FullName, CandidateKind.Text })]
    [InlineData("Nom de naissance", new[] { CandidateKind.FullName, CandidateKind.Text })]
    [InlineData("Ville", new[] { CandidateKind.City, CandidateKind.Text })]
    [InlineData("Ville de naissance", new[] { CandidateKind.City, CandidateKind.Text })]
    [InlineData("Lieu de naissance", new[] { CandidateKind.City, CandidateKind.Text })]
    [InlineData("Place of birth", new[] { CandidateKind.City, CandidateKind.Text })]
    [InlineData("Pays", new[] { CandidateKind.Country, CandidateKind.Text })]
    [InlineData("Entreprise", new[] { CandidateKind.Company, CandidateKind.Text })]
    [InlineData("Nom de l'entreprise", new[] { CandidateKind.Company, CandidateKind.Text })]
    [InlineData("Adresse", new[] { CandidateKind.Text })]
    [InlineData("Nom du client", new[] { CandidateKind.Text })]
    [InlineData("Nom d'utilisateur", new[] { CandidateKind.Text })]
    [InlineData("Prénom de l'enfant", new[] { CandidateKind.Text })]
    [InlineData("Date de naissance", new[] { CandidateKind.Date })]
    [InlineData("Numéro client", new[] { CandidateKind.Reference, CandidateKind.Number })]
    public void ExpectedKinds_ForNamesPlacesAndCompanies_AcceptsRecognizedValuesAndText(string label, CandidateKind[] expected)
    {
        Assert.Equal(expected.Order(), LabelConcepts.ExpectedKinds(LabelConcepts.From(label)).Order());
    }

    [Theory]
    [InlineData("Nom", CandidateKind.LastName)]
    [InlineData("Last name", CandidateKind.LastName)]
    [InlineData("Nom et prénom", CandidateKind.FullName)]
    public void ExpectedKinds_WhenTheFormAsksTheFirstNameApart_NameMeansLastName(string label, CandidateKind expected)
    {
        Assert.Contains(expected, LabelConcepts.ExpectedKinds(LabelConcepts.From(label), firstNameAskedApart: true));
    }

    [Fact]
    public void ExpectedKinds_WithoutClue_ReturnsNothing()
    {
        Assert.Empty(LabelConcepts.ExpectedKinds(LabelConcepts.From("Commentaire")));
    }

    [Theory]
    [InlineData("Email", "Courriel", 1.0)]
    [InlineData("Référence", "Commande n°", 1.0)]
    [InlineData("Nom de l'entreprise", "Nom", 2.0 / 3)]
    [InlineData("Ville", "Email", 0.0)]
    [InlineData("Ville", "", 0.0)]
    public void Similarity_MeasuresSharedConcepts(string first, string second, double expected)
    {
        var similarity = LabelConcepts.Similarity(LabelConcepts.From(first), LabelConcepts.From(second));

        Assert.Equal(expected, similarity, precision: 6);
    }

    [Theory]
    [InlineData("À")]
    [InlineData("Cc")]
    [InlineData("Cci")]
    [InlineData("To")]
    [InlineData("Destinataires")]
    public void From_WithRecipientLabel_TalksAboutAnEmail(string label)
    {
        Assert.Equal(new[] { LabelConcepts.Email }, LabelConcepts.From(label));
    }

    [Theory]
    [InlineData("Nom du destinataire")]
    [InlineData("Adresse du destinataire")]
    public void From_WithRecipientInALongerLabel_DoesNotTalkAboutAnEmail(string label)
    {
        Assert.DoesNotContain(LabelConcepts.Email, LabelConcepts.From(label));
    }
}
