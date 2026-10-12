namespace PasteFlow.Core.Security;

/// <summary>
/// What came of a request to check that the user is really there.
/// </summary>
public enum PresenceResult
{
    /// <summary>
    /// The user proved it is them: face, fingerprint or PIN.
    /// </summary>
    Verified,

    /// <summary>
    /// The user cancelled, or the check failed.
    /// </summary>
    Refused,

    /// <summary>
    /// Nothing can check the user on this computer: Windows Hello is not set up.
    /// </summary>
    Unavailable,
}
