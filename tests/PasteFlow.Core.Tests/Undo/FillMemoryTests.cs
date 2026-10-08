using PasteFlow.Core.Tests.Fakes;
using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Tests.Undo;

public sealed class FillMemoryTests
{
    [Fact]
    public void TakeLastFill_GivesTheRememberedValues()
    {
        var memory = new FillMemory();
        var written = new[] { Written("email", "antoine.b@orange.fr"), Written("phone", "06 47 21 85 39") };

        memory.Remember(written);

        Assert.Equal(written, memory.TakeLastFill());
    }

    [Fact]
    public void TakeLastFill_ForgetsTheFillSoItIsUndoneOnlyOnce()
    {
        var memory = new FillMemory();
        memory.Remember(new[] { Written("email", "antoine.b@orange.fr") });

        _ = memory.TakeLastFill();

        Assert.Empty(memory.TakeLastFill());
    }

    [Fact]
    public void Remember_KeepsOnlyTheLastFill()
    {
        var memory = new FillMemory();
        memory.Remember(new[] { Written("email", "antoine.b@orange.fr") });

        memory.Remember(new[] { Written("city", "Reims") });

        Assert.Equal(new[] { Written("city", "Reims") }, memory.TakeLastFill());
    }

    [Fact]
    public void Remember_FillThatWroteNothing_KeepsThePreviousOne()
    {
        var memory = new FillMemory();
        memory.Remember(new[] { Written("email", "antoine.b@orange.fr") });

        memory.Remember(Array.Empty<WrittenField>());

        Assert.Single(memory.TakeLastFill());
    }

    [Fact]
    public void Remember_CopiesTheValuesGiven()
    {
        var memory = new FillMemory();
        var written = new List<WrittenField> { Written("email", "antoine.b@orange.fr") };

        memory.Remember(written);
        written.Clear();

        Assert.Single(memory.TakeLastFill());
    }

    [Fact]
    public void Append_AddsAValueToTheLastFill()
    {
        var memory = new FillMemory();
        memory.Remember(new[] { Written("email", "antoine.b@orange.fr") });

        memory.Append(Written("city", "Reims"));

        Assert.Equal(new[] { Written("email", "antoine.b@orange.fr"), Written("city", "Reims") }, memory.TakeLastFill());
    }

    [Fact]
    public void Append_WithoutFill_StartsOne()
    {
        var memory = new FillMemory();

        memory.Append(Written("city", "Reims"));

        Assert.Equal(new[] { Written("city", "Reims") }, memory.TakeLastFill());
    }

    [Fact]
    public void Events_TellWhatIsFilledAppendedAndUndone()
    {
        var memory = new FillMemory();
        var events = new List<string>();
        memory.Filled += (_, written) => events.Add($"filled {written.Count}");
        memory.Appended += (_, written) => events.Add($"appended {written.Value}");
        memory.Undone += (_, written) => events.Add($"undone {written.Count}");

        memory.Remember(Array.Empty<WrittenField>());
        memory.Remember(new[] { Written("email", "antoine.b@orange.fr") });
        memory.Append(Written("city", "Reims"));
        memory.ReportUndone(Array.Empty<WrittenField>());
        memory.ReportUndone(new[] { Written("city", "Reims") });

        Assert.Equal(new[] { "filled 1", "appended Reims", "undone 1" }, events);
    }

    [Fact]
    public void TakeLastFill_WithoutFill_ReturnsNothing()
    {
        Assert.Empty(new FillMemory().TakeLastFill());
    }

    private static WrittenField Written(string id, string value)
    {
        return new WrittenField { Field = TestFields.Text(id, id), Value = value };
    }
}
