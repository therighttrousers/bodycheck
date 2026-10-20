namespace BodyCheck.Core.Tests;

public class FeaturesTests
{
    [Fact]
    public void ReadsOnlyTheFeaturesItNames()
    {
        Features features = Features.Read("test.jsonc", """
            {
                // A stronger jump.
                "PlayerJumpSpeed": 15,
                "PlayerPhaseThrow": "Conserved",
            }
            """);

        Assert.Equal(new Features { PlayerJumpSpeed = 15, PlayerPhaseThrow = PhaseThrow.Conserved }, features);
    }

    [Theory]
    [InlineData("""{ "PlayerJumpSped": 15 }""")]
    [InlineData("""{ "playerJumpSpeed": 15 }""")]
    [InlineData("""{ "PlayerJumpSpeed": "high" }""")]
    [InlineData("""{ "PlayerJumpSpeed": null }""")]
    [InlineData("""{ "PlayerPhaseThrow": 1 }""")]
    [InlineData("""{ "PlayerJumpSpeed": 15 """)]
    [InlineData("null")]
    public void RejectsWhatIsNotAConfiguration(string json)
    {
        var e = Assert.Throws<InvalidDataException>(() => Features.Read("test.jsonc", json));
        Assert.StartsWith("test.jsonc: ", e.Message);
    }
}
