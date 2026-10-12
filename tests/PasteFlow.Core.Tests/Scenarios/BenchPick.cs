using PasteFlow.Core.Picking;

namespace PasteFlow.Core.Tests.Scenarios;

/// <summary>
/// What the user does in the list shown at the cursor: types letters, widens the cut, removes words, then chooses.
/// </summary>
internal sealed record BenchPick
{
    public string? Query { get; init; }

    public int Widen { get; init; }

    public int ShrinkEnd { get; init; }

    /// <summary>
    /// Gets the choice to make by its text; the highlighted one when <see langword="null"/>.
    /// </summary>
    public string? Choose { get; init; }

    /// <summary>
    /// Plays these keys on the list, as the user would.
    /// </summary>
    /// <param name="request">What the list offers.</param>
    /// <param name="pick">What the user does, or <see langword="null"/> when nothing was planned: the list is closed.</param>
    /// <returns>The text chosen, or <see langword="null"/>.</returns>
    public static string? Play(PickRequest request, BenchPick? pick)
    {
        if (pick is null)
        {
            return null;
        }

        var state = new PickerState(request);
        if (pick.Query is not null)
        {
            state.SetQuery(pick.Query);
        }

        for (var step = 0; step < pick.Widen; step++)
        {
            state.NextCut();
        }

        for (var step = 0; step < pick.ShrinkEnd; step++)
        {
            state.ShrinkEnd();
        }

        return pick.Choose is null
            ? state.Selected?.Text
            : state.Items.FirstOrDefault(item => string.Equals(item.Text, pick.Choose, StringComparison.Ordinal))?.Text;
    }
}
