using PasteFlow.Core.Profile;

namespace PasteFlow.Core.Tests.Scenarios;

/// <summary>
/// A value of the profile of the user in a scenario of the test bench.
/// </summary>
internal sealed record BenchProfileValue
{
    public required ProfileTopic Topic { get; init; }

    public required string Value { get; init; }

    public int Uses { get; init; }

    public string Sites { get; init; } = string.Empty;

    public ProfileValue ToProfileValue()
    {
        return new ProfileValue { Topic = Topic, Value = Value, UseCount = Uses, Sites = Sites };
    }
}
