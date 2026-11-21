namespace navigationshurely.services;

/// <summary>
/// Represents the transient application-level administrative session.
/// It is deliberately independent of WPF and is never persisted.
/// </summary>
public sealed class AdminSession
{
    public bool IsActive { get; private set; }

    /// <summary>
    /// Activates a session. Returns true only when transitioning from inactive.
    /// </summary>
    public bool Activate()
    {
        if (IsActive)
            return false;

        IsActive = true;
        return true;
    }

    /// <summary>
    /// Deactivates a session. Returns true only when transitioning from active.
    /// </summary>
    public bool Deactivate()
    {
        if (!IsActive)
            return false;

        IsActive = false;
        return true;
    }
}
