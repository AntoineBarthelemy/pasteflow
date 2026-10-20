using PasteFlow.Core.Browser;

namespace PasteFlow.Core.Tests.Browser;

public sealed class NativeMessagesTests
{
    [Fact]
    public async Task WriteThenRead_GivesTheSameMessageBack()
    {
        using var stream = new MemoryStream();

        await NativeMessages.WriteAsync(stream, """{"type":"read","texte":"Léa"}""", CancellationToken.None);
        stream.Position = 0;

        Assert.Equal("""{"type":"read","texte":"Léa"}""", await NativeMessages.ReadAsync(stream, CancellationToken.None));
        Assert.Null(await NativeMessages.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Write_PutsTheLengthFirstLowestByteFirst()
    {
        using var stream = new MemoryStream();

        await NativeMessages.WriteAsync(stream, "{}", CancellationToken.None);

        Assert.Equal(new byte[] { 2, 0, 0, 0, (byte)'{', (byte)'}' }, stream.ToArray());
    }

    [Fact]
    public async Task Read_RefusesAnAbsurdLength()
    {
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F });

        await Assert.ThrowsAsync<InvalidDataException>(() => NativeMessages.ReadAsync(stream, CancellationToken.None));
    }
}
