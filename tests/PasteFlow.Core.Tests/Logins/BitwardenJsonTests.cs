using PasteFlow.Core.Logins;
using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Logins;

public sealed class BitwardenJsonTests
{
    private const string Items = """
        [
          { "object": "item", "id": "a1", "type": 1, "reprompt": 0, "name": "GitHub",
            "login": { "uris": [ { "match": null, "uri": "https://github.com/login" } ], "username": "AntoineBarthelemy", "password": "secret-1" } },
          { "object": "item", "id": "a2", "type": 1, "reprompt": 1, "name": "Banque",
            "login": { "uris": [ { "uri": "https://banque.fr" } ], "username": "antoine", "password": "secret-2" } },
          { "object": "item", "id": "n1", "type": 2, "name": "Note sécurisée", "notes": "rien" },
          { "object": "item", "id": "i1", "type": 4, "name": "Moi",
            "identity": { "firstName": "Antoine", "lastName": "Barthélémy", "email": "antoine.b@orange.fr", "phone": null,
              "city": "Taissy", "postalCode": "51500", "ssn": "1 90 03 51 454 123 62", "passportNumber": "" } }
        ]
        """;

    [Fact]
    public void Logins_KeepTheAccountsWithoutTheirPasswords()
    {
        var login = Assert.Single(BitwardenJson.Logins(Items));

        Assert.Equal("a1", login.Id);
        Assert.Equal("GitHub", login.Name);
        Assert.Equal("AntoineBarthelemy", login.UserName);
        Assert.Equal(new[] { "https://github.com/login" }, login.Uris);
        Assert.DoesNotContain("secret", login.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_GivesItsValues()
    {
        var identity = BitwardenJson.Identity(Items);

        Assert.Equal(
            new[] { ProfileTopic.FirstName, ProfileTopic.LastName, ProfileTopic.Email, ProfileTopic.PostalCode, ProfileTopic.City, ProfileTopic.SocialSecurityNumber },
            identity.Select(value => value.Topic).Order());
        Assert.All(identity, value => Assert.Equal(ProfileOrigin.PasswordManager, value.Origin));
    }

    [Fact]
    public void SeveralIdentities_GiveNothing()
    {
        const string Two = """
            [ { "id": "i1", "type": 4, "identity": { "firstName": "Antoine" } },
              { "id": "i2", "type": 4, "identity": { "firstName": "Léa" } } ]
            """;

        Assert.Empty(BitwardenJson.Identity(Two));
    }

    [Theory]
    [InlineData("""{ "serverUrl": null, "userEmail": "a@b.fr", "status": "locked" }""", "locked")]
    [InlineData("""{ "status": "unlocked" }""", "unlocked")]
    [InlineData("pas du json", "")]
    [InlineData("[]", "")]
    public void Status_ReadsTheStateOfTheVault(string json, string expected)
    {
        Assert.Equal(expected, BitwardenJson.Status(json));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pas du json")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""[ { "id": "x", "type": 1.5 } ]""")]
    public void DamagedAnswers_GiveNothing(string json)
    {
        Assert.Empty(BitwardenJson.Logins(json));
        Assert.Empty(BitwardenJson.Identity(json));
    }
}
