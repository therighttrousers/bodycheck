using System.IO;

using BodyCheck.Core;

using GdUnit4;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
public class FeaturesTest
{
    [TestCase]
    public void Reads_only_the_features_it_names()
    {
        Features features = Features.Read("test.jsonc", """
            {
                // A stronger jump.
                "PlayerJumpSpeed": 15,
                "PlayerPhaseThrow": "Conserved",
            }
            """);

        AssertThat(features).IsEqual(new Features { PlayerJumpSpeed = 15, PlayerPhaseThrow = PhaseThrow.Conserved });
    }

    [TestCase("""{ "PlayerJumpSped": 15 }""")]
    [TestCase("""{ "playerJumpSpeed": 15 }""")]
    [TestCase("""{ "PlayerJumpSpeed": "high" }""")]
    [TestCase("""{ "PlayerJumpSpeed": null }""")]
    [TestCase("""{ "PlayerPhaseThrow": 1 }""")]
    [TestCase("""{ "PlayerJumpSpeed": 15 """)]
    [TestCase("null")]
    public void Rejects_what_is_not_a_configuration(string json)
    {
        var e = Thrown<InvalidDataException>(() => Features.Read("test.jsonc", json));
        AssertThat(e.Message).StartsWith("test.jsonc: ");
    }
}
