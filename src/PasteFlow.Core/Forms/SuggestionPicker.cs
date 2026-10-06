using PasteFlow.Core.Undo;

namespace PasteFlow.Core.Forms;

/// <summary>
/// Writes the value the user picks among the suggestions of a field.
/// </summary>
/// <remarks>
/// A value picked belongs to the last fill: undoing the fill empties the field again.
/// </remarks>
public sealed class SuggestionPicker
{
    private readonly IFieldWriter _fieldWriter;
    private readonly FillMemory _fillMemory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuggestionPicker"/> class.
    /// </summary>
    /// <param name="fieldWriter">The component that writes values into the fields read.</param>
    /// <param name="fillMemory">The memory of the last fill, shared with the undo service.</param>
    public SuggestionPicker(IFieldWriter fieldWriter, FillMemory fillMemory)
    {
        ArgumentNullException.ThrowIfNull(fieldWriter);
        ArgumentNullException.ThrowIfNull(fillMemory);

        _fieldWriter = fieldWriter;
        _fillMemory = fillMemory;
    }

    /// <summary>
    /// Writes one of the suggestions of a field into it.
    /// </summary>
    /// <param name="fieldToCheck">The field and its suggestions.</param>
    /// <param name="index">The position of the suggestion picked, starting at 0.</param>
    /// <param name="cancellationToken">A token to cancel the writing.</param>
    /// <returns>
    /// <see langword="true"/> if the value was written; <see langword="false"/> if there is no such suggestion or the
    /// field refused it.
    /// </returns>
    public async Task<bool> TryPickAsync(FieldToCheck fieldToCheck, int index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fieldToCheck);

        if (index < 0 || index >= fieldToCheck.Suggestions.Count)
        {
            return false;
        }

        var value = fieldToCheck.Suggestions[index].Value;
        if (!await _fieldWriter.TryWriteAsync(fieldToCheck.Field, value, cancellationToken))
        {
            return false;
        }

        _fillMemory.Append(new WrittenField { Field = fieldToCheck.Field, Value = value });
        return true;
    }
}
