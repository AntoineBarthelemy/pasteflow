using System.IO;
using PasteFlow.App.History;
using PasteFlow.Core.Profile;

namespace PasteFlow.App.Profile;

/// <summary>
/// Keeps the profile of the user in a file encrypted with their Windows account (DPAPI).
/// </summary>
/// <remarks>
/// Only the same Windows account, on the same computer, can read the file. To bring the profile to another computer,
/// the user exports it with a password (see <see cref="ProfileArchive"/>).
/// </remarks>
internal sealed class ProtectedFileProfileStore : IProfileStore
{
    private readonly ProtectedJsonFile<ProfileValue> _file;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileProfileStore"/> class.
    /// </summary>
    /// <param name="path">The path of the encrypted file.</param>
    public ProtectedFileProfileStore(string path)
    {
        _file = new ProtectedJsonFile<ProfileValue>(path, "PasteFlow.Profile.v1");
    }

    /// <summary>
    /// Gets the default path of the profile: the local application data folder of the user.
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "profile.dat");

    /// <summary>
    /// Gets the path of the values counted while waiting to be learned, next to the profile.
    /// </summary>
    public static string LearningPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PasteFlow",
        "profile-learning.dat");

    /// <inheritdoc/>
    public Task<IReadOnlyList<ProfileValue>> LoadAsync(CancellationToken cancellationToken)
    {
        return _file.LoadAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> TrySaveAsync(IReadOnlyList<ProfileValue> values, CancellationToken cancellationToken)
    {
        return _file.TrySaveAsync(values, cancellationToken);
    }
}
